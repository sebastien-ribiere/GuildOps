using Godot;

namespace GuildOps.Game.Ui;

/// <summary>
/// Confirmation modale d'une action irréversible (nouvelle partie qui efface la partie en mémoire, quitter sans sauvegarde).
/// Le focus initial est sur le choix SÛR (« Annuler ») ; Échap annule. Aucune action n'est exécutée avant la confirmation.
/// </summary>
public partial class ConfirmDialog : Overlay
{
    private Label _title = null!, _body = null!;
    private Button _yes = null!, _no = null!;
    private Action? _onYes;

    protected override void Build()
    {
        AddDim(0.82f);
        var center = AddCenter();
        var panel = UiKit.Panel("parchment", 8);
        center.AddChild(panel);
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 4);
        panel.AddChild(box);
        _title = UiKit.AsTitle(UiKit.Lbl("", UiKit.Ink, wrap: true));
        _title.CustomMinimumSize = new Vector2(300, 0);
        box.AddChild(_title);
        _body = UiKit.Lbl("", UiKit.Ink, wrap: true);
        _body.CustomMinimumSize = new Vector2(300, 0);
        box.AddChild(_body);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        row.AddThemeConstantOverride("separation", 6);
        box.AddChild(row);
        _no = UiKit.Btn("Annuler", Close, 90);
        _yes = UiKit.Btn("Confirmer", Confirm, 120);
        row.AddChild(_no);
        row.AddChild(_yes);
        SetFocusRing([_no, _yes]);
    }

    public string Title => _title.Text;

    /// <summary>Affiche la question ; <paramref name="onConfirm"/> n'est appelée qu'après le choix explicite, une fois la boîte fermée.</summary>
    public void Ask(string title, string body, string confirmText, Action onConfirm, string cancelText = "Annuler")
    {
        _title.Text = title;
        _body.Text = body;
        _yes.Text = confirmText;
        _no.Text = cancelText;
        _onYes = onConfirm;
        Open();
    }

    private void Confirm()
    {
        var action = _onYes;
        _onYes = null;
        Close();
        action?.Invoke();
    }

    protected override void OnClosed() => _onYes = null;

    protected override Control InitialFocus() => _no;
}
