using Godot;

namespace GuildOps.Game.Ui;

public enum CardState { Normal, Selected, Dim }

/// <summary>Bouton « carte » : un panneau 9-slice cliquable et focalisable dont les libellés suivent l'état visuel.</summary>
public partial class CardButton : Button
{
    private readonly List<Label> _labels = [];

    public T Track<T>(T label) where T : Label { _labels.Add(label); return label; }

    public void SetState(CardState state)
    {
        var kind = state switch { CardState.Selected => "burgundy", CardState.Dim => "dark", _ => "parchment" };
        var file = $"ui/panel_{kind}_9slice.png";
        var normal = UiKit.Nine(file, 8, 4);
        var hover = UiKit.Nine(file, 8, 4, new Color(1.15f, 1.12f, 1.05f));
        AddThemeStyleboxOverride("normal", normal);
        AddThemeStyleboxOverride("hover", hover);
        AddThemeStyleboxOverride("pressed", hover);
        AddThemeStyleboxOverride("hover_pressed", hover);
        AddThemeStyleboxOverride("disabled", normal);
        AddThemeStyleboxOverride("focus", UiKit.FocusBox());
        var ink = state switch { CardState.Normal => UiKit.Ink, CardState.Selected => UiKit.Cream, _ => new Color(0.70f, 0.65f, 0.75f) };
        foreach (var label in _labels) label.AddThemeColorOverride("font_color", ink);
    }
}
