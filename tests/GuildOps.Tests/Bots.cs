using GuildOps.Core.Domain;
using GuildOps.Core.Engine;

namespace GuildOps.Tests;

/// <summary>Joueurs automatiques déterministes, pour tester des parties entières sans UI.</summary>
internal static class Bots
{
    /// <summary>Ne fait jamais rien : passe les 7 jours.</summary>
    public static void Idle(GameSession s)
    {
        while (s.Status == GameStatus.InProgress) Assert.True(s.EndDay().Ok);
    }

    /// <summary>
    /// Glouton : à chaque jour, engage jusqu'à 3 missions en maximisant (chance × prime − engagement), résout, puis termine la journée.
    /// <paramref name="afterStep"/> est appelé après chaque commande pour vérifier des invariants.
    /// </summary>
    public static void Greedy(GameSession s, Action<GameSession>? afterStep = null)
    {
        while (s.Status == GameStatus.InProgress)
        {
            for (var i = 0; i < s.Data.Rules.MaxMissionsPerDay; i++)
            {
                var best = s.CurrentOffers.Where(o => o.State == OfferState.Available)
                    .SelectMany(o => s.Heroes.Select(h => (Offer: o, Hero: h, Check: s.Preview(o.Id, h.Id))))
                    .Where(x => x.Check.CanAssign)
                    .OrderByDescending(x => x.Check.Chance!.Final * x.Offer.Template.RewardCopper - 100 * x.Check.EngagementCostCopper)
                    .ThenBy(x => x.Offer.Id, StringComparer.Ordinal).ThenBy(x => x.Hero.Id, StringComparer.Ordinal)
                    .Cast<(QuestOffer Offer, Hero Hero, AssignmentCheck Check)?>().FirstOrDefault();
                if (best is null) break;
                var assigned = s.Assign(best.Value.Offer.Id, best.Value.Hero.Id);
                Assert.True(assigned.Ok);
                afterStep?.Invoke(s);
                Assert.True(s.Resolve(assigned.Value!.Id).Ok);
                afterStep?.Invoke(s);
            }
            Assert.True(s.EndDay().Ok);
            afterStep?.Invoke(s);
        }
    }

    public static void AssertInvariants(GameSession s)
    {
        var r = s.Data.Rules;
        Assert.True(s.Guild.TreasuryCopper >= 0);
        Assert.InRange(s.Guild.Reputation, r.ReputationMin, r.ReputationMax);
        Assert.InRange(s.Guild.Day, 1, r.Days);
        Assert.InRange(s.Guild.MissionsAcceptedToday, 0, r.MaxMissionsPerDay);
        Assert.All(s.Heroes, h => Assert.InRange(h.Energy, 0, r.MaxEnergy));
        Assert.All(s.Heroes, h => Assert.Equal(h.AssignedQuestId is null, h.IsFree));
        Assert.True(s.Guild.MissionsSucceeded <= s.Quests.Count(q => q.State == QuestState.Succeeded));
    }
}
