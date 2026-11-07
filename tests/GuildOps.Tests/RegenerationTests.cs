using GuildOps.Core.Data;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using static GuildOps.Tests.TestSupport;

namespace GuildOps.Tests;

public class RegenerationTests
{
    [Fact]
    public void Regeneration_replaces_the_board_costs_two_reputation_and_keeps_history()
    {
        var s = NewSession();
        var old = s.CurrentOffers.Select(o => o.Id).ToList();
        var treasury = s.Guild.TreasuryCopper;

        var r = s.RegenerateOffers();

        Assert.True(r.Ok);
        Assert.Equal(35, r.Value!.ReputationBefore);
        Assert.Equal(33, r.Value.ReputationAfter);
        Assert.Equal(33, s.Guild.Reputation);
        Assert.Equal(treasury, s.Guild.TreasuryCopper);
        Assert.Equal(1, s.Guild.Day);
        Assert.Equal(4, s.CurrentOffers.Count);
        Assert.All(s.CurrentOffers, o => Assert.StartsWith("d1r-", o.Id));
        Assert.Equal(s.CurrentOffers.Select(o => o.Id), r.Value.NewOffers.Select(o => o.Id));
        // Les offres remplacées restent dans l'historique, expirées et inchangées.
        Assert.Equal(old, s.Offers.Where(o => o.Day == 1 && !o.Id.StartsWith("d1r-")).Select(o => o.Id));
        Assert.All(s.Offers.Where(o => old.Contains(o.Id)), o => Assert.Equal(OfferState.Expired, o.State));
        Assert.True(s.RegenerationUsed);
        Assert.Contains(s.Events, e => e.Type == GameEventType.OffersRegenerated);
    }

    [Fact]
    public void A_second_regeneration_is_refused_without_mutation_double_click()
    {
        var s = NewSession();
        s.RegenerateOffers();
        var snap = Snapshot(s);
        var r = s.RegenerateOffers();
        Assert.False(r.Ok);
        Assert.Contains(RefusalReason.RegenerationAlreadyUsed, r.Reasons);
        Assert.Equal(snap, Snapshot(s));
    }

    [Fact]
    public void Regeneration_is_refused_once_a_mission_is_engaged_today()
    {
        var s = NewSession();
        var (o, h) = FindAssignable(s);
        s.Assign(o, h);
        var snap = Snapshot(s);
        var r = s.RegenerateOffers();
        Assert.Contains(RefusalReason.MissionsAlreadyEngaged, r.Reasons);
        Assert.Equal(snap, Snapshot(s));
        Assert.False(s.RegenerationUsed);   // un refus ne consomme pas le droit
    }

    [Fact]
    public void It_can_be_used_on_any_day_but_only_once_per_game()
    {
        var s = NewSession();
        s.EndDay(); s.EndDay();
        Assert.Equal(3, s.Guild.Day);
        Assert.True(s.RegenerateOffers().Ok);
        Assert.All(s.CurrentOffers, o => Assert.StartsWith("d3r-", o.Id));
        s.EndDay();
        Assert.Equal(4, s.Guild.Day);
        Assert.Contains(RefusalReason.RegenerationAlreadyUsed, s.RegenerateOffers().Reasons);
        Assert.All(s.CurrentOffers, o => Assert.StartsWith("d4-", o.Id));
    }

    [Fact]
    public void Reset_gives_the_right_back()
    {
        var s = NewSession();
        s.RegenerateOffers();
        s.Reset();
        Assert.False(s.RegenerationUsed);
        Assert.Empty(s.RegenerationCheck());
    }

    [Fact]
    public void Regeneration_is_refused_after_the_game_is_finished()
    {
        var s = NewSession();
        Bots.Idle(s);
        var snap = Snapshot(s);
        Assert.Contains(RefusalReason.GameFinished, s.RegenerateOffers().Reasons);
        Assert.Equal(snap, Snapshot(s));
    }

    [Fact]
    public void New_board_uses_the_reputation_after_the_cost_and_keeps_medor_guarantee()
    {
        for (var seed = 0; seed < 60; seed++)
        {
            var s = NewSession(seed);
            s.Guild.ApplyReputation(41 - s.Guild.Reputation, 0, 100);   // 41 : pas de garantie ; 39 après coût : garantie
            Assert.True(s.RegenerateOffers().Ok);
            Assert.Equal(39, s.Guild.Reputation);
            Assert.Equal(4, s.CurrentOffers.Count);
            Assert.Contains(s.CurrentOffers, o => o.Template.Id == "promener-medor");
        }
    }

    [Fact]
    public void Cost_is_clamped_at_zero_reputation()
    {
        var s = NewSession();
        s.Guild.ApplyReputation(-35, 0, 100);
        var r = s.RegenerateOffers();
        Assert.True(r.Ok);
        Assert.Equal(0, s.Guild.Reputation);
        Assert.Contains(s.CurrentOffers, o => o.Template.Id == "promener-medor");
    }

    [Fact]
    public void Regeneration_is_deterministic_for_the_same_commands()
    {
        string Run(long seed)
        {
            var s = NewSession(seed);
            s.RegenerateOffers();
            return string.Join(",", s.CurrentOffers.Select(o => o.Template.Id));
        }
        Assert.Equal(Run(Seed), Run(Seed));
        Assert.Contains(Enumerable.Range(0, 30), seed => Run(seed) != string.Join(",", NewSession(seed).CurrentOffers.Select(o => o.Template.Id)));
    }

    [Fact]
    public void Preview_of_an_expired_replaced_offer_is_refused()
    {
        var s = NewSession();
        var oldId = s.CurrentOffers[0].Id;
        s.RegenerateOffers();
        Assert.Contains(RefusalReason.OfferNotAvailable, s.Preview(oldId, s.Heroes[0].Id).Reasons);
    }

    [Fact]
    public void Disabled_flag_refuses_regeneration()
    {
        var dir = DataTests.FindGameData();
        var rules = File.ReadAllText(Path.Combine(dir, "rules.v0.1.json")).Replace("\"regenerateDayOnce\": true", "\"regenerateDayOnce\": false");
        var data = GameDataLoader.Load(rules,
            File.ReadAllText(Path.Combine(dir, "hero-balancing.v0.1.json")),
            File.ReadAllText(Path.Combine(dir, "roster.v0.1.json")),
            File.ReadAllText(Path.Combine(dir, "quest-catalogue.v0.1.json")));
        var s = GameSession.Start(data, Seed);
        Assert.Contains(RefusalReason.RegenerationDisabled, s.RegenerateOffers().Reasons);
    }

    [Fact]
    public void Missing_cost_field_is_rejected_at_load()
    {
        var dir = DataTests.FindGameData();
        var rules = File.ReadAllText(Path.Combine(dir, "rules.v0.1.json")).Replace("\"regenerateDayReputationCost\": 2,", "");
        Assert.Throws<GameDataException>(() => GameDataLoader.Load(rules,
            File.ReadAllText(Path.Combine(dir, "hero-balancing.v0.1.json")),
            File.ReadAllText(Path.Combine(dir, "roster.v0.1.json")),
            File.ReadAllText(Path.Combine(dir, "quest-catalogue.v0.1.json"))));
    }

    [Fact]
    public void Greedy_play_with_a_regeneration_keeps_invariants()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var s = NewSession(seed);
            s.RegenerateOffers();
            Bots.Greedy(s, Bots.AssertInvariants);
            Assert.Equal(GameStatus.Finished, s.Status);
        }
    }
}
