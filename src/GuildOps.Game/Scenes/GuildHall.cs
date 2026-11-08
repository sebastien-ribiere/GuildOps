using Godot;
using GuildOps.Core.Domain;
using GuildOps.Game.Ui;

namespace GuildOps.Game;

/// <summary>
/// Hall de la guilde : décor fourni + zones d'interaction qui sont des contrôles Godot séparés des pixels du décor
/// (clic ou clavier). Pas de déplacement libre ni de pathfinding ; les sprites sont purement décoratifs.
/// </summary>
public partial class GuildHall : PanelBase
{
    private const int TopOffset = 24; // le décor 640×360 déborde sous le HUD : on le recale.

    private readonly Dictionary<View, Label> _badges = [];
    private Control _sprites = null!;

    protected override void Build()
    {
        var background = new TextureRect
        {
            Texture = UiKit.Tex("backgrounds/guildhall_640x360.png"), Position = new Vector2(0, -TopOffset), Size = new Vector2(640, 360),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(background);

        _sprites = new Control { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_sprites);

        Zone(View.Board, "Contrats", new Rect2(40, 105, 142, 117));
        Zone(View.Roster, "Aventuriers", new Rect2(470, 105, 127, 122));
        Zone(View.Missions, "Missions", new Rect2(385, 225, 66, 46));
        Zone(View.Complaints, "Réclamations", new Rect2(284, 268, 78, 92));
        Zone(View.Report, "Trésorerie / bilan", new Rect2(508, 255, 94, 56));
    }

    private void Zone(View target, string title, Rect2 rect)
    {
        var button = new Button
        {
            Position = rect.Position - new Vector2(0, TopOffset), Size = rect.Size, Flat = true, TooltipText = title,
            FocusMode = FocusModeEnum.All,
        };
        var hover = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.14f) };
        var empty = new StyleBoxEmpty();
        foreach (var name in new[] { "normal", "disabled" }) button.AddThemeStyleboxOverride(name, empty);
        foreach (var name in new[] { "hover", "pressed", "hover_pressed" }) button.AddThemeStyleboxOverride(name, hover);
        var focus = UiKit.FocusBox();
        focus.BorderWidthLeft = focus.BorderWidthRight = focus.BorderWidthTop = focus.BorderWidthBottom = 2;
        button.AddThemeStyleboxOverride("focus", focus);
        button.Pressed += () => Shell.Show(target);
        AddChild(button);

        var chip = new PanelContainer();
        var style = new StyleBoxFlat { BgColor = new Color(UiKit.Ink, 0.92f), BorderColor = UiKit.Gold };
        style.SetBorderWidthAll(1);
        style.SetContentMarginAll(3);
        chip.AddThemeStyleboxOverride("panel", style);
        chip.MouseFilter = MouseFilterEnum.Ignore;
        var label = UiKit.Lbl(title);
        chip.AddChild(label);
        // Ancrée au centre-bas de la zone : la pastille se recentre toute seule quand le texte change.
        chip.AnchorLeft = chip.AnchorRight = 0.5f;
        chip.AnchorTop = chip.AnchorBottom = 1;
        chip.OffsetLeft = chip.OffsetRight = 0;
        chip.OffsetTop = chip.OffsetBottom = -2;
        chip.GrowHorizontal = GrowDirection.Both;
        chip.GrowVertical = GrowDirection.Begin;
        button.AddChild(chip);
        _badges[target] = label;
        label.SetMeta("title", title);
    }

    public override void FocusFirst()
    {
        if (GetViewport()?.GuiGetFocusOwner() is null || !IsAncestorOf(GetViewport().GuiGetFocusOwner()))
            FirstButton(this)?.GrabFocus();
    }

    public override void Refresh()
    {
        if (Shell is null) return;
        var s = Shell.Session;
        void Set(View view, string extra)
        {
            var label = _badges[view];
            label.Text = (string)label.GetMeta("title") + extra;
        }
        Set(View.Board, $" ({s.CurrentOffers.Count(o => o.State == OfferState.Available)})");
        Set(View.Roster, $" ({s.Heroes.Count(h => h.IsFree)}/{s.Heroes.Count})");
        Set(View.Missions, $" ({s.Quests.Count(q => q.State == QuestState.Assigned)})");
        Set(View.Complaints, $" ({s.OpenComplaints.Count})");

        UiKit.Clear(_sprites);
        for (var i = 0; i < s.Heroes.Count; i++)
        {
            var hero = s.Heroes[i];
            var sprite = new IdleSprite();
            sprite.Setup(hero.Id, i);
            var x = i < 6 ? 24 + 34 * i : 372 + 34 * (i - 6);
            sprite.Position = new Vector2(x, 286 - TopOffset - 6 * (i % 2));
            if (!hero.IsFree) sprite.Modulate = new Color(0.6f, 0.6f, 0.75f);
            _sprites.AddChild(sprite);
        }
    }
}
