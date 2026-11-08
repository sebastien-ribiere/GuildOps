using GuildOps.Core.Domain;
using GuildOps.Core.Engine;

namespace GuildOps.Tests;

/// <summary>
/// Parité UI/Core (phase B5) : la même politique déterministe que le scénario <c>--autoplay</c> de GuildOps.Game
/// (qui passe par les boutons de l'UI) doit mener Core au même état final. Le Godot affiche
/// « AUTOPLAY_RESULT day=7 treasury=2077 reputation=87 succeeded=18 events=113 status=Finished success=True ».
/// </summary>
public class UiParityTests
{
    [Fact]
    public void Politique_autoplay_sur_Core_seul_donne_l_etat_final_observe_dans_l_UI()
    {
        var s = GameSession.Start(TestSupport.Data, TestSupport.Data.Rules.TestSeed);
        while (s.Status == GameStatus.InProgress)
        {
            for (var n = 0; n < s.Data.Rules.MaxMissionsPerDay; n++)
            {
                var pick = s.CurrentOffers.Where(o => o.State == OfferState.Available)
                    .SelectMany(o => s.Heroes.Select(h => (offer: o, hero: h)))
                    .Where(p => s.Preview(p.offer.Id, p.hero.Id).CanAssign)
                    .Select(p => ((QuestOffer, Hero)?)(p.offer, p.hero)).FirstOrDefault();
                if (pick is null) break;
                Assert.True(s.Assign(pick.Value.Item1.Id, pick.Value.Item2.Id).Ok);
            }
            foreach (var quest in s.Quests.Where(q => q.State == QuestState.Assigned).ToList()) Assert.True(s.Resolve(quest.Id).Ok);
            foreach (var complaint in s.OpenComplaints.ToList())
            {
                var choice = new[] { ComplaintChoice.Repair, ComplaintChoice.Apologize, ComplaintChoice.Deny }
                    .FirstOrDefault(c => s.ComplaintCheck(complaint.Id, c).Count == 0, ComplaintChoice.Deny);
                s.ResolveComplaint(complaint.Id, choice);
            }
            Assert.True(s.EndDay().Ok);
        }
        Assert.Equal(7, s.Guild.Day);
        Assert.Equal(2077, s.Guild.TreasuryCopper);
        Assert.Equal(87, s.Guild.Reputation);
        Assert.Equal(18, s.Guild.MissionsSucceeded);
        Assert.Equal(113, s.Events.Count);
        Assert.True(s.Report!.Success);
    }
}
