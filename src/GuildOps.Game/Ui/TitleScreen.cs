using Godot;
using GuildOps.Core.Domain;

namespace GuildOps.Game.Ui;

/// <summary>
/// Écran titre : le fond B5.3 (logo « GuildOps » peint dans l'image, jamais redoublé) et un menu de vrais boutons Godot, en bas à droite.
/// Il ne possède aucune session : « Nouvelle partie » et « Reprendre » délèguent à <see cref="GameShell"/>, qui garde l'UNIQUE
/// <c>GameSession</c>. « Reprendre » n'existe que pour une partie réellement en mémoire (aucune sauvegarde sur disque n'est prétendue).
/// Échap n'a pas d'effet ici : on ne quitte jamais par accident.
/// </summary>
public partial class TitleScreen : Overlay
{
    private Button _resume = null!, _new = null!, _quit = null!;
    private readonly List<Button> _menu = [];
    private Label _note = null!;
    private VBoxContainer _list = null!;

    public IReadOnlyList<Button> MenuButtons => _menu;
    public Button ResumeButton => _resume;

    protected override void Build()
    {
        var art = B53.Picture(B53.Title);
        art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(art);

        var panel = UiKit.Panel("dark", 6);
        panel.SelfModulate = new Color(1, 1, 1, 0.95f);   // fond prune semi-opaque : le texte reste lisible sur l'illustration
        panel.CustomMinimumSize = new Vector2(176, 0);
        panel.AnchorLeft = panel.AnchorRight = panel.AnchorTop = panel.AnchorBottom = 1;
        panel.GrowHorizontal = GrowDirection.Begin;
        panel.GrowVertical = GrowDirection.Begin;
        panel.OffsetLeft = panel.OffsetRight = -12;
        panel.OffsetTop = panel.OffsetBottom = -10;
        AddChild(panel);

        _list = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _list.AddThemeConstantOverride("separation", 2);
        panel.AddChild(_list);

        _resume = Entry("Reprendre", "play", () => Shell.ResumeGame());
        _new = Entry("Nouvelle partie", "play", () => Shell.RequestNewGame());
        _quit = Entry("Quitter", "exit", () => Shell.RequestQuit());
        _note = UiKit.Lbl("", UiKit.Muted, wrap: true);
        _note.HorizontalAlignment = HorizontalAlignment.Center;
        _list.AddChild(_note);
        _list.MoveChild(_note, _resume.GetIndex() + 1);
    }

    private Button Entry(string text, string icon, Action onPressed)
    {
        var button = IconButton(text, icon, onPressed, 0, 18);
        _list.AddChild(button);
        _menu.Add(button);
        return button;
    }

    protected override void OnOpening()
    {
        var s = Shell.Session;
        var resumable = Shell.HasProgress;
        _resume.Visible = resumable;
        _note.Visible = resumable;
        if (resumable)
        {
            var finished = s.Status == GameStatus.Finished;
            _resume.Text = finished ? "Revoir le bilan" : $"Reprendre (jour {s.Guild.Day}/{s.Data.Rules.Days})";
            _resume.TooltipText = "Partie en cours, conservée en mémoire seulement : elle n'est pas sauvegardée sur le disque.";
            _note.Text = "Partie en mémoire, non sauvegardée";
        }
        SetFocusRing(_menu);
    }

    /// <summary>Le premier choix utile : reprendre une partie en mémoire, sinon en commencer une.</summary>
    protected override Control InitialFocus() => _resume.Visible ? _resume : _new;

    protected override bool OnCancel() => true;   // Échap ne ferme pas le titre et ne quitte pas

    /// <summary>Rafraîchit le menu sans le rouvrir (retour de l'aide, des paramètres, etc.).</summary>
    public void RefreshMenu() => OnOpening();
}
