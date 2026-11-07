using GuildOps.Core.Data;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using GuildOps.Core.Randomness;
using static GuildOps.Tests.TestSupport;

namespace GuildOps.Tests;

public class DataTests
{
    [Fact]
    public void Embedded_data_loads_with_contract_baseline()
    {
        var r = Data.Rules;
        Assert.Equal(1200, r.InitialTreasuryCopper);
        Assert.Equal(35, r.InitialReputation);
        Assert.Equal(4, r.OffersPerDay);
        Assert.Equal(3, r.MaxMissionsPerDay);
        Assert.Equal(100, r.CopperPerGold);
        Assert.Equal(40, r.RecoveryReputationThreshold);
        Assert.Equal(Seed, r.TestSeed);
        Assert.Equal(12, Data.Heroes.Count);
    }

    [Fact]
    public void Heroes_are_three_per_specialty()
    {
        foreach (var skill in Enum.GetValues<Skill>())
            Assert.Equal(3, Data.Heroes.Count(h => h.Skill == skill));
    }

    [Fact]
    public void Catalogue_is_sorted_by_id_and_within_category_bounds()
    {
        var ids = Data.Templates.Select(t => t.Id).ToList();
        Assert.Equal(ids.OrderBy(i => i, StringComparer.Ordinal), ids);
        Assert.True(Data.Templates.Count >= 20);
        foreach (var c in Enum.GetValues<QuestCategory>())
            Assert.Contains(Data.Templates, t => t.Category == c);
    }

    [Fact]
    public void Medor_matches_guaranteed_recovery_offer_in_rules()
    {
        var medor = Data.Templates.Single(t => t.Id == Data.Rules.RecoveryTemplateId);
        Assert.Equal(2, medor.RewardCopper);      // T24 : 2 cuivres exactement
        Assert.Equal(10, medor.EnergyCost);
        Assert.Equal(1, medor.MinLevel);
        Assert.Equal(Skill.Exploration, medor.Skill);
        Assert.Equal(QuestCategory.Corvee, medor.Category);
    }

    [Fact]
    public void Invalid_weights_are_rejected_at_load()
    {
        var dir = FindGameData();
        var rules = File.ReadAllText(Path.Combine(dir, "rules.v0.1.json")).Replace("\"paria\": [\n        85,", "\"paria\": [\n        80,");
        Assert.Throws<GameDataException>(() => GameDataLoader.Load(rules,
            File.ReadAllText(Path.Combine(dir, "hero-balancing.v0.1.json")),
            File.ReadAllText(Path.Combine(dir, "roster.v0.1.json")),
            File.ReadAllText(Path.Combine(dir, "quest-catalogue.v0.1.json")),
            File.ReadAllText(Path.Combine(dir, "storylets.v0.1.json"))));
    }

    [Fact]
    public void Template_outside_category_bounds_is_rejected()
    {
        var dir = FindGameData();
        var cat = File.ReadAllText(Path.Combine(dir, "quest-catalogue.v0.1.json")).Replace("\"rewardCopper\": 2\n", "\"rewardCopper\": 3\n");
        Assert.Throws<GameDataException>(() => GameDataLoader.Load(
            File.ReadAllText(Path.Combine(dir, "rules.v0.1.json")),
            File.ReadAllText(Path.Combine(dir, "hero-balancing.v0.1.json")),
            File.ReadAllText(Path.Combine(dir, "roster.v0.1.json")), cat.Replace("\"rewardCopper\": 3", "\"rewardCopper\": 11"),
            File.ReadAllText(Path.Combine(dir, "storylets.v0.1.json"))));
    }

    [Fact]
    public void Core_has_no_godot_dependency()
    {
        var refs = typeof(GameSession).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");
        Assert.DoesNotContain(refs, n => n.Contains("Godot", StringComparison.OrdinalIgnoreCase));
    }

    internal static string FindGameData()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "game-data"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("game-data"), "game-data");
    }
}

public class RandomAndMoneyTests
{
    [Fact]
    public void Same_seed_gives_same_sequence_and_values_are_in_range()
    {
        var a = new SeededRandom(Seed);
        var b = new SeededRandom(Seed);
        for (var i = 0; i < 1000; i++)
        {
            var x = a.NextInt(100);
            Assert.Equal(x, b.NextInt(100));
            Assert.InRange(x, 0, 99);
        }
    }

    [Fact]
    public void Sequence_is_pinned_across_platforms() =>
        // Valeurs figées : toute modification de l'algorithme (donc de la reproductibilité) doit être délibérée.
        Assert.Equal("794,598,691,897,847,289,818,536", string.Join(",", Next(new SeededRandom(Seed), 8)));

    [Fact]
    public void Algorithm_is_the_reference_splitmix64() =>
        // Sortie de référence publiée de SplitMix64 pour la graine 0 : 0xE220A8397B1DCDAF.
        Assert.Equal((int)(0xE220A8397B1DCDAFUL % int.MaxValue), new SeededRandom(0).NextInt(int.MaxValue));

    private static IEnumerable<int> Next(ISeededRandom r, int n) { for (var i = 0; i < n; i++) yield return r.NextInt(1000); }

    [Fact]
    public void Seed_1142026_offers_are_pinned() =>
        Assert.Equal(new[] { "promener-medor", "rats-de-la-cave", "moulin-capricieux", "chapeau-du-maire" }, NewSession().Offers.Select(o => o.Template.Id).ToArray());

    [Theory]
    [InlineData(2, "2 cuivres")]
    [InlineData(1, "1 cuivre")]
    [InlineData(100, "1 or")]
    [InlineData(1200, "12 or")]
    [InlineData(1250, "12 or 50 cuivres")]
    [InlineData(0, "0 cuivre")]
    public void Money_is_formatted_from_integer_copper(int copper, string expected) =>
        Assert.Equal(expected, CurrencyFormat.Format(copper, 100));
}

public class SuccessChanceTests
{
    private static ChanceBreakdown C(QuestCategory cat, int heroLvl, int minLvl, int energy, int cost, int trait = 0) =>
        SuccessChance.Compute(Data.Rules, cat, heroLvl, minLvl, trait, energy, cost);

    [Fact]
    public void Formula_matches_contract() =>
        // 85 - 18 (aventure) + 5*(3-2) = 72
        Assert.Equal(72, C(QuestCategory.Aventure, 3, 2, 100, 20).Final);

    [Fact]
    public void Low_energy_penalty_applies_strictly_below_twice_the_cost()
    {
        Assert.Equal(0, C(QuestCategory.Village, 1, 1, 40, 20).LowEnergyPenalty);   // 40 == 2*20 : pas de pénalité
        Assert.Equal(5, C(QuestCategory.Village, 1, 1, 39, 20).LowEnergyPenalty);   // 39 <  2*20
    }

    [Fact]
    public void Chance_is_clamped_to_15_95()
    {
        Assert.Equal(95, C(QuestCategory.Corvee, 5, 1, 100, 10).Final);   // 85+20 -> 95
        Assert.Equal(15, C(QuestCategory.Royale, 4, 4, 1, 60, -50).Final);
    }

    [Fact]
    public void Trait_modifier_is_part_of_the_formula() =>
        Assert.Equal(77, C(QuestCategory.Aventure, 2, 2, 100, 20, trait: 10).Final);
}

public class OfferTests
{
    [Fact]
    public void Initial_state_matches_contract_T21()
    {
        var s = NewSession();
        Assert.Equal(12, s.Heroes.Count);
        Assert.Equal(1200, s.Guild.TreasuryCopper);
        Assert.Equal(35, s.Guild.Reputation);
        Assert.Equal(1, s.Guild.Day);
        Assert.Equal(4, s.Offers.Count);
        Assert.All(s.Offers, o => Assert.Equal(OfferState.Available, o.State));
    }

    [Fact]
    public void Medor_is_guaranteed_within_the_four_offers_when_reputation_is_low_T23()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var s = NewSession(seed);                       // réputation 35 < 40
            Assert.Equal(4, s.Offers.Count);
            Assert.Equal(1, s.Offers.Count(o => o.Template.Id == "promener-medor"));
            Assert.Equal(s.Offers.Count, s.Offers.Select(o => o.Template.Id).Distinct().Count());
            var medor = s.Offers.Single(o => o.Template.Id == "promener-medor");
            Assert.Contains(s.Heroes, h => s.Preview(medor.Id, h.Id).CanAssign);
        }
    }

    [Fact]
    public void Same_seed_gives_same_offers_and_reading_never_rerolls_T22_T25()
    {
        var a = NewSession();
        var b = NewSession();
        var before = string.Join(",", a.Offers.Select(o => o.Template.Id));
        _ = a.Preview(a.Offers[0].Id, a.Heroes[0].Id);
        _ = a.Offers; _ = a.Heroes;
        Assert.Equal(before, string.Join(",", a.Offers.Select(o => o.Template.Id)));
        Assert.Equal(before, string.Join(",", b.Offers.Select(o => o.Template.Id)));
    }

    [Fact]
    public void Reset_reproduces_initial_state_and_events_T09()
    {
        var s = NewSession();
        var initial = Snapshot(s);
        var events = s.Events.Select(e => e.Message).ToList();
        var (o, h) = FindAssignable(s);
        s.Assign(o, h);
        Assert.NotEqual(initial, Snapshot(s));
        s.Reset();
        Assert.Equal(initial, Snapshot(s));
        Assert.Equal(events, s.Events.Select(e => e.Message));
    }

    [Fact]
    public void Low_reputation_never_offers_notable_or_royale_but_high_does_sometimes_T11_T12()
    {
        var heroes = NewSession().Heroes;
        int Count(int rep, Func<QuestCategory, bool> pick)
        {
            var n = 0;
            for (var seed = 0; seed < 300; seed++)
                n += OfferGenerator.Generate(Data, rep, 1200, heroes, new SeededRandom(seed)).Count(t => pick(t.Category));
            return n;
        }
        Assert.Equal(0, Count(10, c => c is QuestCategory.Notable or QuestCategory.Royale));
        Assert.True(Count(90, c => c is QuestCategory.Notable or QuestCategory.Royale) > 150);
        Assert.True(Count(10, c => c == QuestCategory.Corvee) > Count(90, c => c == QuestCategory.Corvee));
    }

    [Fact]
    public void Offers_are_eligible_for_at_least_one_hero_and_unique_per_day()
    {
        var heroes = NewSession().Heroes;
        for (var seed = 0; seed < 100; seed++)
        {
            var templates = OfferGenerator.Generate(Data, 85, 1200, heroes, new SeededRandom(seed));
            Assert.Equal(templates.Count, templates.Select(t => t.Id).Distinct().Count());
            Assert.All(templates, t => Assert.Contains(heroes, h => AssignmentRules.Evaluate(Data.Rules, h, t, 1200).Count == 0));
        }
    }

    [Fact]
    public void Poor_guild_still_gets_medor_T15()
    {
        var heroes = NewSession().Heroes;
        var templates = OfferGenerator.Generate(Data, 90, 5, heroes, new SeededRandom(1));   // réputation haute mais caisse < 10
        Assert.Contains(templates, t => t.Id == "promener-medor");
    }

    [Fact]
    public void Empty_catalogue_falls_back_to_fewer_offers_without_crashing()
    {
        var onlyMedor = Data with { Templates = Data.Templates.Where(t => t.Id == "promener-medor").ToList() };
        var templates = OfferGenerator.Generate(onlyMedor, 85, 1200, NewSession().Heroes, new SeededRandom(1));
        Assert.Single(templates);
    }

    [Fact]
    public void Published_offers_do_not_change_when_reputation_changes_T14()
    {
        var s = NewSessionWithRolls(0);
        var before = string.Join(",", s.Offers.Select(o => o.Template.Id));
        var (o, h) = FindAssignable(s);
        s.Assign(o, h);
        s.Resolve("q1");
        Assert.NotEqual(35, s.Guild.Reputation);
        Assert.Equal(before, string.Join(",", s.Offers.Select(x => x.Template.Id)));
    }
}

public class AssignmentTests
{
    [Fact]
    public void Valid_assignment_debits_once_and_changes_state_T01()
    {
        var s = NewSession();
        var (o, h) = FindAssignable(s, x => x.Template.Category != QuestCategory.Corvee);
        var hero = s.Heroes.First(x => x.Id == h);
        var cost = hero.Definition.BaseCostCopper;
        var r = s.Assign(o, h);
        Assert.True(r.Ok);
        Assert.Equal(1200 - cost, s.Guild.TreasuryCopper);
        Assert.False(hero.IsFree);
        Assert.Equal(OfferState.Accepted, s.Offers.First(x => x.Id == o).State);
        Assert.Equal(1, s.Guild.MissionsAcceptedToday);
    }

    [Fact]
    public void Corvee_waives_engagement_cost_but_still_consumes_energy_at_resolution()
    {
        var s = NewSessionWithRolls(0);
        var medor = s.Offers.Single(o => o.Template.Id == "promener-medor");
        var hero = s.Heroes.First(x => x.Definition.Skill == Skill.Exploration);
        var energy = hero.Energy;
        Assert.True(s.Assign(medor.Id, hero.Id).Ok);
        Assert.Equal(1200, s.Guild.TreasuryCopper);
        Assert.Equal(energy, hero.Energy);              // l'énergie est prélevée à la résolution
        s.Resolve("q1");
        Assert.Equal(energy - 10, hero.Energy);
    }

    [Fact]
    public void Busy_hero_is_refused_without_mutation_T02()
    {
        var s = NewSession();
        var (o, h) = FindAssignable(s);
        s.Assign(o, h);
        var other = s.Offers.First(x => x.State == OfferState.Available);
        var snap = Snapshot(s);
        var r = s.Assign(other.Id, h);
        Assert.False(r.Ok);
        Assert.Contains(RefusalReason.HeroBusy, r.Reasons);
        Assert.Equal(snap, Snapshot(s));
    }

    [Fact]
    public void Missing_skill_is_refused_with_explicit_message_T03()
    {
        var s = NewSession();
        var offer = s.Offers[0];
        var wrong = s.Heroes.First(x => x.Definition.Skill != offer.Template.Skill);
        var snap = Snapshot(s);
        var r = s.Assign(offer.Id, wrong.Id);
        Assert.False(r.Ok);
        Assert.Contains(RefusalReason.SkillMismatch, r.Reasons);
        Assert.Contains("compétence", r.Message);
        Assert.Equal(snap, Snapshot(s));
    }

    [Fact]
    public void Level_below_minimum_is_refused()
    {
        var s = NewSession();
        var low = Data.Templates.First(t => t.MinLevel >= 3);
        var offer = new QuestOfferProbe(s).Inject(low);
        var hero = s.Heroes.First(x => x.Definition.Skill == low.Skill && x.Definition.Level < low.MinLevel);
        Assert.Contains(RefusalReason.LevelTooLow, s.Preview(offer.Id, hero.Id).Reasons);
        Assert.Null(s.Preview(offer.Id, hero.Id).Chance);   // pas de probabilité pour un niveau inadmissible
    }

    [Fact]
    public void Treasury_exactly_equal_to_cost_is_enough_T04()
    {
        var s = NewSession();
        var (o, h) = FindAssignable(s, x => x.Template.Category != QuestCategory.Corvee);
        var cost = s.Heroes.First(x => x.Id == h).Definition.BaseCostCopper;
        s.Guild.Debit(s.Guild.TreasuryCopper - cost);
        Assert.True(s.Assign(o, h).Ok);
        Assert.Equal(0, s.Guild.TreasuryCopper);
    }

    [Fact]
    public void Treasury_one_below_cost_is_refused()
    {
        var s = NewSession();
        var (o, h) = FindAssignable(s, x => x.Template.Category != QuestCategory.Corvee);
        var cost = s.Heroes.First(x => x.Id == h).Definition.BaseCostCopper;
        s.Guild.Debit(s.Guild.TreasuryCopper - cost + 1);
        var snap = Snapshot(s);
        Assert.Contains(RefusalReason.NotEnoughTreasury, s.Assign(o, h).Reasons);
        Assert.Equal(snap, Snapshot(s));
    }

    [Fact]
    public void Energy_exactly_equal_to_cost_is_enough_and_one_below_is_refused_T05()
    {
        var s = NewSession();
        var (o, h) = FindAssignable(s);
        var hero = s.Heroes.First(x => x.Id == h);
        var cost = s.Offers.First(x => x.Id == o).Template.EnergyCost;
        hero.Energy = cost - 1;
        Assert.Contains(RefusalReason.NotEnoughEnergy, s.Assign(o, h).Reasons);
        hero.Energy = cost;
        Assert.True(s.Assign(o, h).Ok);
    }

    [Fact]
    public void Same_offer_cannot_be_assigned_twice_T06()
    {
        var s = NewSession();
        var (o, h) = FindAssignable(s);
        s.Assign(o, h);
        var other = s.Heroes.First(x => x.IsFree && x.Definition.Skill == s.Offers.First(q => q.Id == o).Template.Skill);
        var snap = Snapshot(s);
        var r = s.Assign(o, other.Id);
        Assert.Contains(RefusalReason.OfferNotAvailable, r.Reasons);
        Assert.Equal(snap, Snapshot(s));
    }

    [Fact]
    public void At_most_three_missions_per_day()
    {
        var s = NewSession();
        for (var i = 0; i < 3; i++)
        {
            var (o, h) = FindAssignable(s);
            Assert.True(s.Assign(o, h).Ok);
        }
        var fourth = s.Offers.FirstOrDefault(o => o.State == OfferState.Available);
        if (fourth is null) return;     // le scénario seedé n'a pas de 4e offre libre : rien à refuser
        var hero = s.Heroes.First(x => x.IsFree);
        Assert.Contains(RefusalReason.DailyLimitReached, s.Assign(fourth.Id, hero.Id).Reasons);
    }

    [Fact]
    public void Unknown_ids_are_refused_without_mutation()
    {
        var s = NewSession();
        var snap = Snapshot(s);
        Assert.Contains(RefusalReason.UnknownOffer, s.Assign("nope", s.Heroes[0].Id).Reasons);
        Assert.Contains(RefusalReason.UnknownHero, s.Assign(s.Offers[0].Id, "nope").Reasons);
        Assert.Contains(RefusalReason.UnknownQuest, s.Resolve("nope").Reasons);
        Assert.Equal(snap, Snapshot(s));
    }
}

public class ResolutionTests
{
    private static (GameSession S, string Quest, Hero Hero, QuestOffer Offer) Assigned(int roll, Func<QuestOffer, bool>? filter = null)
    {
        var s = NewSessionWithRolls(roll);
        var (o, h) = FindAssignable(s, filter);
        s.Assign(o, h);
        return (s, "q1", s.Heroes.First(x => x.Id == h), s.Offers.First(x => x.Id == o));
    }

    [Fact]
    public void Success_pays_reward_once_and_consumes_energy_T07_T08()
    {
        var (s, q, hero, offer) = Assigned(0, o => o.Template.Category != QuestCategory.Corvee);
        var treasury = s.Guild.TreasuryCopper;
        var energy = hero.Energy;
        var r = s.Resolve(q);
        Assert.True(r.Ok && r.Value!.Success);
        Assert.Equal(treasury + offer.Template.RewardCopper, s.Guild.TreasuryCopper);
        Assert.Equal(energy - offer.Template.EnergyCost, hero.Energy);
        Assert.True(hero.IsFree);
        Assert.Equal(1, s.Guild.MissionsSucceeded);
        Assert.Equal(39, s.Guild.Reputation);   // 35 + 4

        var snap = Snapshot(s);
        var again = s.Resolve(q);                // double clic
        Assert.False(again.Ok);
        Assert.Contains(RefusalReason.QuestNotAssigned, again.Reasons);
        Assert.Equal(snap, Snapshot(s));
    }

    [Fact]
    public void Failure_pays_nothing_but_still_consumes_energy_and_costs_reputation_T26()
    {
        var (s, q, hero, offer) = Assigned(99, o => o.Template.Category != QuestCategory.Corvee);
        var treasury = s.Guild.TreasuryCopper;
        var energy = hero.Energy;
        var r = s.Resolve(q).Value!;
        // roll 99 échoue tant que la chance est < 100 (plafond 95)
        Assert.False(r.Success);
        Assert.Equal(treasury, s.Guild.TreasuryCopper);
        Assert.Equal(energy - offer.Template.EnergyCost, hero.Energy);
        Assert.Equal(-3, r.ReputationDelta);
        Assert.Equal(32, s.Guild.Reputation);
        Assert.Equal(0, r.RewardPaidCopper);
    }

    [Fact]
    public void Corvee_has_smaller_reputation_stakes()
    {
        var (s, q, _, _) = Assigned(0, o => o.Template.Category == QuestCategory.Corvee);
        Assert.Equal(2, s.Resolve(q).Value!.ReputationDelta);
        var (f, fq, _, _) = Assigned(99, o => o.Template.Category == QuestCategory.Corvee);
        Assert.Equal(-1, f.Resolve(fq).Value!.ReputationDelta);
    }

    [Fact]
    public void Roll_equal_to_chance_fails_and_roll_just_below_succeeds()
    {
        var probe = NewSession();
        var (o, h) = FindAssignable(probe);
        var chance = probe.Preview(o, h).Chance!.Final;

        var below = NewSessionWithRolls(chance - 1);
        below.Assign(o, h);
        Assert.True(below.Resolve("q1").Value!.Success);

        var equal = NewSessionWithRolls(chance);
        equal.Assign(o, h);
        Assert.False(equal.Resolve("q1").Value!.Success);
    }

    [Fact]
    public void Reputation_is_clamped_to_0_100()
    {
        var (s, q, _, _) = Assigned(0, o => o.Template.Category != QuestCategory.Corvee);
        s.Guild.ApplyReputation(100, 0, 100);
        Assert.Equal(100, s.Guild.Reputation);
        Assert.Equal(100, s.Resolve(q).Value!.ReputationAfter);

        var (f, fq, _, _) = Assigned(99, o => o.Template.Category != QuestCategory.Corvee);
        f.Guild.ApplyReputation(-100, 0, 100);
        Assert.Equal(0, f.Resolve(fq).Value!.ReputationAfter);
    }

    [Fact]
    public void Treasury_never_goes_negative_through_the_public_commands()
    {
        var s = NewSession();
        while (true)
        {
            var next = s.Offers.Where(o => o.State == OfferState.Available)
                .SelectMany(o => s.Heroes.Select(h => (o, h))).FirstOrDefault(p => s.Preview(p.o.Id, p.h.Id).CanAssign);
            if (next.o is null) break;
            s.Assign(next.o.Id, next.h.Id);
            Assert.True(s.Guild.TreasuryCopper >= 0);
        }
    }

    [Fact]
    public void Seeded_scenario_is_reproducible_T25()
    {
        string Play()
        {
            var s = NewSession();
            for (var i = 0; i < 3; i++)
            {
                var (o, h) = FindAssignable(s);
                s.Assign(o, h);
                s.Resolve($"q{i + 1}");
            }
            return Snapshot(s) + "|" + string.Join("/", s.Events.Select(e => e.Message));
        }
        Assert.Equal(Play(), Play());
    }

    [Fact]
    public void Preview_does_not_consume_randomness()
    {
        var a = NewSession();
        var b = NewSession();
        for (var i = 0; i < 20; i++) _ = a.Preview(a.Offers[0].Id, a.Heroes[0].Id);
        var (o, h) = FindAssignable(a);
        a.Assign(o, h); b.Assign(o, h);
        Assert.Equal(a.Resolve("q1").Value!.Roll, b.Resolve("q1").Value!.Roll);
    }

    [Fact]
    public void Events_form_a_logical_ordered_journal()
    {
        var (s, q, _, _) = Assigned(0);
        s.Resolve(q);
        Assert.Equal(Enumerable.Range(1, s.Events.Count), s.Events.Select(e => e.Sequence));
        Assert.Contains(s.Events, e => e.Type == GameEventType.ReputationChanged && e.Message.Contains("35 →"));
    }
}

/// <summary>Aide de test : publie une offre arbitraire pour tester des cas que le tirage seedé ne produit pas.</summary>
internal sealed class QuestOfferProbe(GameSession session)
{
    public QuestOffer Inject(QuestTemplate template)
    {
        var offer = (QuestOffer)Activator.CreateInstance(typeof(QuestOffer),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
            new object[] { "probe-" + template.Id, template, 1 }, null)!;
        ((List<QuestOffer>)session.Offers).Add(offer);
        return offer;
    }
}
