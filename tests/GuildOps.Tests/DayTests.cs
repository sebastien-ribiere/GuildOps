using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using static GuildOps.Tests.TestSupport;

namespace GuildOps.Tests;

public class DayProgressionTests
{
    [Fact]
    public void Day_one_has_no_energy_regeneration_and_day_two_recovers_30_capped_at_100_T22()
    {
        var s = NewSession();
        var before = s.Heroes.ToDictionary(h => h.Id, h => h.Energy);
        Assert.True(s.EndDay().Ok);
        Assert.Equal(2, s.Guild.Day);
        Assert.All(s.Heroes, h => Assert.Equal(Math.Min(100, before[h.Id] + 30), h.Energy));
        Assert.Contains(s.Heroes, h => before[h.Id] + 30 > 100);   // le plafond est réellement exercé
    }

    [Fact]
    public void Spent_energy_is_recovered_by_exactly_30()
    {
        var s = NewSessionWithRolls(0);
        var (o, h) = FindAssignable(s);
        s.Assign(o, h);
        s.Resolve("q1");
        var hero = s.Heroes.First(x => x.Id == h);
        hero.Energy = 40;
        s.EndDay();
        Assert.Equal(70, hero.Energy);
    }

    [Fact]
    public void Ending_the_day_resets_the_daily_counter_and_publishes_four_new_offers()
    {
        var s = NewSession();
        var (o, h) = FindAssignable(s);
        s.Assign(o, h);
        s.Resolve("q1");
        Assert.Equal(1, s.Guild.MissionsAcceptedToday);
        var t = s.EndDay().Value!;
        Assert.Equal(0, s.Guild.MissionsAcceptedToday);
        Assert.Equal(4, t.NewOffers.Count);
        Assert.All(t.NewOffers, x => Assert.StartsWith("d2-", x.Id));
        Assert.Equal(t.NewOffers.Select(x => x.Id), s.CurrentOffers.Select(x => x.Id));
        Assert.Equal(2, t.NewDay);
    }

    [Fact]
    public void Unaccepted_offers_expire_and_history_is_kept_unchanged()
    {
        var s = NewSession();
        var (o, h) = FindAssignable(s);
        s.Assign(o, h);
        s.Resolve("q1");
        var day1 = s.CurrentOffers.Select(x => (x.Id, x.Template.Id)).ToList();
        s.EndDay();
        var history = s.Offers.Where(x => x.Day == 1).ToList();
        Assert.Equal(day1, history.Select(x => (x.Id, x.Template.Id)));
        Assert.Equal(OfferState.Accepted, history.Single(x => x.Id == o).State);
        Assert.All(history.Where(x => x.Id != o), x => Assert.Equal(OfferState.Expired, x.State));
        // Une offre expirée ne peut plus être acceptée.
        var expired = history.First(x => x.State == OfferState.Expired);
        Assert.Contains(RefusalReason.OfferNotAvailable, s.Preview(expired.Id, s.Heroes[0].Id).Reasons);
    }

    [Fact]
    public void Ending_the_day_with_a_pending_mission_is_refused_without_mutation()
    {
        var s = NewSession();
        var (o, h) = FindAssignable(s);
        s.Assign(o, h);
        var snap = Snapshot(s);
        var r = s.EndDay();
        Assert.False(r.Ok);
        Assert.Contains(RefusalReason.PendingMissions, r.Reasons);
        Assert.Equal(snap, Snapshot(s));
        Assert.Equal(1, s.Guild.Day);
    }

    [Fact]
    public void Daily_limit_is_lifted_on_the_next_day()
    {
        var s = NewSession();
        for (var i = 1; i <= 3; i++)
        {
            var (o, h) = FindAssignable(s);
            s.Assign(o, h);
            s.Resolve($"q{i}");
        }
        Assert.Equal(3, s.Guild.MissionsAcceptedToday);
        s.EndDay();
        var (o2, h2) = FindAssignable(s);
        Assert.True(s.Assign(o2, h2).Ok);
    }

    [Fact]
    public void New_offers_follow_the_current_reputation_not_the_old_one_T14_T20()
    {
        int HighTierCount(int reputation)
        {
            var n = 0;
            for (var seed = 0; seed < 60; seed++)
            {
                var s = NewSession(seed);
                s.Guild.ApplyReputation(reputation - s.Guild.Reputation, 0, 100);
                var published = s.CurrentOffers.Select(x => x.Id).ToList();
                s.EndDay();
                // Les offres déjà publiées ne bougent pas.
                Assert.Equal(published, s.Offers.Where(x => x.Day == 1).Select(x => x.Id));
                n += s.CurrentOffers.Count(x => x.Template.Category is QuestCategory.Notable or QuestCategory.Royale);
            }
            return n;
        }
        Assert.Equal(0, HighTierCount(5));
        Assert.True(HighTierCount(95) > 40);
    }

    [Fact]
    public void Day_two_still_guarantees_medor_for_a_low_reputation_guild()
    {
        for (var seed = 0; seed < 100; seed++)
        {
            var s = NewSession(seed);
            s.EndDay();
            Assert.Equal(4, s.CurrentOffers.Count);
            Assert.Contains(s.CurrentOffers, o => o.Template.Id == "promener-medor");
        }
    }

    [Fact]
    public void Same_seed_and_same_commands_give_the_same_week()
    {
        string Week()
        {
            var s = NewSession();
            Bots.Greedy(s);
            return Snapshot(s) + string.Join("/", s.Events.Select(e => e.Message));
        }
        Assert.Equal(Week(), Week());
    }
}

public class FinalAssessmentTests
{
    private static GameSession AtLastDay(int reputation, int treasury, int succeeded)
    {
        var s = NewSession();
        for (var d = 1; d < s.Data.Rules.Days; d++) Assert.True(s.EndDay().Ok);
        Assert.Equal(7, s.Guild.Day);
        s.Guild.ApplyReputation(reputation - s.Guild.Reputation, 0, 100);
        s.Guild.Debit(s.Guild.TreasuryCopper);
        s.Guild.Credit(treasury);
        s.Guild.MissionsSucceeded = succeeded;
        return s;
    }

    [Theory]
    [InlineData(55, 1000, 8, true)]    // exactement aux seuils
    [InlineData(54, 1000, 8, false)]
    [InlineData(55, 999, 8, false)]
    [InlineData(55, 1000, 7, false)]
    [InlineData(100, 5000, 21, true)]
    [InlineData(0, 0, 0, false)]
    public void Assessment_thresholds_are_inclusive(int rep, int treasury, int succeeded, bool expected)
    {
        var s = AtLastDay(rep, treasury, succeeded);
        var r = s.EndDay();
        Assert.True(r.Ok);
        Assert.Equal(GameStatus.Finished, s.Status);
        Assert.Equal(expected, r.Value!.Report!.Success);
        Assert.Same(r.Value.Report, s.Report);
        Assert.Null(r.Value.NewDay);
        Assert.Equal(7, s.Guild.Day);
    }

    [Fact]
    public void Report_says_which_criteria_failed()
    {
        var s = AtLastDay(54, 1000, 7);
        var report = s.EndDay().Value!.Report!;
        Assert.False(report.ReputationOk);
        Assert.True(report.TreasuryOk);
        Assert.False(report.MissionsOk);
        Assert.Contains("inachevé", report.Message);
    }

    [Fact]
    public void After_the_assessment_every_command_is_refused_and_state_is_preserved()
    {
        var s = AtLastDay(10, 100, 1);
        s.EndDay();
        var snap = Snapshot(s);
        Assert.Contains(RefusalReason.GameFinished, s.EndDay().Reasons);
        Assert.Contains(RefusalReason.GameFinished, s.Assign(s.Offers[0].Id, s.Heroes[0].Id).Reasons);
        Assert.Contains(RefusalReason.GameFinished, s.Resolve("q1").Reasons);
        Assert.Contains(RefusalReason.GameFinished, s.Preview(s.Offers[0].Id, s.Heroes[0].Id).Reasons);
        Assert.Equal(snap, Snapshot(s));
    }

    [Fact]
    public void An_unfinished_week_is_not_a_game_over_and_can_be_restarted()
    {
        var s = NewSession();
        var initial = Snapshot(s);
        Bots.Idle(s);
        Assert.Equal(GameStatus.Finished, s.Status);
        Assert.False(s.Report!.Success);
        s.Reset();
        Assert.Equal(GameStatus.InProgress, s.Status);
        Assert.Null(s.Report);
        Assert.Equal(initial, Snapshot(s));
    }

    [Fact]
    public void Idle_play_fails_the_assessment_with_no_changes_to_the_economy()
    {
        var s = NewSession();
        Bots.Idle(s);
        Assert.Equal(1200, s.Guild.TreasuryCopper);
        Assert.Equal(35, s.Guild.Reputation);
        Assert.False(s.Report!.MissionsOk);
    }
}

public class SimulatedWeekTests
{
    [Fact]
    public void Greedy_bot_keeps_every_invariant_over_many_seeds()
    {
        for (var seed = 0; seed < 150; seed++)
        {
            var s = NewSession(seed);
            Bots.Greedy(s, Bots.AssertInvariants);
            Assert.Equal(GameStatus.Finished, s.Status);
            Assert.NotNull(s.Report);
            Assert.Equal(7, s.Guild.Day);
        }
    }
}
