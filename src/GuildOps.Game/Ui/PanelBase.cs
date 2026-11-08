using Godot;

namespace GuildOps.Game.Ui;

/// <summary>Panneau de l'interface : construit une fois, rafraîchi depuis l'état de Core à chaque changement.</summary>
public abstract partial class PanelBase : Control
{
    protected GameShell Shell { get; private set; } = null!;

    public void Bind(GameShell shell)
    {
        Shell = shell;
        SetAnchorsPreset(LayoutPreset.FullRect);
        Build();
    }

    protected abstract void Build();
    public abstract void Refresh();

    /// <summary>Donne le focus clavier au premier contrôle utile quand la vue devient visible.</summary>
    public virtual void FocusFirst() { }

    protected static Button? FirstButton(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button { Disabled: false, Visible: true } b && b.FocusMode == FocusModeEnum.All) return b;
            if (FirstButton(child) is { } found) return found;
        }
        return null;
    }
}
