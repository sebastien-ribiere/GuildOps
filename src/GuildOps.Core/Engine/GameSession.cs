using GuildOps.Core.Data;
using GuildOps.Core.Domain;
using GuildOps.Core.Randomness;

namespace GuildOps.Core.Engine;

/// <summary>
/// Façade du moteur. Les commandes (Assign, Resolve, Reset) sont atomiques : un refus ne modifie rien.
/// Les requêtes (Preview, propriétés) ne modifient jamais l'état et ne tirent jamais le hasard.
/// </summary>
public sealed class GameSession
{
    private readonly Func<long, ISeededRandom> _rngFactory;
    private readonly ITraitEffects _traits;
    private ISeededRandom _rng = null!;
    private readonly List<Hero> _heroes = [];
    private readonly List<QuestOffer> _offers = [];
    private readonly List<Quest> _quests = [];
    private readonly List<GameEvent> _events = [];
    private int _questCounter;

    private GameSession(GameData data, long seed, Func<long, ISeededRandom> rngFactory, ITraitEffects traits)
    {
        Data = data;
        Seed = seed;
        _rngFactory = rngFactory;
        _traits = traits;
        Guild = null!;
    }

    public static GameSession Start(GameData data, long seed, Func<long, ISeededRandom>? rngFactory = null, ITraitEffects? traits = null)
    {
        var session = new GameSession(data, seed, rngFactory ?? (s => new SeededRandom(s)), traits ?? new NoTraitEffects());
        session.Initialize();
        return session;
    }

    public GameData Data { get; }
    public long Seed { get; }
    public Guild Guild { get; private set; }
    public IReadOnlyList<Hero> Heroes => _heroes;
    public IReadOnlyList<QuestOffer> Offers => _offers;
    public IReadOnlyList<Quest> Quests => _quests;
    public IReadOnlyList<GameEvent> Events => _events;
    public string ReputationTierId => Data.Rules.TierFor(Guild.Reputation).Id;

    /// <summary>Rejoue l'état initial depuis la même graine (R12) : mêmes héros, mêmes offres, même journal de départ.</summary>
    public void Reset() => Initialize();

    private void Initialize()
    {
        var rules = Data.Rules;
        _rng = _rngFactory(Seed);
        _heroes.Clear();
        _offers.Clear();
        _quests.Clear();
        _events.Clear();
        _questCounter = 0;
        _heroes.AddRange(Data.Heroes.Select(d => new Hero(d)));
        Guild = new Guild(rules.InitialTreasuryCopper, rules.InitialReputation, rules.InitialDay);

        Log(GameEventType.GameStarted, $"Nouvelle partie (graine {Seed}) : {_heroes.Count} aventuriers, {Money(Guild.TreasuryCopper)}, réputation {Guild.Reputation}.");
        PublishOffers();
    }

    private void PublishOffers()
    {
        var templates = OfferGenerator.Generate(Data, Guild.Reputation, Guild.TreasuryCopper, _heroes, _rng);
        for (var i = 0; i < templates.Count; i++)
            _offers.Add(new QuestOffer($"d{Guild.Day}-o{i + 1}", templates[i], Guild.Day));
        Log(GameEventType.OffersPublished, $"Jour {Guild.Day} : {templates.Count} offres publiées (palier « {ReputationTierId} »).");
    }

    public AssignmentCheck Preview(string offerId, string heroId)
    {
        var offer = _offers.FirstOrDefault(o => o.Id == offerId);
        if (offer is null) return Refuse(RefusalReason.UnknownOffer);
        var hero = _heroes.FirstOrDefault(h => h.Id == heroId);
        if (hero is null) return Refuse(RefusalReason.UnknownHero);

        var reasons = new List<RefusalReason>();
        if (offer.State != OfferState.Available) reasons.Add(RefusalReason.OfferNotAvailable);
        if (Guild.MissionsAcceptedToday >= Data.Rules.MaxMissionsPerDay) reasons.Add(RefusalReason.DailyLimitReached);
        reasons.AddRange(AssignmentRules.Evaluate(Data.Rules, hero, offer.Template, Guild.TreasuryCopper));

        // La probabilité n'a de sens que si le niveau est admissible (le bonus de niveau n'est jamais négatif).
        ChanceBreakdown? chance = hero.Definition.Level >= offer.Template.MinLevel
            ? SuccessChance.Compute(Data.Rules, offer.Template.Category, hero.Definition.Level, offer.Template.MinLevel,
                _traits.SuccessModifierPp(hero.Definition, offer.Template), hero.Energy, offer.Template.EnergyCost)
            : null;

        return new AssignmentCheck(reasons.Count == 0, reasons, AssignmentRules.EngagementCost(Data.Rules, hero, offer.Template), chance);
    }

    public Result<Quest> Assign(string offerId, string heroId)
    {
        var check = Preview(offerId, heroId);
        if (!check.CanAssign) return Result<Quest>.Refused(check.Reasons);

        var offer = _offers.First(o => o.Id == offerId);
        var hero = _heroes.First(h => h.Id == heroId);

        // Transaction : toutes les vérifications sont faites, les mutations suivantes ne peuvent plus échouer.
        Guild.Debit(check.EngagementCostCopper);
        var quest = new Quest($"q{++_questCounter}", offer, hero.Id, check.EngagementCostCopper);
        offer.State = OfferState.Accepted;
        hero.AssignedQuestId = quest.Id;
        Guild.MissionsAcceptedToday++;
        _quests.Add(quest);
        Log(GameEventType.MissionAssigned,
            $"{hero.Definition.Name} part pour « {offer.Template.Title} » (engagement : {Money(check.EngagementCostCopper)}).");
        return Result<Quest>.Success(quest);
    }

    public Result<MissionOutcome> Resolve(string questId)
    {
        var quest = _quests.FirstOrDefault(q => q.Id == questId);
        if (quest is null) return Result<MissionOutcome>.Refused([RefusalReason.UnknownQuest]);
        if (quest.State != QuestState.Assigned) return Result<MissionOutcome>.Refused([RefusalReason.QuestNotAssigned]);

        var rules = Data.Rules;
        var hero = _heroes.First(h => h.Id == quest.HeroId);
        var template = quest.Template;
        var chance = SuccessChance.Compute(rules, template.Category, hero.Definition.Level, template.MinLevel,
            _traits.SuccessModifierPp(hero.Definition, template), hero.Energy, template.EnergyCost);

        var roll = _rng.NextInt(SuccessFormula.RollRange);
        var success = roll < chance.Final;
        var isCommunity = template.Category == QuestCategory.Corvee;

        // Mutations : énergie débitée une fois, prime versée une fois et seulement en cas de succès.
        hero.Energy -= template.EnergyCost;
        hero.AssignedQuestId = null;
        quest.State = success ? QuestState.Succeeded : QuestState.Failed;
        var reward = success ? template.RewardCopper : 0;
        Guild.Credit(reward);
        if (success) Guild.MissionsSucceeded++;

        var delta = success
            ? (isCommunity ? rules.Deltas.CommunitySuccess : rules.Deltas.RegularSuccess)
            : (isCommunity ? rules.Deltas.CommunityFailure : rules.Deltas.RegularFailure);
        var before = Guild.Reputation;
        var after = Guild.ApplyReputation(delta, rules.ReputationMin, rules.ReputationMax);
        var reason = $"{(success ? "Succès" : "Échec")} de « {template.Title} »";

        Log(GameEventType.MissionResolved,
            $"{hero.Definition.Name} {(success ? "réussit" : "échoue")} « {template.Title} » (tirage {roll} / chance {chance.Final} %) : " +
            $"{(success ? $"prime {Money(reward)}" : "aucune prime")}, énergie -{template.EnergyCost}.");
        Log(GameEventType.ReputationChanged, $"Réputation {before} → {after} ({delta:+#;-#;0}) : {reason}.");

        return Result<MissionOutcome>.Success(new MissionOutcome(
            quest.Id, hero.Id, success, roll, chance.Final, reward, template.EnergyCost, before, after, reason));
    }

    public string Money(int copper) => CurrencyFormat.Format(copper, Data.Rules.CopperPerGold);

    private static AssignmentCheck Refuse(RefusalReason reason) => new(false, [reason], 0, null);

    private void Log(GameEventType type, string message) => _events.Add(new GameEvent(_events.Count + 1, Guild.Day, type, message));
}
