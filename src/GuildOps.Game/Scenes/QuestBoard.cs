using Godot;
using GuildOps.Core.Domain;
using GuildOps.Game.Ui;

namespace GuildOps.Game;

/// <summary>Tableau des contrats : les offres du jour, la sélection et la régénération (une fois, si Core l'autorise).</summary>
public partial class QuestBoard : PanelBase
{
    private VBoxContainer _rows = null!;
    private Button _regen = null!;
    private AssignmentPanel _assignment = null!;

    protected override void Build()
    {
        var root = new HBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 6; root.OffsetRight = -6; root.OffsetTop = 4; root.OffsetBottom = -4;
        AddChild(root);

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 2 };
        root.AddChild(left);
        left.AddChild(UiKit.Title("Tableau des contrats"));
        _rows = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 3);
        left.AddChild(_rows);
        _regen = UiKit.Btn("", Shell.Regenerate);
        _regen.ClipText = true;
        left.AddChild(_regen);

        _assignment = GD.Load<PackedScene>("res://Scenes/AssignmentPanel.tscn").Instantiate<AssignmentPanel>();
        _assignment.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _assignment.CustomMinimumSize = new Vector2(200, 0);
        root.AddChild(_assignment);
        _assignment.Bind(Shell);
    }

    public override void FocusFirst() => (FirstButton(_rows) ?? FirstButton(this))?.GrabFocus();

    public override void Refresh()
    {
        if (Shell is null) return;
        var s = Shell.Session;
        var focused = GetViewport()?.GuiGetFocusOwner();
        var keepIndex = focused is not null && focused.GetParent() == _rows ? focused.GetIndex() : -1;
        UiKit.Clear(_rows);
        foreach (var offer in s.CurrentOffers)
        {
            var selected = offer.Id == Shell.SelectedOfferId;
            var state = selected ? CardState.Selected : offer.State != OfferState.Available ? CardState.Dim : CardState.Normal;
            var row = Cards.OfferRow(s, offer, state, 0, 66);
            row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var id = offer.Id;
            row.Pressed += () => Shell.SelectOffer(id);
            _rows.AddChild(row);
        }
        if (keepIndex >= 0 && keepIndex < _rows.GetChildCount() && Visible) _rows.GetChild<Button>(keepIndex).GrabFocus();

        _regen.Disabled = s.RegenerationCheck().Count > 0;
        _regen.Text = s.RegenerationUsed
            ? "Renouveler le tableau (déjà utilisé)"
            : $"Renouveler (-{s.Data.Rules.RegenerationReputationCost} réputation, 1 fois par partie)";
        _assignment.Refresh();
    }
}
