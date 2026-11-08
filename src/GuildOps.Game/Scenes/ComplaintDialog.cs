using Godot;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using GuildOps.Game.Ui;

namespace GuildOps.Game;

/// <summary>Réclamations ouvertes/closes/expirées. Validité des choix via <c>ComplaintCheck</c>, effets via <c>ResolveComplaint</c>.</summary>
public partial class ComplaintDialog : PanelBase
{
    private VBoxContainer _list = null!;
    private VBoxContainer _detail = null!;

    protected override void Build()
    {
        var root = new HBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 6; root.OffsetRight = -6; root.OffsetTop = 4; root.OffsetBottom = -4;
        AddChild(root);

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1 };
        root.AddChild(left);
        left.AddChild(UiKit.Title("RÉCLAMATIONS CLIENTS"));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        left.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(_list);

        var panel = UiKit.Panel("parchment", 8);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsStretchRatio = 1.5f;
        root.AddChild(panel);
        _detail = new VBoxContainer();
        panel.AddChild(_detail);
    }

    public override void FocusFirst() => (FirstButton(_list) ?? FirstButton(_detail))?.GrabFocus();

    public override void Refresh()
    {
        if (Shell is null) return;
        var s = Shell.Session;

        UiKit.Clear(_list);
        if (s.Complaints.Count == 0) _list.AddChild(UiKit.Lbl("Aucune réclamation.\nLes incidents de mission en ouvrent une.", wrap: true));
        foreach (var c in s.Complaints)
        {
            var state = c.Id == Shell.SelectedComplaintId ? CardState.Selected : c.State == ComplaintState.Open ? CardState.Normal : CardState.Dim;
            var card = new CardButton { CustomMinimumSize = new Vector2(0, 30), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            box.AddThemeConstantOverride("separation", 0);
            box.SetAnchorsPreset(LayoutPreset.FullRect);
            box.OffsetLeft = 6; box.OffsetRight = -6; box.OffsetTop = 3;
            card.AddChild(box);
            box.AddChild(card.Track(UiKit.Lbl(c.Storylet.Title, clip: true)));
            box.AddChild(card.Track(UiKit.Lbl(StateText(c.State) + (c.State == ComplaintState.Open ? $" · jusqu'au jour {c.ExpiresAfterDay}" : ""), clip: true)));
            card.SetState(state);
            var id = c.Id;
            card.Pressed += () => Shell.SelectComplaint(id);
            _list.AddChild(card);
        }

        UiKit.Clear(_detail);
        var complaint = Shell.SelectedComplaint;
        if (complaint is null) { _detail.AddChild(UiKit.Lbl("Sélectionnez une réclamation.", UiKit.Ink)); return; }

        _detail.AddChild(UiKit.IconText("warning", complaint.Storylet.Title, UiKit.Ink));
        _detail.AddChild(UiKit.Lbl(complaint.Storylet.Text, UiKit.Ink, wrap: true));
        _detail.AddChild(UiKit.Lbl($"État : {StateText(complaint.State)} · réparation : {s.Money(complaint.RepairCostCopper)} · à traiter avant la fin du jour {complaint.ExpiresAfterDay}", UiKit.Ink, wrap: true));

        var labels = complaint.Storylet.Labels;
        var reasons = new HashSet<string>();
        foreach (var choice in Enum.GetValues<ComplaintChoice>())
        {
            var bonus = s.ComplaintTraitBonus(complaint.Id, choice);
            var bonusText = bonus == 0 ? "" : $" [trait {bonus:+#;-#}]";
            var text = choice switch
            {
                ComplaintChoice.Repair => $"{labels.Repair} ({s.Money(complaint.RepairCostCopper)}){bonusText}",
                ComplaintChoice.Apologize => labels.Apologize + bonusText,
                _ => labels.Deny + bonusText,
            };
            var check = s.ComplaintCheck(complaint.Id, choice);
            foreach (var reason in check) reasons.Add(RefusalMessages.For(reason));
            var captured = choice;
            var button = UiKit.Btn(text, () => Shell.ResolveComplaint(captured));
            button.ClipText = true;
            button.TooltipText = text;
            button.Disabled = check.Count > 0;
            _detail.AddChild(button);
        }
        if (reasons.Count > 0) _detail.AddChild(UiKit.Lbl(string.Join("\n", reasons), UiKit.Bad(true), wrap: true));
        if (complaint.State == ComplaintState.Resolved && Shell.LastComplaintOutcome is { } o && o.ComplaintId == complaint.Id)
            _detail.AddChild(UiKit.Lbl($"{o.Message} Réputation {o.ReputationBefore} → {o.ReputationAfter} ({o.ReputationDelta:+#;-#;0}).", UiKit.Ink, wrap: true));
    }

    private static string StateText(ComplaintState state) => state switch
    {
        ComplaintState.Open => "ouverte", ComplaintState.Resolved => "close", _ => "expirée",
    };
}
