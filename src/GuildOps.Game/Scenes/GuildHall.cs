using Godot;
using GuildOps.Core.Domain;
using GuildOps.Game.Ui;

namespace GuildOps.Game;

/// <summary>
/// Hall de la guilde : décor fourni + zones d'interaction qui sont des contrôles Godot séparés des pixels du décor (clic ou clavier).
/// Chaque zone épouse un objet du décor (tableau de contrats, arche, bureau, tapis, coffre) et porte une plaque de nom ancrée sur l'objet
/// et un compteur tiré de Core fusionné dans le même texte (« Contrats (4) »). Les douze sprites sont purement décoratifs, répartis en petits groupes : pas de déplacement libre,
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

    private sealed record ZoneUi(Button Button, Label Text, string Name);

    protected override void Build()
    {
        var background = new TextureRect
        {
            Texture = UiKit.Tex("backgrounds/guildhall_640x360.png"), Position = new Vector2(0, -TopOffset), Size = new Vector2(640, 360),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(background);

        // Ambiance B5.2 : deux boucles décoratives posées sur le décor (sous les sprites, les zones et les étiquettes), calées sur les points
        // d'ancrage du décor natif ; le décalage vertical du hall (TopOffset) est appliqué à la pose.
        AddChild(AmbientFx.Create("foyer_8x64.png", 8, 64, 64, 170, new Vector2(321, 162), TopOffset));
        AddChild(AmbientFx.Create("tresor_8x32.png", 8, 32, 32, 140, new Vector2(498, 221), TopOffset, phase: 0.3));

        _sprites = new Control { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_sprites);

        // Rectangles calés sur les objets du décor ; l'étiquette (nom + compteur en un seul texte) est posée sur l'objet lui-même :
        // (ancre horizontale 0..1, ancre verticale 0..1, décalage y) relatifs au rectangle de la zone.
        Zone(View.Board, "Contrats", new Rect2(70, 120, 82, 76), 0.5f, 0f, 3);          // tableau de liège, étiquette sur le cadre haut
        Zone(View.Roster, "Aventuriers", new Rect2(522, 115, 108, 108), 0.5f, 1f, -6);    // grande arche d'où partent les aventuriers
        Zone(View.Missions, "Missions", new Rect2(232, 176, 176, 46), 0.5f, 1f, -3);      // bureau du maître de guilde, bord avant
        Zone(View.Complaints, "Réclamations", new Rect2(200, 243, 240, 58), 0.5f, 0.5f, 0); // tapis d'accueil devant le bureau
        Zone(View.Report, "Trésorerie", new Rect2(452, 178, 52, 44), 0.5f, 1f, 11);        // coffre ouvert rempli d'or
    }

    private void Zone(View target, string name, Rect2 rect, float anchorX, float anchorY, int offsetY)
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
        button.AddThemeStyleboxOverride("focus", UiKit.FocusBox());
        button.Pressed += () => Shell.Show(target);
        AddChild(button);

        // Une seule étiquette compacte par zone : « Nom (compteur) », Pixeloid Sans Bold 9 px, fond encre translucide.
        var plaque = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        plaque.AddThemeStyleboxOverride("panel", PlaqueStyle(false));
        var text = UiKit.HallLbl(name, UiKit.Cream);
        plaque.AddChild(text);
        plaque.AnchorLeft = plaque.AnchorRight = anchorX;
        plaque.AnchorTop = plaque.AnchorBottom = anchorY;
        plaque.OffsetLeft = plaque.OffsetRight = 0;
        plaque.OffsetTop = plaque.OffsetBottom = offsetY;
        plaque.GrowHorizontal = GrowDirection.Both;
        plaque.GrowVertical = anchorY >= 1f ? GrowDirection.Begin : anchorY > 0f ? GrowDirection.Both : GrowDirection.End;
        button.AddChild(plaque);

        void Highlight(bool on)
        {
            plaque.AddThemeStyleboxOverride("panel", PlaqueStyle(on));
            text.AddThemeColorOverride("font_color", on ? UiKit.Ink : UiKit.Cream);
        }
        button.MouseEntered += () => Highlight(true);
        button.MouseExited += () => Highlight(button.HasFocus());
        button.FocusEntered += () => Highlight(true);
        button.FocusExited += () => Highlight(false);

        _zones[target] = new ZoneUi(button, text, name);
    }

    private static StyleBoxFlat PlaqueStyle(bool active)
    {
        var box = UiKit.Flat(active ? UiKit.Gold : new Color(UiKit.Ink, 0.88f), active ? UiKit.Ink : new Color(UiKit.Gold, 0.55f), 1, 0);
        box.ContentMarginLeft = box.ContentMarginRight = 4;
        box.ContentMarginTop = 1; box.ContentMarginBottom = 2;
        return box;
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

        void Set(View view, string badge, string tooltip)
        {
            var zone = _zones[view];
            zone.Text.Text = badge.Length > 0 ? $"{zone.Name} ({badge})" : zone.Name;
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
