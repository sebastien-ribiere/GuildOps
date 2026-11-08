using Godot;
using GuildOps.Core.Engine;

namespace GuildOps.Game;

/// <summary>
/// Outils communs des contrôles automatisés (<see cref="AutoPlay"/> : scénario de référence ; <see cref="FlowCheck"/> : parcours normal
/// titre → introduction → jeu → fin) : images de rendu, événements clavier réels, recherche de contrôles, comptage des échecs.
/// Les lignes <c>*_CHECK</c> sont lues par tools/run-autoplay.sh et tools/run-flow.sh.
/// </summary>
public sealed class Probe(GameShell shell, string? shotsDir)
{
    public GameShell Shell => shell;
    public int Failures { get; private set; }

    public void Check(string name, bool ok, string detail)
    {
        if (!ok) Failures++;
        GD.Print($"{name} {(ok ? "OK" : "FAIL")} — {detail}");
    }

    public async Task Frame(int count = 1)
    {
        for (var i = 0; i < count; i++) await shell.ToSignal(shell.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>Action d'interface (ui_accept, ui_cancel, ui_focus_next…) comme si le joueur appuyait puis relâchait la touche.</summary>
    public async Task Press(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        await Frame(2);
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
        await Frame(1);
    }

    /// <summary>Touche physique réelle (raccourcis qui ne sont pas des actions d'interface, ex. F1).</summary>
    public async Task PressKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        await Frame(2);
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await Frame(1);
    }

    /// <summary>Empreinte de tout ce que Core peut modifier : si elle ne bouge pas, aucune mutation n'a eu lieu.</summary>
    public static string Snapshot(GameSession s)
        => $"{s.Guild.Day}|{s.Guild.TreasuryCopper}|{s.Guild.Reputation}|{s.Guild.MissionsAcceptedToday}|{s.Guild.MissionsSucceeded}|{s.Events.Count}|{s.Quests.Count}|{s.Complaints.Count}|" +
           string.Join(",", s.Quests.Select(q => $"{q.Id}:{q.State}")) + "|" + string.Join(",", s.Heroes.Select(h => $"{h.Id}:{h.Energy}"));

    public Control? Focused() => shell.GetViewport().GuiGetFocusOwner();

    /// <summary>Tabule (ui_focus_next) jusqu'à ce que le contrôle voulu ait le focus ; faux si le tour complet n'y mène pas.</summary>
    public async Task<bool> TabTo(Func<Control, bool> wanted, int max = 60)
    {
        for (var i = 0; i < max; i++)
        {
            if (Focused() is { } c && wanted(c)) return true;
            await Press("ui_focus_next");
        }
        return Focused() is { } last && wanted(last);
    }

    public static Button? FindButton(Node root, string textPrefix)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button { Visible: true } b && b.IsVisibleInTree() && b.Text.StartsWith(textPrefix, StringComparison.Ordinal)) return b;
            if (FindButton(child, textPrefix) is { } found) return found;
        }
        return null;
    }

    /// <summary>Capture du viewport 640×360 agrandie ×2 au plus proche (1280×720), telle que le joueur la voit.</summary>
    public async Task ShotNow(string name)
    {
        if (shotsDir is null) return;
        await shell.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await shell.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = shell.GetViewport().GetTexture().GetImage();
        image.Resize(image.GetWidth() * 2, image.GetHeight() * 2, Image.Interpolation.Nearest);
        image.SavePng($"{shotsDir}/{name}.png");
    }
}
