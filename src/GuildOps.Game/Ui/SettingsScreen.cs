using Godot;

namespace GuildOps.Game.Ui;

/// <summary>
/// Paramètres d'affichage et de confort : fenêtré / plein écran, échelle entière de la fenêtre, « Réduire les animations », rappel des commandes
/// clavier. Chaque état est écrit en toutes lettres (jamais la couleur seule). Aucun réglage audio : GuildOps est entièrement silencieux.
/// Ces réglages ne touchent ni à Core ni aux résultats : voir <see cref="DisplaySettings"/>.
/// </summary>
public partial class SettingsScreen : Overlay
{
    private Button _fullscreen = null!, _smaller = null!, _larger = null!, _motion = null!, _close = null!;
    private Label _scaleLabel = null!, _scaleNote = null!;

    public Button FullscreenButton => _fullscreen;
    public Button SmallerButton => _smaller;
    public Button LargerButton => _larger;
    public Button MotionButton => _motion;

    protected override void Build()
    {
        AddDim(0.84f);
        var center = AddCenter();
        var panel = UiKit.Panel("parchment", 8);
        center.AddChild(panel);
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 3);
        panel.AddChild(box);

        box.AddChild(UiKit.AsTitle(UiKit.Lbl("Paramètres", UiKit.Ink)));
        box.AddChild(B53.OrnamentDivider());

        box.AddChild(UiKit.AsTitle(UiKit.Lbl("Affichage", UiKit.MutedOnLight)));
        _fullscreen = IconButton("", "fullscreen", () => Shell.Settings.SetFullscreen(!Shell.Settings.Fullscreen), 230, 20);
        box.AddChild(_fullscreen);
        var scaleRow = new HBoxContainer();
        scaleRow.AddThemeConstantOverride("separation", 4);
        box.AddChild(scaleRow);
        _smaller = UiKit.Btn("Plus petite", () => Shell.Settings.SetScale(Shell.Settings.Scale - 1), 76, 20);
        _larger = UiKit.Btn("Plus grande", () => Shell.Settings.SetScale(Shell.Settings.Scale + 1), 76, 20);
        _smaller.TooltipText = "Fenêtre plus petite (échelle entière)";
        _larger.TooltipText = "Fenêtre plus grande (échelle entière)";
        _scaleLabel = UiKit.AsTitle(UiKit.Lbl("", UiKit.Ink));
        _scaleLabel.VerticalAlignment = VerticalAlignment.Center;
        _scaleLabel.CustomMinimumSize = new Vector2(200, 0);
        scaleRow.AddChild(_smaller);
        scaleRow.AddChild(_larger);
        scaleRow.AddChild(_scaleLabel);
        _scaleNote = UiKit.Lbl("", UiKit.MutedOnLight, wrap: true);
        _scaleNote.CustomMinimumSize = new Vector2(380, 0);
        box.AddChild(_scaleNote);

        box.AddChild(UiKit.AsTitle(UiKit.Lbl("Confort", UiKit.MutedOnLight)));
        _motion = IconButton("", "reduce_motion", () => Shell.Settings.SetReduceMotion(!Shell.Settings.ReduceMotion), 230, 20);
        box.AddChild(_motion);
        var motionNote = UiKit.Lbl("Coupe l'ambiance du hall et les effets de résultat. Sans aucune conséquence sur les règles, les tirages ou les résultats.", UiKit.MutedOnLight, wrap: true);
        motionNote.CustomMinimumSize = new Vector2(380, 0);
        box.AddChild(motionNote);

        box.AddChild(UiKit.AsTitle(UiKit.Lbl("Commandes clavier", UiKit.MutedOnLight)));
        foreach (var line in HelpContent.Keyboard)
            box.AddChild(UiKit.IconText("keyboard", line, UiKit.Ink, folder: B53.Icons));

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        box.AddChild(row);
        _close = UiKit.Btn("Fermer (Échap)", Close, 110);
        row.AddChild(_close);
        Shell.Settings.Changed += Refresh;   // les libellés d'état suivent chaque changement, quelle que soit son origine
    }

    protected override void OnOpening() => Refresh();

    /// <summary>Remet à jour les libellés d'état et la boucle de focus (les boutons d'échelle sont écartés en plein écran ou aux bornes).</summary>
    public void Refresh()
    {
        var settings = Shell.Settings;
        _fullscreen.Text = $"Plein écran : {(settings.Fullscreen ? "OUI" : "NON")}";
        _motion.Text = $"Réduire les animations : {(settings.ReduceMotion ? "OUI" : "NON")}";
        _scaleLabel.Text = settings.Fullscreen
            ? "Échelle : automatique (plein écran)"
            : $"Fenêtre : x{settings.Scale} ({settings.WindowSize.X}x{settings.WindowSize.Y})";
        _scaleNote.Text = $"Image logique {DisplaySettings.LogicalWidth}x{DisplaySettings.LogicalHeight}, pixels nets : seules les échelles entières sont proposées (jusqu'à x{settings.MaxScale} sur cet écran).";
        _smaller.Disabled = settings.Fullscreen || settings.Scale <= 1;
        _larger.Disabled = settings.Fullscreen || settings.Scale >= settings.MaxScale;
        SetFocusRing([_fullscreen, _smaller, _larger, _motion, _close]);
        // Un bouton devenu inutilisable perd le focus : on le rend à son pendant (ou au plein écran) pour que le clavier garde une prise.
        if (IsOpen && GetViewport().GuiGetFocusOwner() is null)
            (settings.Fullscreen ? _fullscreen : !_smaller.Disabled ? _smaller : !_larger.Disabled ? _larger : _fullscreen).GrabFocus();
    }

    protected override Control InitialFocus() => _fullscreen;
}
