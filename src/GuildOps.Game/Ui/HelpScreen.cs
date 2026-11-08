using Godot;

namespace GuildOps.Game.Ui;

/// <summary>
/// Aide et tutoriel en quatre pages (illustrations B5.3 + texte Godot de <see cref="HelpContent"/>). Facultatif, jamais imposé : consultable
/// depuis le titre, le HUD (« Aide ») et F1, à tout moment. Purement informatif : aucune mutation de la session, aucun faux contrat.
/// Précédent / Suivant, indicateur « n/4 », Échap ou « Fermer », focus piégé puis restitué au contrôle d'origine.
/// </summary>
public partial class HelpScreen : Overlay
{
    private int _page;
    private IReadOnlyList<HelpPage> _pages = [];
    private Label _heading = null!, _progress = null!, _caption = null!;
    private TextureRect _picture = null!;
    private RichTextLabel _body = null!;
    private ScrollContainer _scroll = null!;
    private Button _prev = null!, _next = null!, _replay = null!, _close = null!;
    private readonly List<ColorRect> _dots = [];

    public int Page => _page;
    public int PageCount => _pages.Count;
    public RichTextLabel Body => _body;
    public ScrollContainer BodyScroll => _scroll;
    public Button PreviousButton => _prev;
    public Button NextButton => _next;

    protected override void Build()
    {
        AddDim(0.84f);
        var center = AddCenter();
        var panel = UiKit.Panel("parchment", 8);
        center.AddChild(panel);
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 3);
        panel.AddChild(box);

        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 4);
        box.AddChild(head);
        _heading = UiKit.AsTitle(UiKit.Lbl("", UiKit.Ink, clip: true));
        _heading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(_heading);
        for (var i = 0; i < B53.TutorialPages; i++)
        {
            var dot = new ColorRect { CustomMinimumSize = new Vector2(6, 6), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
            _dots.Add(dot);
            head.AddChild(dot);
        }
        _progress = UiKit.AsTitle(UiKit.Lbl("", UiKit.Ink));
        head.AddChild(_progress);
        box.AddChild(B53.OrnamentDivider());

        var middle = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        middle.AddThemeConstantOverride("separation", 8);
        box.AddChild(middle);
        var left = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        left.AddThemeConstantOverride("separation", 2);
        middle.AddChild(left);
        _picture = B53.Picture(B53.Tutorial(1));
        left.AddChild(_picture);
        _caption = UiKit.Lbl("", UiKit.MutedOnLight, wrap: true);
        _caption.CustomMinimumSize = new Vector2(192, 0);
        left.AddChild(_caption);

        _scroll = UiKit.Scroll();
        _scroll.CustomMinimumSize = new Vector2(352, 150);
        middle.AddChild(_scroll);
        _body = UiKit.RichFit(onLight: true);
        _scroll.AddChild(_body);

        var keys = UiKit.IconText("keyboard", HelpContent.KeyboardLine, UiKit.MutedOnLight, wrap: true, folder: B53.Icons);
        keys.CustomMinimumSize = new Vector2(552, 0);
        box.AddChild(keys);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        box.AddChild(row);
        _prev = IconButton("Précédent", "back", () => Go(_page - 1), 90, 20);
        _next = UiKit.Btn("Suivant", () => { if (_page >= _pages.Count - 1) Close(); else Go(_page + 1); }, 90);
        row.AddChild(_prev);
        row.AddChild(_next);
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _replay = IconButton("Revoir l'introduction", "info", () => Shell.Intro.OpenReplay(), 150, 20);
        _close = UiKit.Btn("Fermer (Échap)", Close, 110);
        row.AddChild(_replay);
        row.AddChild(_close);
    }

    protected override void OnOpening()
    {
        _pages = HelpContent.Pages(Shell.Session);
        ShowPage(0);
    }

    /// <summary>Aller à une page (0 à 3) ; le focus ne reste jamais sur un bouton devenu inutilisable.</summary>
    public void Go(int page)
    {
        ShowPage(Math.Clamp(page, 0, _pages.Count - 1));
        if (GetViewport().GuiGetFocusOwner() is not { } f || f.FocusMode == FocusModeEnum.None || !IsAncestorOf(f)) _next.GrabFocus();
    }

    private void ShowPage(int page)
    {
        _page = page;
        var p = _pages[page];
        _heading.Text = p.Title;
        _picture.Texture = B53.Tutorial(page + 1);
        _caption.Text = p.Caption ?? "";
        _caption.Visible = p.Caption is not null;
        _body.Text = p.Body;
        _scroll.ScrollVertical = 0;
        _progress.Text = $"{page + 1}/{_pages.Count}";
        for (var i = 0; i < _dots.Count; i++) _dots[i].Color = i == page ? UiKit.Ink : new Color(0.72f, 0.60f, 0.40f);
        _prev.Disabled = page == 0;
        _next.Text = page == _pages.Count - 1 ? "Terminer" : "Suivant";
        SetFocusRing([_prev, _next, _replay, _close]);
    }

    protected override Control InitialFocus() => _next;
}
