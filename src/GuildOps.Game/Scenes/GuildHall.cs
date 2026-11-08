using Godot;
using GuildOps.Core.Domain;
using GuildOps.Game.Ui;

namespace GuildOps.Game;

/// <summary>
/// Hall de la guilde : décor fourni + zones d'interaction qui sont des contrôles Godot séparés des pixels du décor (clic ou clavier).
/// Chaque zone épouse un objet du décor (panneau de contrats, râtelier, table, porte, coffre) et porte une plaque de nom ancrée sur l'objet
/// et une pastille d'état tirée de Core. Les douze sprites sont purement décoratifs, répartis en petits groupes : pas de déplacement libre,
/// ni collision, ni pathfinding.
/// </summary>
public partial class GuildHall : PanelBase
{
    private const int TopOffset = 24; // le décor 640×360 déborde sous le HUD : on le recale.

    /// <summary>Positions décoratives (x, haut du sprite) en coordonnées du décor, par petits groupes hors des zones cliquables.</summary>
    private static readonly (int x, int y)[] Spots =
    [
        (30, 228), (70, 238), (112, 226),   // devant le panneau de contrats
        (60, 284), (100, 292), (146, 276),  // bas-gauche de la salle
        (232, 270), (254, 288),             // à gauche de la porte
        (372, 280), (402, 292),             // à droite de la porte, sous la table
        (456, 240), (478, 268),             // près du râtelier
    ];

    private readonly Dictionary<View, ZoneUi> _zones = [];
    private Control _sprites = null!;

    private sealed record ZoneUi(Button Button, PanelContainer Plaque, Label PlaqueText, Label Badge, string Name);

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

        // Rectangles calés sur les objets du décor ; plaque au-dessus de l'objet quand il est petit, sur son cadre sinon.
        Zone(View.Board, "Contrats", new Rect2(40, 105, 142, 117), plaqueAbove: false);
        Zone(View.Roster, "Aventuriers", new Rect2(468, 105, 130, 124), plaqueAbove: false);
        Zone(View.Missions, "Missions", new Rect2(382, 214, 72, 34), plaqueAbove: true);
        Zone(View.Complaints, "Réclamations", new Rect2(284, 266, 78, 70), plaqueAbove: false);
        Zone(View.Report, "Trésorerie", new Rect2(507, 254, 96, 68), plaqueAbove: true);
    }

    private void Zone(View target, string name, Rect2 rect, bool plaqueAbove)
    {
        var button = new Button
        {
            Position = rect.Position - new Vector2(0, TopOffset), Size = rect.Size, Flat = true, TooltipText = name,
            FocusMode = FocusModeEnum.All,
        };
        var hover = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.14f) };
        var empty = new StyleBoxEmpty();
        foreach (var n in new[] { "normal", "disabled" }) button.AddThemeStyleboxOverride(n, empty);
        foreach (var n in new[] { "hover", "pressed", "hover_pressed" }) button.AddThemeStyleboxOverride(n, hover);
        var focus = UiKit.FocusBox();
        button.AddThemeStyleboxOverride("focus", focus);
        button.Pressed += () => Shell.Show(target);
        AddChild(button);

        // Plaque de nom : bois/parchemin du kit, centrée sur le bord haut de l'objet (ou juste au-dessus pour les petits objets).
        var plaque = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        plaque.AddThemeStyleboxOverride("panel", PlaqueStyle(false));
        var text = UiKit.Lbl(name, UiKit.Ink);
        plaque.AddChild(text);
        plaque.AnchorLeft = plaque.AnchorRight = 0.5f;
        plaque.AnchorTop = plaque.AnchorBottom = 0;
        plaque.OffsetLeft = plaque.OffsetRight = 0;
        plaque.OffsetTop = plaqueAbove ? -4 : 4;
        plaque.OffsetBottom = plaqueAbove ? -4 : 4;
        plaque.GrowHorizontal = GrowDirection.Both;
        plaque.GrowVertical = plaqueAbove ? GrowDirection.Begin : GrowDirection.End;
        button.AddChild(plaque);

        // Pastille d'état (coin bas-droit) : valeur dérivée de Core.
        var badgePanel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        badgePanel.AddThemeStyleboxOverride("panel", UiKit.Flat(new Color(UiKit.Ink, 0.92f), UiKit.Gold, 1, 2));
        var badge = UiKit.Lbl("", UiKit.Cream);
        badgePanel.AddChild(badge);
        badgePanel.AnchorLeft = badgePanel.AnchorRight = 1;
        badgePanel.AnchorTop = badgePanel.AnchorBottom = 1;
        badgePanel.GrowHorizontal = GrowDirection.Begin;
        badgePanel.GrowVertical = GrowDirection.Begin;
        badgePanel.OffsetLeft = badgePanel.OffsetRight = -3;
        badgePanel.OffsetTop = badgePanel.OffsetBottom = -3;
        button.AddChild(badgePanel);

        void Highlight(bool on) => plaque.AddThemeStyleboxOverride("panel", PlaqueStyle(on));
        button.MouseEntered += () => Highlight(true);
        button.MouseExited += () => Highlight(button.HasFocus());
        button.FocusEntered += () => Highlight(true);
        button.FocusExited += () => Highlight(false);

        _zones[target] = new ZoneUi(button, plaque, text, badge, name);
    }

    private static StyleBoxFlat PlaqueStyle(bool active)
        => UiKit.Flat(active ? UiKit.Gold : CardButton.ParchmentFill, active ? UiKit.Ink : CardButton.ParchmentEdge, 2, 2);

    public override void FocusFirst()
    {
        if (GetViewport()?.GuiGetFocusOwner() is null || !IsAncestorOf(GetViewport().GuiGetFocusOwner()))
            FirstButton(this)?.GrabFocus();
    }

    public override void Refresh()
    {
        if (Shell is null) return;
        var s = Shell.Session;

        void Set(View view, string badge, string tooltip)
        {
            var zone = _zones[view];
            zone.Badge.Text = badge;
            zone.Badge.GetParent<Control>().Visible = badge.Length > 0;
            zone.Button.TooltipText = $"{zone.Name} — {tooltip}";
        }
        var available = s.CurrentOffers.Count(o => o.State == OfferState.Available);
        var free = s.Heroes.Count(h => h.IsFree);
        var inProgress = s.Quests.Count(q => q.State == QuestState.Assigned);
        var open = s.OpenComplaints.Count;
        Set(View.Board, $"{available}", $"{available} contrat(s) disponible(s)");
        Set(View.Roster, $"{free}/{s.Heroes.Count}", $"{free} héros libre(s) sur {s.Heroes.Count}");
        Set(View.Missions, $"{inProgress}", $"{inProgress} mission(s) en cours");
        Set(View.Complaints, open > 0 ? $"{open}" : "", open > 0 ? $"{open} réclamation(s) ouverte(s)" : "aucune réclamation ouverte");
        Set(View.Report, s.Money(s.Guild.TreasuryCopper), "trésorerie et bilan de la journée");

        UiKit.Clear(_sprites);
        // Tri par hauteur : les sprites du bas passent devant ceux du haut.
        foreach (var i in Enumerable.Range(0, Math.Min(s.Heroes.Count, Spots.Length)).OrderBy(i => Spots[i].y))
        {
            var hero = s.Heroes[i];
            var sprite = new IdleSprite();
            sprite.Setup(hero.Id, i);
            sprite.Position = new Vector2(Spots[i].x, Spots[i].y - TopOffset);
            if (!hero.IsFree) sprite.Modulate = new Color(0.6f, 0.6f, 0.75f);
            _sprites.AddChild(sprite);
        }
    }
}
