using Godot;

namespace GuildOps.Game.Ui;

/// <summary>
/// Introduction d'une nouvelle partie : la salle du Conseil royal (illustration B5.3) sous un parchemin Godot. Le texte est dans des contrôles,
/// jamais dans l'image. Ignorable immédiatement (« Passer l'introduction », Échap) ; n'avance rien dans Core.
/// Rouverte volontairement depuis l'aide, elle ne fait que se refermer (<see cref="OpenReplay"/>).
/// </summary>
public partial class IntroScreen : Overlay
{
    private Label _body = null!;
    private Button _start = null!, _skip = null!;
    private bool _replay;

    protected override void Build()
    {
        var back = new ColorRect { Color = UiKit.Backdrop, MouseFilter = MouseFilterEnum.Ignore };
        back.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(back);

        var ribbon = B53.RibbonWithText("title", "Le Conseil royal");
        ribbon.Position = new Vector2(224, 6);
        AddChild(ribbon);

        var picture = B53.Picture(B53.Intro);
        picture.Position = new Vector2(128, 42);
        AddChild(picture);

        // Parchemin Godot posé sur le bas de l'illustration (le décor reste lisible au-dessus).
        var parchment = UiKit.Panel("parchment", 8);
        parchment.Position = new Vector2(144, 168);
        parchment.CustomMinimumSize = new Vector2(352, 0);
        AddChild(parchment);
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 3);
        parchment.AddChild(box);
        _body = UiKit.Lbl("", UiKit.Ink, wrap: true);
        _body.CustomMinimumSize = new Vector2(330, 0);
        box.AddChild(_body);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 8);
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        row.OffsetTop = -40; row.OffsetBottom = -14;
        AddChild(row);
        _start = IconButton("Commencer", "play", Finish, 130, 22);
        _skip = UiKit.Btn("Passer l'introduction", Finish, 150, 22);
        row.AddChild(_start);
        row.AddChild(_skip);
    }

    /// <summary>Introduction d'une nouvelle partie : « Commencer » et « Passer » mènent tous deux au hall.</summary>
    public void OpenNewGame()
    {
        _replay = false;
        Open();
    }

    /// <summary>Relecture depuis l'aide : un seul bouton, qui referme sans toucher à la navigation ni à la partie.</summary>
    public void OpenReplay()
    {
        _replay = true;
        Open();
    }

    protected override void OnOpening()
    {
        var rules = Shell.Session.Data.Rules;
        _body.Text = $"Le Conseil royal vous confie une guilde à remettre sur pied. Vous avez {rules.Days} jours pour organiser les missions " +
                     $"et faire vos preuves : au jour {rules.Days}, le Conseil rendra son bilan.";
        _start.Text = _replay ? "Fermer" : "Commencer";
        _skip.Visible = !_replay;
        SetFocusRing(_replay ? [_start] : [_start, _skip]);
    }

    private void Finish()
    {
        Close();
        if (!_replay) Shell.BeginPlaying();
    }

    /// <summary>Échap = « Passer l'introduction » (nouvelle partie) ou simple fermeture (relecture).</summary>
    protected override bool OnCancel()
    {
        Finish();
        return true;
    }

    protected override Control InitialFocus() => _start;
}
