using Godot;

namespace GuildOps.Game.Ui;

public enum CardState { Normal, Selected, Dim }

/// <summary>
/// Bouton « carte » : panneau plat (palette du kit) cliquable et focalisable dont les libellés suivent l'état visuel.
/// L'état ne repose pas sur la seule couleur : une sélection ajoute un cadre doré épais et une coche, un état grisé peut
/// porter une icône (⚠ engagé, ✖ incompatible, ✔ acceptée…) posée dans l'angle supérieur droit.
/// Les cartes sont plates (bord de 2 px) : le 9-slice ornemental réserve 8 px par côté, trop pour une police de 15 px.
/// </summary>
public partial class CardButton : Button
{
    public static readonly Color ParchmentFill = new(0.93f, 0.85f, 0.62f);
    public static readonly Color ParchmentEdge = new(0.50f, 0.33f, 0.18f);
    private static readonly Color DimFill = new(0.17f, 0.13f, 0.27f);
    private static readonly Color DimEdge = new(0.40f, 0.32f, 0.54f);
    private static readonly Color SelectedFill = new(0.45f, 0.15f, 0.26f);

    private readonly List<Label> _labels = [];
    private TextureRect? _marker;

    public T Track<T>(T label) where T : Label { _labels.Add(label); return label; }

    /// <summary>Applique l'état visuel. <paramref name="marker"/> : nom d'icône (<c>success</c>, <c>warning</c>, <c>failure</c>…) ou null.</summary>
    public void SetState(CardState state, string? marker = null)
    {
        var (fill, edge, width) = state switch
        {
            CardState.Selected => (SelectedFill, UiKit.Gold, 3),
            CardState.Dim => (DimFill, DimEdge, 2),
            _ => (ParchmentFill, ParchmentEdge, 2),
        };
        var normal = UiKit.Flat(fill, edge, width, 0);
        var hover = UiKit.Flat(fill.Lightened(0.12f), edge, width, 0);
        AddThemeStyleboxOverride("normal", normal);
        AddThemeStyleboxOverride("hover", hover);
        AddThemeStyleboxOverride("pressed", hover);
        AddThemeStyleboxOverride("hover_pressed", hover);
        AddThemeStyleboxOverride("disabled", normal);
        AddThemeStyleboxOverride("focus", UiKit.FocusBox());
        var ink = state switch { CardState.Normal => UiKit.Ink, CardState.Selected => UiKit.Cream, _ => new Color(0.78f, 0.73f, 0.84f) };
        foreach (var label in _labels) label.AddThemeColorOverride("font_color", ink);

        _marker?.QueueFree();
        _marker = null;
        marker ??= state == CardState.Selected ? "success" : null;
        if (marker is not null)
        {
            _marker = UiKit.Icon(marker);
            _marker.SetAnchorsPreset(LayoutPreset.TopRight);
            _marker.OffsetLeft = -19; _marker.OffsetRight = -3; _marker.OffsetTop = 3; _marker.OffsetBottom = 19;
            AddChild(_marker);
        }
    }
}
