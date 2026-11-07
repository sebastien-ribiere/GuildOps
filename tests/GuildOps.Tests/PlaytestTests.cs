using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using static GuildOps.Tests.TestSupport;

namespace GuildOps.Tests;

/// <summary>
/// T27 — parties de référence sur sept jours, rejouables à l'identique : un scénario « victoire » et un scénario « échec »
/// sur la MÊME graine officielle de test (<c>testSeed</c> des règles), qui ne diffèrent que par la réponse aux réclamations.
/// Les valeurs ci-dessous sont des mesures de la baseline actuelle : si une PR d'équilibrage les change, ce test le dit.
/// </summary>
public class PlaytestTests
{
    private static GameSession Play(long seed, ComplaintPolicy policy)
    {
        var s = GameSession.Start(Data, seed);
        Bots.Greedy(s, Bots.AssertInvariants, policy);
        return s;
    }

    [Fact]
    public void Reference_seed_is_the_contract_test_seed()
    {
        Assert.Equal(1142026, Data.Rules.TestSeed);
    }

    [Fact]
    public void Winning_playthrough_repairs_what_it_can_and_meets_all_three_conditions()
    {
        var s = Play(Seed, ComplaintPolicy.RepairIfPossible);
        var r = s.Report!;

        Assert.Equal(GameStatus.Finished, s.Status);
        Assert.True(r.Success);
        Assert.True(r.ReputationOk && r.TreasuryOk && r.MissionsOk);
        Assert.True(r.Reputation >= 55 && r.TreasuryCopper >= 1000 && r.MissionsSucceeded >= 8);
        Assert.Equal((64, 1707, 15), (r.Reputation, r.TreasuryCopper, r.MissionsSucceeded));
        Assert.Equal(5, s.Complaints.Count);
        Assert.Equal(0, r.OpenComplaints);
    }

    [Fact]
    public void Losing_playthrough_ignores_every_complaint_and_misses_the_reputation_target_only()
    {
        var s = Play(Seed, ComplaintPolicy.Ignore);
        var r = s.Report!;

        Assert.Equal(GameStatus.Finished, s.Status);
        Assert.False(r.Success);
        Assert.False(r.ReputationOk);
        Assert.True(r.TreasuryOk && r.MissionsOk);                       // la perte vient uniquement de la réputation
        Assert.Equal((37, 1397, 15), (r.Reputation, r.TreasuryCopper, r.MissionsSucceeded));
        // aucune réponse donnée : les réclamations expirent, sauf celles ouvertes trop tard pour expirer avant le bilan
        Assert.DoesNotContain(s.Complaints, c => c.State == ComplaintState.Resolved);
        Assert.Contains(s.Complaints, c => c.State == ComplaintState.Expired);
        Assert.StartsWith("Bilan inachevé", r.Message);                  // pas un game-over : on peut recommencer
    }

    [Fact]
    public void Apologising_everywhere_misses_the_goal_by_one_reputation_point()
    {
        var r = Play(Seed, ComplaintPolicy.Apologize).Report!;
        Assert.False(r.Success);
        Assert.Equal(54, r.Reputation);        // seuil inclusif à 55 : un point de moins
        Assert.True(r.TreasuryOk && r.MissionsOk);
    }

    [Fact]
    public void Reference_playthroughs_are_reproducible_to_the_last_event()
    {
        foreach (var policy in Enum.GetValues<ComplaintPolicy>())
        {
            var a = Play(Seed, policy);
            var b = Play(Seed, policy);
            Assert.Equal(a.Events.Select(e => e.Message), b.Events.Select(e => e.Message));
            Assert.Equal(Snapshot(a), Snapshot(b));
        }
    }

    [Fact]
    public void Doing_nothing_for_seven_days_loses_without_any_error()
    {
        var s = NewSession();
        Bots.Idle(s);
        Assert.False(s.Report!.Success);
        Assert.False(s.Report.MissionsOk);
        Assert.Equal(0, s.Report.MissionsSucceeded);
    }

    [Fact]
    public void The_complaint_decision_alone_flips_the_outcome_on_the_same_seed()
    {
        Assert.False(Play(Seed, ComplaintPolicy.Ignore).Report!.Success);
        Assert.True(Play(Seed, ComplaintPolicy.RepairIfPossible).Report!.Success);
    }
}
