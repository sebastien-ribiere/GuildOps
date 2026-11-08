using Godot;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;

namespace GuildOps.Game;

/// <summary>
/// Scénario de preuve (option <c>-- --autoplay [--shots=dossier]</c>) : joue 7 jours via les MÊMES commandes que les boutons
/// (sélection puis GameShell.Assign/Resolve/…). Politique déterministe, identique à celle du test de parité Core.
/// </summary>
public sealed class AutoPlay(GameShell shell)
{
    private string? _shotsDir;

    public async Task Run()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--shots=")) _shotsDir = arg["--shots=".Length..];
        if (_shotsDir is not null) DirAccess.MakeDirRecursiveAbsolute(_shotsDir);

        var s = shell.Session;
        await Shot("01-hall", View.Hall);
        await KeyboardCheck();
        var shotsDone = new HashSet<string>();
        while (s.Status == GameStatus.InProgress)
        {
            var day = s.Guild.Day;
            for (var n = 0; n < s.Data.Rules.MaxMissionsPerDay; n++)
            {
                var pick = Pick(s);
                if (pick is null) break;
                shell.Show(View.Board);
                shell.SelectOffer(pick.Value.offer);
                if (shotsDone.Add("board")) await Shot("02-board", View.Board);
                shell.Show(View.Roster);
                shell.SelectHero(pick.Value.hero);
                if (shotsDone.Add("roster")) await Shot("03-roster", View.Roster);
                shell.Assign();
            }
            foreach (var quest in s.Quests.Where(q => q.State == QuestState.Assigned).ToList())
            {
                shell.SelectQuest(quest.Id);
                shell.Show(View.Missions);
                shell.Resolve();
                if (shotsDone.Add("missions")) await Shot("04-missions", View.Missions);
            }
            foreach (var complaint in s.OpenComplaints.ToList())
            {
                shell.SelectComplaint(complaint.Id);
                shell.Show(View.Complaints);
                if (shotsDone.Add("complaints")) await Shot("05-complaints", View.Complaints);
                shell.ResolveComplaint(Choose(s, complaint));
            }
            shell.Show(View.Report);
            if (day == 1) await Shot("06-report-day1", View.Report);
            shell.EndDay();
        }
        await Shot("07-report-final", View.Report);
        var g = s.Guild;
        GD.Print($"AUTOPLAY_RESULT day={g.Day} treasury={g.TreasuryCopper} reputation={g.Reputation} succeeded={g.MissionsSucceeded} events={s.Events.Count} status={s.Status} success={s.Report?.Success}");
        shell.GetTree().Quit();
    }

    /// <summary>Politique commune (voir le test de parité) : première offre disponible × premier héros dont Preview.CanAssign est vrai.</summary>
    public static (string offer, string hero)? Pick(GameSession s)
    {
        foreach (var offer in s.CurrentOffers.Where(o => o.State == OfferState.Available))
            foreach (var hero in s.Heroes)
                if (s.Preview(offer.Id, hero.Id).CanAssign) return (offer.Id, hero.Id);
        return null;
    }

    public static ComplaintChoice Choose(GameSession s, Complaint c)
    {
        foreach (var choice in new[] { ComplaintChoice.Repair, ComplaintChoice.Apologize, ComplaintChoice.Deny })
            if (s.ComplaintCheck(c.Id, choice).Count == 0) return choice;
        return ComplaintChoice.Deny;
    }

    /// <summary>Navigation clavier : un contrôle a le focus à l'ouverture de chaque vue, et Échap ramène au hall.</summary>
    private async Task KeyboardCheck()
    {
        var results = new List<string>();
        foreach (var view in new[] { View.Hall, View.Board, View.Roster, View.Missions, View.Complaints, View.Report })
        {
            shell.Show(view);
            await shell.ToSignal(shell.GetTree(), SceneTree.SignalName.ProcessFrame);
            var owner = shell.GetViewport().GuiGetFocusOwner();
            results.Add($"{view}:{(owner is null ? "none" : "ok")}");
        }
        shell.Show(View.Board);
        var escape = new InputEventAction { Action = "ui_cancel", Pressed = true };
        Input.ParseInputEvent(escape);
        await shell.ToSignal(shell.GetTree(), SceneTree.SignalName.ProcessFrame);
        await shell.ToSignal(shell.GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print($"KEYBOARD_CHECK focus[{string.Join(" ", results)}] escape_to_hall={shell.Current == View.Hall}");
        shell.Show(View.Hall);
    }

    private async Task Shot(string name, View view)
    {
        shell.Show(view);
        if (_shotsDir is null) return;
        await shell.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await shell.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = shell.GetViewport().GetTexture().GetImage();
        image.Resize(image.GetWidth() * 2, image.GetHeight() * 2, Image.Interpolation.Nearest);
        image.SavePng($"{_shotsDir}/{name}.png");
    }
}
