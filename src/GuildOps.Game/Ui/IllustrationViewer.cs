using Godot;
using GuildOps.Core.Domain;

namespace GuildOps.Game.Ui;

/// <summary>
/// Visionneuse d'illustration : affiche l'illustration HD 384×256 à l'échelle 1 (ratio 3:2 intact, sans lissage) avec le titre et la
/// catégorie en texte. Les fiches (panneau d'affectation 208 px, détail de mission) n'ont pas la place d'un 384×256 sans repousser le
/// verdict et les boutons ; la visionneuse est donc ouverte à la demande (clic ou Entrée sur l'illustration).
/// Modale légère : Échap / Entrée / clic la ferme, le focus clavier est piégé sur son bouton. Aucune mutation, aucun tirage.
/// </summary>
public partial class IllustrationViewer : Control
{
    private TextureRect _picture = null!;
    private Label _title = null!, _caption = null!;
    private Button _close = null!;
    private Control? _returnFocus;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        Name = "IllustrationViewer";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect { Color = new Color(UiKit.Ink, 0.82f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dim.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) Close(); };
        AddChild(dim);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = UiKit.Panel("parchment", 6);
        center.AddChild(panel);
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 3);
        panel.AddChild(box);

        _title = UiKit.AsTitle(UiKit.Lbl("", UiKit.Ink, clip: true));
        box.AddChild(_title);
        _picture = Art.Picture(Art.Hd("promener-medor"), new Vector2(Art.HdW, Art.HdH));
        box.AddChild(_picture);
        _caption = UiKit.Lbl("", UiKit.MutedOnLight, wrap: true);
        _caption.CustomMinimumSize = new Vector2(Art.HdW, 0);
        box.AddChild(_caption);
        _close = UiKit.Btn("Fermer (Échap)", Close);
        box.AddChild(_close);
        // Focus piégé : tant que la visionneuse est ouverte, Tab/flèches restent sur son unique bouton.
        var self = _close.GetPath();
        foreach (var side in new[] { Side.Left, Side.Right, Side.Top, Side.Bottom }) _close.SetFocusNeighbor(side, self);
        _close.FocusNext = _close.FocusPrevious = self;
    }

    /// <summary>Ouvre l'illustration du modèle de contrat. Le focus clavier revient à l'appelant à la fermeture.</summary>
    public void Open(QuestTemplate t)
    {
        _returnFocus = GetViewport().GuiGetFocusOwner();
        _picture.Texture = Art.Hd(t.Id);
        _title.Text = t.Title;
        _caption.Text = $"{Names.Category(t.Category)} · {Names.Skill(t.Skill)} niveau {t.MinLevel} requis. Illustration d'ambiance : elle ne préjuge pas du résultat.";
        Visible = true;
        _close.GrabFocus();
    }

    public void Close()
    {
        if (!Visible) return;
        Visible = false;
        if (_returnFocus is { } f && IsInstanceValid(f) && f.IsInsideTree() && f.IsVisibleInTree()) f.GrabFocus();
        _returnFocus = null;
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;
        if (@event.IsActionPressed("ui_cancel")) { Close(); GetViewport().SetInputAsHandled(); }
    }
}
