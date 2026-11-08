using Godot;

namespace GuildOps.Game.Ui;

public enum FxKind { Succes, Echec, Recompense, Incident }

/// <summary>
/// Effet cosmétique « une fois » : une bande de six images de 32×32 jouée de 0 à 5, sans boucle (kit B5.2, <c>vfx/</c>).
/// Il n'est déclenché que par des événements métier DÉJÀ produits par Core (<see cref="GameShell.MissionResolved"/>), jamais par un
/// rafraîchissement ou une navigation. Il ne tire rien, ne mute rien, ne bloque aucune entrée (<c>MouseFilter.Ignore</c>) et s'interrompt
/// à la première touche ou au premier clic, ou quand son parent est reconstruit.
/// Les effets d'un même résultat s'enchaînent dans l'ordre (verdict, puis prime, puis incident) dans un seul emplacement.
/// « Réduire les animations » : aucun effet n'est lancé, et un effet en cours s'interrompt aussitôt ; le verdict reste affiché en texte.
/// </summary>
public partial class CosmeticFx : TextureRect
{
    private const int Frames = 6, Size32 = 32;

    /// <summary>Journal des effets lancés (vérification automatisée : un effet par événement réel, aucun sur un simple rafraîchissement).</summary>
    public static List<FxKind> Played { get; } = [];

    /// <summary>Nombre d'emplacements d'effet actuellement à l'écran (0 quand tout est terminé ou interrompu).</summary>
    public static int Active { get; private set; }

    private DisplaySettings _settings = null!;

    public override void _EnterTree()
    {
        Active++;
        _settings.Changed += OnSettingsChanged;
    }

    public override void _ExitTree()
    {
        Active--;
        _settings.Changed -= OnSettingsChanged;
    }

    private void OnSettingsChanged()
    {
        if (_settings.ReduceMotion) QueueFree();
    }

    private static double FrameSeconds(FxKind kind) => kind == FxKind.Succes ? 0.09 : 0.10;

    private static string File(FxKind kind) => kind switch
    {
        FxKind.Succes => "succes", FxKind.Echec => "echec", FxKind.Recompense => "recompense", _ => "incident",
    };

    private readonly Queue<FxKind> _queue = new();
    private AtlasTexture? _atlas;
    private Texture2D? _strip;
    private double _clock;
    private int _frame;
    private bool _running;

    /// <summary>Crée l'emplacement (32×32, ancré en haut à droite du parent) et met les effets en file ; le premier démarre aussitôt.</summary>
    public static CosmeticFx? Attach(Control parent, IEnumerable<FxKind> sequence, DisplaySettings settings)
    {
        if (settings.ReduceMotion) return null;
        var fx = new CosmeticFx
        {
            _settings = settings,
            CustomMinimumSize = new Vector2(Size32, Size32), Size = new Vector2(Size32, Size32),
            MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Nearest, TopLevel = false,
            FocusMode = FocusModeEnum.None,
        };
        fx.AnchorLeft = fx.AnchorRight = 1;
        fx.OffsetLeft = -Size32 - 2; fx.OffsetRight = -2;
        fx.OffsetTop = -1; fx.OffsetBottom = Size32 - 1;
        foreach (var kind in sequence) fx._queue.Enqueue(kind);
        parent.AddChild(fx);
        fx.Next();
        return fx;
    }

    private void Next()
    {
        if (_queue.Count == 0) { QueueFree(); return; }
        var kind = _queue.Dequeue();
        _strip = UiKit.Tex($"{Art.Root}vfx/{File(kind)}_6x32.png");
        _atlas = Art.Frame(_strip, 0, Size32, Size32);
        Texture = _atlas;
        _frame = 0; _clock = 0; _running = true;
        Played.Add(kind);
        _current = kind;
    }

    private FxKind _current;

    public override void _Process(double delta)
    {
        if (!_running || _atlas is null) return;
        _clock += delta;
        var frame = (int)(_clock / FrameSeconds(_current));
        if (frame >= Frames) { Next(); return; }
        if (frame == _frame) return;
        _frame = frame;
        _atlas.Region = new Rect2(frame * Size32, 0, Size32, Size32);
    }

    /// <summary>Interruptible : une touche ou un clic arrête l'effet, sans consommer l'événement (le bouton visé le reçoit quand même).</summary>
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true } or InputEventMouseButton { Pressed: true }) QueueFree();
    }
}
