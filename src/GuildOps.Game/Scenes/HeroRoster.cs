using Godot;
using GuildOps.Core.Domain;
using GuildOps.Game.Ui;

namespace GuildOps.Game;

/// <summary>Grille 4×3 des douze héros. L'éligibilité pour l'offre choisie vient de <c>Preview</c> ; un héros indisponible reste sélectionnable.</summary>
public partial class HeroRoster : PanelBase
{
    private GridContainer _grid = null!;
    private AssignmentPanel _assignment = null!;

    protected override void Build()
    {
        var root = new HBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 6; root.OffsetRight = -6; root.OffsetTop = 1; root.OffsetBottom = -1; // 3 × 102 + 2 × 2 = 310 px
        AddChild(root);

        var left = new VBoxContainer();
        root.AddChild(left);
        _grid = new GridContainer { Columns = 4 };
        _grid.AddThemeConstantOverride("h_separation", 2);
        _grid.AddThemeConstantOverride("v_separation", 2);
        left.AddChild(_grid);

        _assignment = GD.Load<PackedScene>("res://Scenes/AssignmentPanel.tscn").Instantiate<AssignmentPanel>();
        _assignment.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _assignment.CustomMinimumSize = new Vector2(190, 0);
        root.AddChild(_assignment);
        _assignment.Bind(Shell);
    }

    public override void FocusFirst() => (FirstButton(_grid) ?? FirstButton(this))?.GrabFocus();

    public override void Refresh()
    {
        if (Shell is null) return;
        var s = Shell.Session;
        var offer = Shell.SelectedOffer;
        var focused = GetViewport()?.GuiGetFocusOwner();
        var keepIndex = focused is not null && focused.GetParent() == _grid ? focused.GetIndex() : -1;
        UiKit.Clear(_grid);
        foreach (var hero in s.Heroes)
        {
            string? reason = null;
            var eligible = true;
            if (offer is not null)
            {
                var check = s.Preview(offer.Id, hero.Id);
                eligible = check.CanAssign;
                if (!eligible) reason = check.Message;
            }
            var state = hero.Id == Shell.SelectedHeroId ? CardState.Selected : !eligible || !hero.IsFree ? CardState.Dim : CardState.Normal;
            var card = Cards.HeroCard(s, hero, state, reason);
            var id = hero.Id;
            card.Pressed += () => Shell.SelectHero(id);
            _grid.AddChild(card);
        }
        if (keepIndex >= 0 && keepIndex < _grid.GetChildCount() && Visible) _grid.GetChild<Button>(keepIndex).GrabFocus();
        _assignment.Refresh();
    }
}
