using Godot;
using GuildOps.Core.Domain;
using GuildOps.Game.Ui;

namespace GuildOps.Game;

/// <summary>Journal des événements, fin de journée (<c>EndDay</c>), bilan royal du jour 7 et redémarrage.</summary>
public partial class DayReport : PanelBase
{
    private RichTextLabel _journal = null!;
    private VBoxContainer _side = null!;

    protected override void Build()
    {
        var root = new HBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 6; root.OffsetRight = -6; root.OffsetTop = 4; root.OffsetBottom = -4;
        AddChild(root);

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.2f };
        root.AddChild(left);
        left.AddChild(UiKit.Lbl("JOURNAL DE LA GUILDE", UiKit.Gold));
        var panel = UiKit.Panel("dark", 6);
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        left.AddChild(panel);
        _journal = UiKit.Rich(onLight: false);
        _journal.ScrollFollowing = true;
        panel.AddChild(_journal);

        var right = UiKit.Panel("parchment", 8);
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(right);
        _side = new VBoxContainer();
        right.AddChild(_side);
    }

    public override void FocusFirst() => FirstButton(_side)?.GrabFocus();

    public override void Refresh()
    {
        if (Shell is null) return;
        var s = Shell.Session;
        var g = s.Guild;
        var finished = s.Status == GameStatus.Finished;
        _journal.Text = string.Join("\n", s.Events.Select(e => $"#{e.Sequence} (J{e.Day}) {e.Message}"));

        UiKit.Clear(_side);
        _side.AddChild(UiKit.Lbl($"TRÉSORERIE ET BILAN — jour {g.Day}/{s.Data.Rules.Days}", UiKit.Ink, wrap: true));
        _side.AddChild(UiKit.IconText("coin", $"Trésorerie : {s.Money(g.TreasuryCopper)}", UiKit.Ink));
        _side.AddChild(UiKit.IconText("reputation", $"Réputation : {g.Reputation}/{s.Data.Rules.ReputationMax} ({s.ReputationTierId})", UiKit.Ink));
        _side.AddChild(UiKit.IconText("scroll", $"Missions réussies : {g.MissionsSucceeded} · engagées aujourd'hui : {g.MissionsAcceptedToday}/{s.Data.Rules.MaxMissionsPerDay}", UiKit.Ink));
        _side.AddChild(UiKit.Lbl($"Réclamations ouvertes : {s.OpenComplaints.Count} · graine {s.Seed}", UiKit.Ink, wrap: true));

        if (s.Report is { } report)
        {
            _side.AddChild(UiKit.IconText(report.Success ? "success" : "failure", report.Success ? "REDRESSEMENT RÉUSSI" : "BILAN INACHEVÉ", report.Success ? UiKit.Good(true) : UiKit.Bad(true)));
            _side.AddChild(Check("Réputation", report.ReputationOk));
            _side.AddChild(Check("Trésorerie", report.TreasuryOk));
            _side.AddChild(Check("Missions réussies", report.MissionsOk));
            _side.AddChild(UiKit.Lbl(report.Message, UiKit.Ink, wrap: true));
        }
        var end = UiKit.Btn(g.Day >= s.Data.Rules.Days ? "Terminer la journée → bilan royal" : "Terminer la journée", Shell.EndDay);
        end.ClipText = true;
        end.Disabled = finished;
        _side.AddChild(end);
        if (finished) _side.AddChild(UiKit.Btn("Recommencer (même graine)", Shell.Reset));
    }

    private static Control Check(string label, bool ok) =>
        UiKit.IconText(ok ? "success" : "failure", $"{label} : {(ok ? "atteint" : "non atteint")}", UiKit.Ink);
}
