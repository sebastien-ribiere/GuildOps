using GuildOps.Core.Data;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using static GuildOps.Tests.TestSupport;

namespace GuildOps.Tests;

public class StoryletDataTests
{
    [Fact]
    public void Catalogue_has_the_eight_storylets_and_keeps_the_two_decided_ones()
    {
        Assert.Equal(["E01", "E02", "E03", "E04", "E05", "E06", "E07", "E08"], Data.Storylets.Select(s => s.Id).OrderBy(x => x, StringComparer.Ordinal));
        var e01 = Data.Storylets.Single(s => s.Id == "E01");
        var e02 = Data.Storylets.Single(s => s.Id == "E02");
        Assert.Equal((40, 80), (e01.ProbabilityPercent, e01.RepairCostCopper));
        Assert.Equal((60, 120), (e02.ProbabilityPercent, e02.RepairCostCopper));
        Assert.Equal(2, Data.ComplaintExpiryDays);
        Assert.Equal(-4, Data.Rules.Deltas.CollateralDamage);
        Assert.DoesNotContain(Data.Storylets, s => s.Title.Contains("blason", StringComparison.OrdinalIgnoreCase));   // réservé au TP final
    }

    private static string Replace(string file, string from, string to)
    {
        var dir = DataTests.FindGameData();
        string Read(string f) => File.ReadAllText(Path.Combine(dir, f));
        string Pick(string f) => f == file ? Read(f).Replace(from, to) : Read(f);
        return string.Join("\u0001", Pick("rules.v0.1.json"), Pick("hero-balancing.v0.1.json"), Pick("roster.v0.1.json"),
            Pick("quest-catalogue.v0.1.json"), Pick("storylets.v0.1.json"));
    }

    private static GameData Load(string packed)
    {
        var p = packed.Split('\u0001');
        return GameDataLoader.Load(p[0], p[1], p[2], p[3], p[4]);
    }

    [Fact]
    public void Unknown_hero_probability_out_of_range_and_bad_expiry_are_rejected()
    {
        Assert.Throws<GameDataException>(() => Load(Replace("storylets.v0.1.json", "\"gerard-dev\"", "\"personne\"")));
        Assert.Throws<GameDataException>(() => Load(Replace("storylets.v0.1.json", "\"probabilityPercent\": 40", "\"probabilityPercent\": 101")));
        Assert.Throws<GameDataException>(() => Load(Replace("storylets.v0.1.json", "\"complaintExpiryDays\": 2", "\"complaintExpiryDays\": 0")));
        Assert.Throws<GameDataException>(() => Load(Replace("storylets.v0.1.json", "\"wifi-du-dragon\"", "\"modele-fantome\"")));
    }
}

public class StorySelectionTests
{
    private static MissionContext Gerard(bool success) =>
        new("gerard-dev", Data.Templates.Single(t => t.Id == "rats-de-la-cave"), success);

    [Fact]
    public void Conditions_are_matched_on_hero_outcome_and_category()
    {
        var e01 = Data.Storylets.Single(s => s.Id == "E01");
        Assert.True(StoryletSelector.Matches(e01, Gerard(true)));
        Assert.False(StoryletSelector.Matches(e01, Gerard(false)));
        Assert.False(StoryletSelector.Matches(e01, Gerard(true) with { HeroId = "hermine-secops" }));
        Assert.False(StoryletSelector.Matches(e01, Gerard(true) with { Template = Data.Templates.Single(t => t.Id == "bandits-de-la-route") }));
    }

    [Fact]
    public void Probability_boundary_is_strictly_below_the_percentage()
    {
        Assert.NotNull(StoryletSelector.Select(Data.Storylets, Gerard(true), new ScriptedRandom(39)));
        Assert.Null(StoryletSelector.Select(Data.Storylets, Gerard(true), new ScriptedRandom(40)));
    }

    [Fact]
    public void No_eligible_storylet_consumes_no_randomness()
    {
        var rng = new ScriptedRandom(0);
        Assert.Null(StoryletSelector.Select(Data.Storylets, Gerard(false), rng));
        Assert.Equal(0, rng.Calls);
    }

    [Fact]
    public void At_most_one_storylet_per_mission_highest_priority_first()
    {
        var always = new Storylet("E99", "Toujours", "x", Priority: 99, ProbabilityPercent: 100, new StoryletConditions(OutcomeFilter.Any, [], [], []), 10, new ChoiceLabels("a", "b", "c"));
        var also = always with { Id = "E98", Priority = 5 };
        var chosen = StoryletSelector.Select([also, always], Gerard(true), new ScriptedRandom(0, 0));
        Assert.Equal("E99", chosen!.Id);
    }

    [Fact]
    public void A_new_storylet_is_added_by_data_only()
    {
        var extra = new Storylet("E50", "Nouveau", "texte", 1, 100, new StoryletConditions(OutcomeFilter.Success, ["hermine-secops"], [], []), 33, new ChoiceLabels("r", "a", "d"));
        var data = Data with { Storylets = [.. Data.Storylets, extra] };
        var s = GameSession.Start(data, Seed, traits: new NoTraitEffects());
        var offer = Inject(s, "bandits-de-la-route");
        s.Assign(offer.Id, "hermine-secops");
        // le moteur est inchangé : seul le catalogue a un élément de plus
        var outcome = s.Resolve("q1");
        Assert.True(outcome.Ok);
    }
}

public class ComplaintFlowTests
{
    /// <summary>Gérard réussit "Les rats de la cave" (tirage 0) puis E01 se déclenche (tirage 0 &lt; 40).</summary>
    private static (GameSession S, MissionOutcome O) GerardIncident(int storyletRoll = 0)
    {
        var s = NewSessionWithRolls(0, storyletRoll);
        var offer = Inject(s, "rats-de-la-cave");
        Assert.True(s.Assign(offer.Id, "gerard-dev").Ok);
        return (s, s.Resolve("q1").Value!);
    }

    /// <summary>Jean-Michel échoue "Wi-Fi du dragon" (tirage 99) puis E02 se déclenche.</summary>
    private static (GameSession S, MissionOutcome O) DragonIncident()
    {
        var s = NewSessionWithRolls(99, 0);
        var offer = Inject(s, "wifi-du-dragon");
        Assert.True(s.Assign(offer.Id, "jean-michel-sre").Ok);
        return (s, s.Resolve("q1").Value!);
    }

    [Fact]
    public void Success_incident_pays_the_reward_applies_collateral_damage_and_opens_one_complaint()
    {
        var (s, o) = GerardIncident();
        Assert.True(o.Success);
        Assert.Equal(25 + 0, o.RewardPaidCopper);        // rats-de-la-cave : 25 cuivres
        Assert.Equal(39, o.ReputationAfter);              // +4 de succès...
        Assert.Equal("E01", o.Incident!.StoryletId);
        Assert.Equal(-4, o.Incident.CollateralReputationDelta);
        Assert.Equal(35, s.Guild.Reputation);             // ...puis -4 de dommage collatéral
        Assert.Single(s.OpenComplaints);
        Assert.Equal(80, s.OpenComplaints[0].RepairCostCopper);
        Assert.Equal(QuestState.Succeeded, s.Quests[0].State);
        Assert.Equal(1, s.Guild.MissionsSucceeded);
    }

    [Fact]
    public void Failure_incident_stacks_failure_and_collateral_damage()
    {
        var (s, o) = DragonIncident();
        Assert.False(o.Success);
        Assert.Equal("E02", o.Incident!.StoryletId);
        Assert.Equal(35 - 3 - 4, s.Guild.Reputation);
        Assert.Equal(120, s.OpenComplaints.Single().RepairCostCopper);
    }

    [Fact]
    public void No_incident_when_the_probability_roll_misses()
    {
        var (s, o) = GerardIncident(storyletRoll: 40);
        Assert.Null(o.Incident);
        Assert.Empty(s.Complaints);
        Assert.Equal(39, s.Guild.Reputation);
    }

    [Fact]
    public void Repair_debits_once_and_gains_reputation_once_T17_T18()
    {
        var (s, _) = GerardIncident();
        var treasury = s.Guild.TreasuryCopper;
        var r = s.ResolveComplaint("c1", ComplaintChoice.Repair);
        Assert.True(r.Ok);
        Assert.Equal(treasury - 80, s.Guild.TreasuryCopper);
        Assert.Equal(38, s.Guild.Reputation);             // +3
        Assert.Equal(ComplaintState.Resolved, s.Complaints[0].State);
        Assert.Equal(QuestState.Succeeded, s.Quests[0].State);   // la mission n'est jamais requalifiée

        var snap = Snapshot(s);
        var again = s.ResolveComplaint("c1", ComplaintChoice.Repair);      // double clic
        Assert.False(again.Ok);
        Assert.Contains(RefusalReason.ComplaintNotOpen, again.Reasons);
        Assert.Equal(snap, Snapshot(s));
        Assert.False(s.ResolveComplaint("c1", ComplaintChoice.Deny).Ok);     // pas de deuxième choix non plus
    }

    [Theory]
    [InlineData(ComplaintChoice.Repair, 80, 3)]
    [InlineData(ComplaintChoice.Apologize, 0, 1)]
    [InlineData(ComplaintChoice.Deny, 0, -2)]
    public void Three_choices_give_three_different_money_and_reputation_balances_T17(ComplaintChoice choice, int cost, int delta)
    {
        var (s, _) = GerardIncident();
        var treasury = s.Guild.TreasuryCopper;
        var reputation = s.Guild.Reputation;
        var r = s.ResolveComplaint("c1", choice).Value!;
        Assert.Equal(cost, r.CostPaidCopper);
        Assert.Equal(treasury - cost, s.Guild.TreasuryCopper);
        Assert.Equal(reputation + delta, s.Guild.Reputation);
        Assert.Equal(delta, r.ReputationDelta);
    }

    [Fact]
    public void Unaffordable_repair_is_refused_without_mutation_and_free_options_remain_T19()
    {
        var (s, _) = GerardIncident();
        s.Guild.Debit(s.Guild.TreasuryCopper - 79);
        var snap = Snapshot(s);
        var r = s.ResolveComplaint("c1", ComplaintChoice.Repair);
        Assert.Contains(RefusalReason.NotEnoughTreasury, r.Reasons);
        Assert.Equal(snap, Snapshot(s));
        Assert.Contains(RefusalReason.NotEnoughTreasury, s.ComplaintCheck("c1", ComplaintChoice.Repair));
        Assert.Empty(s.ComplaintCheck("c1", ComplaintChoice.Apologize));
        Assert.Empty(s.ComplaintCheck("c1", ComplaintChoice.Deny));
        Assert.True(s.ResolveComplaint("c1", ComplaintChoice.Apologize).Ok);
        Assert.Equal(79, s.Guild.TreasuryCopper);
    }

    [Fact]
    public void Treasury_exactly_equal_to_the_repair_cost_is_enough()
    {
        var (s, _) = GerardIncident();
        s.Guild.Debit(s.Guild.TreasuryCopper - 80);
        Assert.True(s.ResolveComplaint("c1", ComplaintChoice.Repair).Ok);
        Assert.Equal(0, s.Guild.TreasuryCopper);
    }

    [Fact]
    public void Reputation_stays_within_bounds_when_settling_a_complaint()
    {
        var (s, _) = GerardIncident();
        s.Guild.ApplyReputation(100, 0, 100);
        Assert.Equal(100, s.ResolveComplaint("c1", ComplaintChoice.Repair).Value!.ReputationAfter);

        var (f, _) = GerardIncident();
        f.Guild.ApplyReputation(-100, 0, 100);
        Assert.Equal(0, f.ResolveComplaint("c1", ComplaintChoice.Deny).Value!.ReputationAfter);
    }

    [Fact]
    public void Unknown_complaint_is_refused_and_reputation_cannot_be_bought_without_one()
    {
        var s = NewSession();
        var snap = Snapshot(s);
        Assert.Contains(RefusalReason.UnknownComplaint, s.ResolveComplaint("c1", ComplaintChoice.Repair).Reasons);
        Assert.Equal(snap, Snapshot(s));
    }

    [Fact]
    public void An_open_complaint_does_not_block_ending_the_day()
    {
        var (s, _) = GerardIncident();
        Assert.True(s.EndDay().Ok);
        Assert.Equal(2, s.Guild.Day);
        Assert.Single(s.OpenComplaints);
        // et elle reste traitable le lendemain
        Assert.True(s.ResolveComplaint("c1", ComplaintChoice.Apologize).Ok);
    }

    [Fact]
    public void Complaint_expires_after_two_days_as_a_denial_and_before_the_next_offers_are_drawn()
    {
        var (s, _) = GerardIncident();                 // ouverte le jour 1, ExpiresAfterDay = 3
        Assert.Equal(3, s.Complaints[0].ExpiresAfterDay);
        s.EndDay();                                    // fin du jour 1
        s.EndDay();                                    // fin du jour 2
        Assert.Equal(ComplaintState.Open, s.Complaints[0].State);
        var before = s.Guild.Reputation;
        s.EndDay();                                    // fin du jour 3 : expire
        Assert.Equal(ComplaintState.Expired, s.Complaints[0].State);
        Assert.Equal(before - 2, s.Guild.Reputation);
        Assert.Contains(s.Events, e => e.Type == GameEventType.ComplaintExpired);
        var snap = Snapshot(s);
        Assert.Contains(RefusalReason.ComplaintNotOpen, s.ResolveComplaint("c1", ComplaintChoice.Repair).Reasons);
        Assert.Equal(snap, Snapshot(s));
    }

    [Fact]
    public void Complaints_settled_in_time_never_expire()
    {
        var (s, _) = GerardIncident();
        s.ResolveComplaint("c1", ComplaintChoice.Apologize);
        var rep = s.Guild.Reputation;
        s.EndDay(); s.EndDay(); s.EndDay();
        Assert.Equal(rep, s.Guild.Reputation);
        Assert.Equal(ComplaintState.Resolved, s.Complaints[0].State);
    }

    [Fact]
    public void Expiry_uses_the_clamped_reputation_scale()
    {
        var (s, _) = GerardIncident();
        s.EndDay(); s.EndDay();
        s.Guild.ApplyReputation(-100, 0, 100);
        s.EndDay();
        Assert.Equal(0, s.Guild.Reputation);
    }

    [Fact]
    public void A_complaint_opened_late_is_listed_open_at_the_assessment_without_effect()
    {
        var s = NewSessionDeferred(out var rng);
        for (var d = 1; d < 6; d++) s.EndDay();
        rng.Override = new ScriptedRandom(0, 0);          // à partir d'ici : succès puis E01
        var offer = Inject(s, "rats-de-la-cave");
        s.Assign(offer.Id, "gerard-dev");
        s.Resolve("q1");
        Assert.Single(s.OpenComplaints);               // ouverte le jour 6, expirerait fin du jour 8
        s.EndDay();
        var reputation = s.Guild.Reputation;
        var report = s.EndDay().Value!.Report!;
        Assert.Equal(1, report.OpenComplaints);
        Assert.Contains("Réclamations encore ouvertes", report.Message);
        Assert.Equal(reputation, s.Guild.Reputation);
    }

    [Fact]
    public void No_complaint_command_works_after_the_game_is_finished()
    {
        var (s, _) = GerardIncident();
        Bots.Idle(s);
        Assert.Contains(RefusalReason.GameFinished, s.ResolveComplaint("c1", ComplaintChoice.Deny).Reasons);
    }

    [Fact]
    public void Reset_clears_complaints()
    {
        var (s, _) = GerardIncident();
        s.Reset();
        Assert.Empty(s.Complaints);
        Assert.Empty(s.OpenComplaints);
    }

    [Fact]
    public void Storylet_draw_is_taken_only_when_a_storylet_is_eligible()
    {
        var s = NewSessionWithScript(out var script, 0);
        var offer = Inject(s, "chapeau-du-maire");     // corvée de combat avec... un autre héros que Gérard
        s.Assign(offer.Id, "hermine-secops");
        var callsBefore = script.Calls;
        s.Resolve("q1");
        Assert.Equal(callsBefore + 1, script.Calls);   // seulement le tirage de la mission
    }

    [Fact]
    public void Incidents_rewarded_by_real_seeded_play_keep_every_invariant_and_replay_identically()
    {
        var incidents = 0;
        string Play(long seed)
        {
            var s = NewSession(seed);
            Bots.Greedy(s, x =>
            {
                Bots.AssertInvariants(x);
                foreach (var c in x.OpenComplaints.ToList())
                {
                    var choice = x.ComplaintCheck(c.Id, ComplaintChoice.Repair).Count == 0 ? ComplaintChoice.Repair : ComplaintChoice.Apologize;
                    Assert.True(x.ResolveComplaint(c.Id, choice).Ok);
                    Bots.AssertInvariants(x);
                }
            });
            incidents += s.Complaints.Count;
            return Snapshot(s) + string.Join("/", s.Events.Select(e => e.Message));
        }

        for (var seed = 0; seed < 150; seed++) Assert.Equal(Play(seed), Play(seed));
        Assert.True(incidents > 0, "Aucun incident sur 150 graines : le chemin n'est pas exercé.");
    }
}
