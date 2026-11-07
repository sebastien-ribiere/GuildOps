using GuildOps.Core.Data;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using GuildOps.Core.Randomness;
using static GuildOps.Tests.TestSupport;

namespace GuildOps.Tests;

/// <summary>Sessions AVEC les traits du catalogue V0 (le reste de la suite utilise <see cref="NoTraitEffects"/>).</summary>
internal static class TraitSupport
{
    public static GameSession NewTraitSession(out OverridableRandom rng, GameData? data = null)
    {
        var r = new OverridableRandom(new SeededRandom(Seed));
        rng = r;
        return GameSession.Start(data ?? Data, Seed, _ => r);
    }

    public static GameSession NewTraitSession() => NewTraitSession(out _);

    /// <summary>Catalogue où un unique storylet "toujours" (100 %) s'applique à tout le monde : isole l'effet du trait.</summary>
    public static GameData AlwaysIncident(int repairCost = 10) => Data with
    {
        Storylets = [new Storylet("E99", "Toujours", "texte", 1, 100, new StoryletConditions(OutcomeFilter.Any, [], [], []), repairCost, new ChoiceLabels("r", "a", "d"))],
    };
}

public class TraitCatalogueTests
{
    [Fact]
    public void Every_hero_of_the_roster_has_a_trait_in_the_catalogue()
    {
        Assert.All(Data.Heroes, h => Assert.True(TraitCatalog.Contains(h.TraitId), h.TraitId));
        Assert.All(Data.Heroes, h => Assert.NotNull(TraitCatalog.Default.For(h)));
    }

    [Fact]
    public void An_unknown_trait_is_rejected_at_load()
    {
        var dir = DataTests.FindGameData();
        var roster = File.ReadAllText(Path.Combine(dir, "roster.v0.1.json")).Replace("\"hotfix-muscle\"", "\"trait-fantome\"");
        var ex = Assert.Throws<GameDataException>(() => GameDataLoader.Load(
            File.ReadAllText(Path.Combine(dir, "rules.v0.1.json")),
            File.ReadAllText(Path.Combine(dir, "hero-balancing.v0.1.json")), roster,
            File.ReadAllText(Path.Combine(dir, "quest-catalogue.v0.1.json")),
            File.ReadAllText(Path.Combine(dir, "storylets.v0.1.json"))));
        Assert.Contains("trait-fantome", ex.Message);
    }

    [Fact]
    public void Twelve_traits_use_only_the_seven_typed_families()
    {
        var modifiers = Data.Heroes.SelectMany(h => TraitCatalog.Default.For(h)!.Modifiers).ToList();
        Assert.Equal(Enum.GetValues<TraitFamily>().Length, modifiers.Select(m => m.Family).Distinct().Count());
        // chaque trait a un atout et une contrepartie visibles dans les textes du roster
        Assert.All(Data.Heroes, h => Assert.False(string.IsNullOrWhiteSpace(h.TraitAdvantage + h.TraitDrawback)));
    }
}

public class TraitEffectTests
{
    // (héros, quête, Δ modificateur de chance, Δ engagement, Δ énergie, Δ prime) par rapport à la même affectation sans trait
    public static TheoryData<string, string, int, int, int, int> Expectations => new()
    {
        { "jean-michel-sre", "wifi-du-dragon", 0, 0, 0, 0 },          // non préparé : aucun effet tant que la catégorie n'est pas préparée
        { "gerard-dev", "rats-de-la-cave", 0, 0, -3, 0 },
        { "merlinda-qa", "tour-hantee", 6, 6, 0, 0 },
        { "borborygme-release", "canards-demoniaques", 0, 0, -4, 0 },
        { "archibald-platform", "moulin-capricieux", 0, 4, 0, 0 },    // pas encore d'expérience : seule la contrepartie s'applique
        { "prudence-productowner", "canards-demoniaques", 0, 0, 0, 18 },
        { "prudence-productowner", "poules-en-fuite", -6, 0, 0, 0 },
        { "hermine-secops", "escorte-du-marchand", 4, 8, 0, 0 },
        { "gaston-finops", "moulin-capricieux", -4, -6, 0, 0 },
        { "alice-ux", "canards-demoniaques", 0, 0, 2, 0 },
        { "leon-dataops", "tour-hantee", 8, 0, 3, 0 },
        { "zelie-cloudops", "tour-hantee", 10, 12, 0, 0 },
        { "clovis-scrum", "bandits-de-la-route", 0, 0, 2, 0 },
    };

    [Theory]
    [MemberData(nameof(Expectations))]
    public void Trait_changes_the_assignment_by_the_declared_amounts(string hero, string quest, int dChance, int dEngagement, int dEnergy, int dReward)
    {
        var plain = NewSession();
        var traited = TraitSupport.NewTraitSession();
        var a = plain.Preview(Inject(plain, quest).Id, hero);
        var b = traited.Preview(Inject(traited, quest).Id, hero);

        Assert.True(a.CanAssign && b.CanAssign, b.Message);
        Assert.Equal(dChance, b.Chance!.TraitModifier - a.Chance!.TraitModifier);
        Assert.Equal(dEngagement, b.EngagementCostCopper - a.EngagementCostCopper);
        Assert.Equal(dEnergy, b.EnergyCost - a.EnergyCost);
        Assert.Equal(dReward, b.RewardCopper - a.RewardCopper);
        Assert.NotEmpty(b.Notes);   // l'effet est prévisible avant l'engagement
    }

    [Fact]
    public void Previewed_energy_and_reward_are_exactly_what_the_resolution_applies()
    {
        var s = TraitSupport.NewTraitSession(out var rng);
        rng.Override = new ScriptedRandom(0);
        var offer = Inject(s, "canards-demoniaques");
        var preview = s.Preview(offer.Id, "prudence-productowner");
        var energyBefore = s.Heroes.Single(h => h.Id == "prudence-productowner").Energy;
        var treasuryBefore = s.Guild.TreasuryCopper;

        Assert.True(s.Assign(offer.Id, "prudence-productowner").Ok);
        var outcome = s.Resolve("q1").Value!;

        Assert.True(outcome.Success);
        Assert.Equal(108, outcome.RewardPaidCopper);
        Assert.Equal(preview.RewardCopper, outcome.RewardPaidCopper);
        Assert.Equal(preview.EnergyCost, outcome.EnergySpent);
        Assert.Equal(energyBefore - preview.EnergyCost, s.Heroes.Single(h => h.Id == "prudence-productowner").Energy);
        Assert.Equal(treasuryBefore - preview.EngagementCostCopper + 108, s.Guild.TreasuryCopper);
    }

    [Fact]
    public void A_failure_pays_no_boosted_reward()
    {
        var s = TraitSupport.NewTraitSession(out var rng);
        rng.Override = new ScriptedRandom(99);
        var offer = Inject(s, "canards-demoniaques");
        s.Assign(offer.Id, "prudence-productowner");
        Assert.Equal(0, s.Resolve("q1").Value!.RewardPaidCopper);
    }

    [Fact]
    public void Energy_cost_never_drops_below_one_and_engagement_never_below_zero()
    {
        var s = TraitSupport.NewTraitSession();
        var offer = Inject(s, "chapeau-du-maire");     // corvée : frais exemptés, énergie 12
        var p = s.Preview(offer.Id, "gerard-dev");
        Assert.Equal(0, p.EngagementCostCopper);       // l'exemption de frais l'emporte sur tout modificateur
        Assert.Equal(9, p.EnergyCost);                 // 12 - 3
        Assert.True(p.EnergyCost >= 1);
    }

    [Fact]
    public void Incident_probability_modifiers_shift_the_storylet_roll_and_are_clamped()
    {
        var e01 = Data.Storylets.Single(s => s.Id == "E01");
        var ctx = new MissionContext("gerard-dev", Data.Templates.Single(t => t.Id == "rats-de-la-cave"), true);
        Assert.Null(StoryletSelector.Select([e01], ctx, new ScriptedRandom(50)));                      // 50 >= 40
        Assert.Equal("E01", StoryletSelector.Select([e01], ctx, new ScriptedRandom(50), +15)!.Id);       // 50 < 55
        Assert.Null(StoryletSelector.Select([e01], ctx, new ScriptedRandom(25), -20));                   // 25 >= 20 (zero-trust)
        Assert.Equal("E01", StoryletSelector.Select([e01], ctx, new ScriptedRandom(99), +100)!.Id);      // borné à 100 %
        Assert.Null(StoryletSelector.Select([e01], ctx, new ScriptedRandom(0), -100));                   // borné à 0 %
    }

    [Fact]
    public void Gerard_triggers_the_wall_incident_more_often_than_the_base_probability()
    {
        // tirage de mission 0 (succès), tirage de storylet 50 : 50 >= 40 sans trait, 50 < 55 avec le trait de Gérard
        var plain = NewSessionWithRolls(0, 50);
        plain.Assign(Inject(plain, "rats-de-la-cave").Id, "gerard-dev");
        Assert.Null(plain.Resolve("q1").Value!.Incident);

        var s = TraitSupport.NewTraitSession(out var rng);
        rng.Override = new ScriptedRandom(0, 50);
        s.Assign(Inject(s, "rats-de-la-cave").Id, "gerard-dev");
        Assert.Equal("E01", s.Resolve("q1").Value!.Incident!.StoryletId);
    }

    [Fact]
    public void Alice_adds_reputation_when_the_complaint_is_repaired_or_apologised_but_not_denied()
    {
        foreach (var (choice, expected) in new[] { (ComplaintChoice.Repair, 3 + 2), (ComplaintChoice.Apologize, 1 + 2), (ComplaintChoice.Deny, -2) })
        {
            var s = TraitSupport.NewTraitSession(out var rng, TraitSupport.AlwaysIncident());
            rng.Override = new ScriptedRandom(0, 0);
            s.Assign(Inject(s, "canards-demoniaques").Id, "alice-ux");
            Assert.NotNull(s.Resolve("q1").Value!.Incident);
            Assert.Equal(choice == ComplaintChoice.Deny ? 0 : 2, s.ComplaintTraitBonus("c1", choice));

            var outcome = s.ResolveComplaint("c1", choice).Value!;
            Assert.Equal(expected, outcome.ReputationDelta);
        }
    }

    [Fact]
    public void Other_heroes_get_no_complaint_bonus()
    {
        var s = TraitSupport.NewTraitSession(out var rng, TraitSupport.AlwaysIncident());
        rng.Override = new ScriptedRandom(0, 0);
        s.Assign(Inject(s, "bandits-de-la-route").Id, "hermine-secops");
        s.Resolve("q1");
        Assert.Equal(0, s.ComplaintTraitBonus("c1", ComplaintChoice.Apologize));
        Assert.Equal(1, s.ResolveComplaint("c1", ComplaintChoice.Apologize).Value!.ReputationDelta);
    }

    [Fact]
    public void Clovis_shares_energy_with_the_others_after_a_mission_without_exceeding_the_cap()
    {
        var s = TraitSupport.NewTraitSession(out var rng);
        rng.Override = new ScriptedRandom(0);
        var before = s.Heroes.ToDictionary(h => h.Id, h => h.Energy);
        s.Assign(Inject(s, "chapeau-du-maire").Id, "clovis-scrum");
        var outcome = s.Resolve("q1").Value!;

        Assert.Equal(14, outcome.EnergySpent);     // 12 + 2 (la cérémonie)
        Assert.Equal(before["clovis-scrum"] - 14, s.Heroes.Single(h => h.Id == "clovis-scrum").Energy);
        Assert.All(s.Heroes.Where(h => h.Id != "clovis-scrum"), h => Assert.Equal(Math.Min(100, before[h.Id] + 3), h.Energy));
        Assert.Contains(s.Events, e => e.Type == GameEventType.EnergyRecovered && e.Message.Contains("coordonne"));
    }

    [Fact]
    public void Archibald_gets_cheaper_only_after_a_first_mission_of_the_same_category()
    {
        var s = TraitSupport.NewTraitSession(out var rng);
        rng.Override = new ScriptedRandom(0);
        var first = Inject(s, "pont-qui-grince");
        var second = Inject(s, "moulin-capricieux");      // même catégorie (village), compétence ingénierie

        var before = s.Preview(second.Id, "archibald-platform");
        Assert.Equal(22, before.EnergyCost);
        Assert.Contains(before.Notes, n => n.StartsWith("Inactif") && n.Contains("première mission"));

        s.Assign(first.Id, "archibald-platform");
        s.Resolve("q1");
        Assert.Equal(1, s.Heroes.Single(h => h.Id == "archibald-platform").MissionsDone(QuestCategory.Village));
        Assert.Equal(0, s.Heroes.Single(h => h.Id == "archibald-platform").MissionsDone(QuestCategory.Aventure));

        var after = s.Preview(second.Id, "archibald-platform");
        Assert.Equal(18, after.EnergyCost);               // 22 - 4
        Assert.Contains(after.Notes, n => n.StartsWith("Actif") && n.Contains("première mission"));
    }
}

public class PreparationTests
{
    private const string Jm = "jean-michel-sre";

    [Fact]
    public void Preparing_a_category_costs_energy_and_copper_and_unlocks_a_reusable_bonus_for_that_category_only()
    {
        var s = TraitSupport.NewTraitSession();
        var offer = Inject(s, "wifi-du-dragon");
        Assert.Equal(0, s.Preview(offer.Id, Jm).Chance!.TraitModifier);
        Assert.Contains(s.Preview(offer.Id, Jm).Notes, n => n.StartsWith("Inactif"));

        var energy = s.Heroes.Single(h => h.Id == Jm).Energy;
        var treasury = s.Guild.TreasuryCopper;
        var result = s.Prepare(Jm, QuestCategory.Aventure);

        Assert.True(result.Ok, result.Message);
        Assert.Equal((10, 15), (result.Value!.EnergySpent, result.Value.CopperSpent));
        Assert.Equal(energy - 10, s.Heroes.Single(h => h.Id == Jm).Energy);
        Assert.Equal(treasury - 15, s.Guild.TreasuryCopper);
        Assert.Equal(0, s.Guild.MissionsAcceptedToday);                       // ne compte pas comme une mission
        Assert.Equal(12, s.Preview(offer.Id, Jm).Chance!.TraitModifier);      // aventure : bonus actif
        Assert.Equal(0, s.Preview(Inject(s, "pont-qui-grince").Id, Jm).Chance!.TraitModifier);   // village : pas préparé
        Assert.Contains(s.Events, e => e.Type == GameEventType.HeroPrepared);
    }

    [Fact]
    public void The_bonus_is_reusable_on_every_later_mission_of_the_category()
    {
        var s = TraitSupport.NewTraitSession(out var rng);
        rng.Override = new ScriptedRandom(0);
        s.Prepare(Jm, QuestCategory.Aventure);
        s.Assign(Inject(s, "wifi-du-dragon").Id, Jm);
        s.Resolve("q1");
        // une autre mission de la même catégorie : le bonus est toujours là (golem : niveau 4 requis, Jean-Michel est niveau 3)
        var again = s.Preview(Inject(s, "wifi-du-dragon").Id, Jm);
        Assert.Equal(12, again.Chance!.TraitModifier);
        Assert.True(s.Heroes.Single(h => h.Id == Jm).IsPrepared(QuestCategory.Aventure));
    }

    [Fact]
    public void Refusals_are_atomic_and_explained()
    {
        var s = TraitSupport.NewTraitSession();
        var snapshot = Snapshot(s);

        Assert.Contains(RefusalReason.NoPreparationTrait, s.Prepare("gerard-dev", QuestCategory.Village).Reasons);
        Assert.Contains(RefusalReason.UnknownHero, s.Prepare("personne", QuestCategory.Village).Reasons);
        Assert.Equal(snapshot, Snapshot(s));

        Assert.True(s.Prepare(Jm, QuestCategory.Aventure).Ok);
        snapshot = Snapshot(s);
        var again = s.Prepare(Jm, QuestCategory.Aventure);
        Assert.Contains(RefusalReason.AlreadyPrepared, again.Reasons);
        Assert.Equal(snapshot, Snapshot(s));

        s.Assign(Inject(s, "pont-qui-grince").Id, Jm);
        snapshot = Snapshot(s);
        Assert.Contains(RefusalReason.HeroBusy, s.Prepare(Jm, QuestCategory.Notable).Reasons);
        Assert.Equal(snapshot, Snapshot(s));
    }

    [Fact]
    public void Not_enough_energy_or_copper_refuses_the_preparation()
    {
        var poor = Data with { Rules = Data.Rules with { InitialTreasuryCopper = 10 } };
        var p = TraitSupport.NewTraitSession(out _, poor);
        Assert.Contains(RefusalReason.NotEnoughTreasury, p.Prepare(Jm, QuestCategory.Aventure).Reasons);
        Assert.Equal(10, p.Guild.TreasuryCopper);

        var tired = Data with { Heroes = Data.Heroes.Select(h => h.Id == Jm ? h with { StartingEnergy = 5 } : h).ToList() };
        var t = TraitSupport.NewTraitSession(out _, tired);
        Assert.Contains(RefusalReason.NotEnoughEnergy, t.Prepare(Jm, QuestCategory.Aventure).Reasons);
    }

    [Fact]
    public void Reset_forgets_preparations_and_experience()
    {
        var s = TraitSupport.NewTraitSession(out var rng);
        rng.Override = new ScriptedRandom(0);
        s.Prepare(Jm, QuestCategory.Aventure);
        s.Assign(Inject(s, "pont-qui-grince").Id, Jm);
        s.Resolve("q1");
        s.Reset();
        var jm = s.Heroes.Single(h => h.Id == Jm);
        Assert.False(jm.IsPrepared(QuestCategory.Aventure));
        Assert.Equal(0, jm.MissionsDone(QuestCategory.Village));
        Assert.True(s.Prepare(Jm, QuestCategory.Aventure).Ok);
    }

    [Fact]
    public void Preparation_is_refused_once_the_game_is_finished()
    {
        var s = TraitSupport.NewTraitSession();
        Bots.Idle(s);
        Assert.Contains(RefusalReason.GameFinished, s.Prepare(Jm, QuestCategory.Aventure).Reasons);
    }
}

public class StoryletCatalogueB3Tests
{
    private static MissionContext Ctx(string hero, string template, bool success, int reputation = 35, int treasury = 1200, int resolvedToday = 1) =>
        new(hero, Data.Templates.Single(t => t.Id == template), success, reputation, treasury, resolvedToday);

    private static Storylet S(string id) => Data.Storylets.Single(s => s.Id == id);

    [Fact]
    public void E03_to_E08_match_their_declared_conditions()
    {
        Assert.True(StoryletSelector.Matches(S("E03"), Ctx("merlinda-qa", "tour-hantee", true)));
        Assert.False(StoryletSelector.Matches(S("E03"), Ctx("merlinda-qa", "tour-hantee", false)));
        Assert.False(StoryletSelector.Matches(S("E03"), Ctx("leon-dataops", "tour-hantee", true)));

        Assert.True(StoryletSelector.Matches(S("E04"), Ctx("alice-ux", "canards-demoniaques", true, treasury: 300)));
        Assert.False(StoryletSelector.Matches(S("E04"), Ctx("alice-ux", "canards-demoniaques", true, treasury: 301)));

        Assert.True(StoryletSelector.Matches(S("E05"), Ctx("alice-ux", "canards-demoniaques", true, resolvedToday: 2)));
        Assert.False(StoryletSelector.Matches(S("E05"), Ctx("alice-ux", "canards-demoniaques", true, resolvedToday: 1)));

        Assert.True(StoryletSelector.Matches(S("E06"), Ctx("archibald-platform", "moulin-capricieux", true)));
        Assert.False(StoryletSelector.Matches(S("E06"), Ctx("archibald-platform", "moulin-capricieux", false)));

        Assert.True(StoryletSelector.Matches(S("E07"), Ctx("borborygme-release", "canards-demoniaques", false)));
        Assert.False(StoryletSelector.Matches(S("E07"), Ctx("borborygme-release", "canards-demoniaques", true)));

        Assert.True(StoryletSelector.Matches(S("E08"), Ctx("alice-ux", "canards-demoniaques", true, reputation: 60)));
        Assert.False(StoryletSelector.Matches(S("E08"), Ctx("alice-ux", "canards-demoniaques", true, reputation: 59)));
        Assert.False(StoryletSelector.Matches(S("E08"), Ctx("alice-ux", "canards-demoniaques", false, reputation: 80)));
    }

    [Fact]
    public void Every_storylet_is_bounded_and_none_is_the_reserved_royal_coat_of_arms()
    {
        Assert.Equal(8, Data.Storylets.Count);
        Assert.All(Data.Storylets, s =>
        {
            Assert.InRange(s.ProbabilityPercent, 1, 99);
            Assert.InRange(s.RepairCostCopper, 1, 200);
            Assert.False(string.IsNullOrWhiteSpace(s.Text));
            Assert.DoesNotContain("blason", s.Title + s.Text, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void E05_triggers_on_the_second_mission_resolved_the_same_day_and_not_the_first()
    {
        var s = NewSessionWithRolls(0, 0, 0);      // mission 1, mission 2, jet de storylet
        s.Assign(Inject(s, "canards-demoniaques").Id, "alice-ux");
        s.Assign(Inject(s, "moulin-capricieux").Id, "gaston-finops");
        Assert.Null(s.Resolve("q1").Value!.Incident);
        Assert.Equal("E05", s.Resolve("q2").Value!.Incident!.StoryletId);
    }
}

public class TraitSimulationTests
{
    [Fact]
    public void Same_seed_and_traits_replay_identically()
    {
        string Run()
        {
            var s = GameSession.Start(Data, 77);
            Bots.Greedy(s);
            return Snapshot(s) + "|" + string.Join("/", s.Events.Select(e => e.Message));
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Greedy_play_with_traits_and_all_storylets_keeps_every_invariant()
    {
        for (var seed = 1; seed <= 150; seed++)
        {
            var s = GameSession.Start(Data, seed);
            Bots.Greedy(s, Bots.AssertInvariants);
            Assert.Equal(GameStatus.Finished, s.Status);
            Assert.All(s.Complaints, c => Assert.True(c.Storylet.RepairCostCopper >= 0));
        }
    }
}
