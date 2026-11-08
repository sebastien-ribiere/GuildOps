using Godot;

namespace GuildOps.Game.Ui;

/// <summary>
/// Écran superposé (titre, introduction, aide, paramètres, crédits, fin de partie, confirmation). Plein cadre 640×360, il intercepte la souris
/// et piège le focus clavier sur ses propres contrôles : rien de ce qui est dessous ne peut être activé tant qu'il est ouvert.
/// Contrat commun (docs/b5-3) :
/// <list type="bullet">
/// <item>Échap ferme l'écran du dessus (jamais plusieurs à la fois), sans réinitialiser la partie ;</item>
/// <item>à la fermeture, le focus revient au contrôle qui avait ouvert l'écran ;</item>
/// <item>un écran n'écrit jamais dans Core : il affiche et délègue à <see cref="GameShell"/>.</item>
/// </list>
/// </summary>
public abstract partial class Overlay : Control
{
    protected GameShell Shell { get; private set; } = null!;
    private Control? _returnFocus;

    public bool IsOpen => Visible;

    public void Bind(GameShell shell, string name)
    {
        Shell = shell;
        Name = name;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Build();
    }

    protected abstract void Build();

    /// <summary>Appelé juste avant l'affichage : met à jour le contenu (aucune mutation de la session).</summary>
    protected virtual void OnOpening() { }

    protected virtual void OnClosed() { }

    /// <summary>Contrôle qui reçoit le focus à l'ouverture.</summary>
    protected abstract Control InitialFocus();

    /// <summary>Réaction à Échap. Retourne vrai si l'événement est consommé. Par défaut : fermer l'écran.</summary>
    protected virtual bool OnCancel()
    {
        Close();
        return true;
    }

    public void Open()
    {
        if (Visible) return;
        _returnFocus = GetViewport().GuiGetFocusOwner();
        OnOpening();
        GetParent().MoveChild(this, -1);   // le dernier ouvert est dessiné au-dessus des autres
        Visible = true;
        Shell.PushOverlay(this);
        InitialFocus().GrabFocus();
    }

    public void Close()
    {
        if (!Visible) return;
        Visible = false;
        Shell.PopOverlay(this);
        OnClosed();
        var target = _returnFocus;
        _returnFocus = null;
        if (target is not null && IsInstanceValid(target) && target.IsInsideTree() && target.IsVisibleInTree()) target.GrabFocus();
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible || Shell.TopOverlay != this) return;
        if (@event.IsActionPressed("ui_cancel") && !@event.IsEcho() && OnCancel()) GetViewport().SetInputAsHandled();
    }

    // ---- Aides de construction ----------------------------------------------------------------------------------------

    /// <summary>
    /// Ferme la boucle de focus sur les contrôles donnés (Tab, Maj+Tab et flèches restent dans l'écran). À rappeler quand l'ensemble des contrôles
    /// utilisables change ; les boutons désactivés ne reçoivent pas le focus clavier et sont écartés.
    /// </summary>
    protected static void SetFocusRing(IEnumerable<Control> controls)
    {
        var ring = controls.Where(c => c.Visible && c is not BaseButton { Disabled: true }).ToList();
        foreach (var c in controls) if (!ring.Contains(c)) c.FocusMode = FocusModeEnum.None; else c.FocusMode = FocusModeEnum.All;
        for (var i = 0; i < ring.Count; i++)
        {
            var next = ring[(i + 1) % ring.Count].GetPath();
            var prev = ring[(i + ring.Count - 1) % ring.Count].GetPath();
            ring[i].FocusNext = next;
            ring[i].FocusPrevious = prev;
            ring[i].SetFocusNeighbor(Side.Right, next);
            ring[i].SetFocusNeighbor(Side.Bottom, next);
            ring[i].SetFocusNeighbor(Side.Left, prev);
            ring[i].SetFocusNeighbor(Side.Top, prev);
        }
    }

    /// <summary>Voile sombre plein cadre (la souris est déjà interceptée par l'écran lui-même).</summary>
    protected Control AddDim(float opacity)
    {
        var dim = new ColorRect { Color = new Color(UiKit.Ink, opacity), MouseFilter = MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);
        return dim;
    }

    protected CenterContainer AddCenter()
    {
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        return center;
    }

    /// <summary>Bouton de menu avec pictogramme 16×16 du kit B5.3 (le texte reste toujours écrit).</summary>
    protected static Button IconButton(string text, string icon, Action onPressed, float minWidth = 0, float height = 20)
    {
        var button = UiKit.Btn(text, onPressed, minWidth, height);
        button.Icon = UiKit.Tex($"{B53.Root}icons/{icon}_16.png");
        button.Alignment = HorizontalAlignment.Left;
        button.AddThemeConstantOverride("h_separation", 4);
        return button;
    }
}
