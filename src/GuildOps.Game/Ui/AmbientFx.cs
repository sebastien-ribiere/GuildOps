using Godot;

namespace GuildOps.Game.Ui;

/// <summary>
/// Animation d'ambiance du hall en boucle (kit B5.2, <c>ambient/</c>) : lueur du foyer, scintillement du trésor. Purement décorative :
/// n'intercepte aucun événement (<c>MouseFilter.Ignore</c>, pas de focus), n'appartient à aucune zone cliquable et ne touche ni Core ni RNG.
/// Placée en coordonnées du DÉCOR natif (640×360) ; le décalage vertical du hall est appliqué par l'appelant.
/// « Réduire les animations » la masque et l'arrête immédiatement (le décor du hall, lui, reste intact).
/// </summary>
public partial class AmbientFx : TextureRect
{
    private AtlasTexture _atlas = null!;
    private int _frames, _w, _h, _frame = -1;
    private double _frameSeconds, _clock;
    private DisplaySettings _settings = null!;

    public bool Reduced => _settings.ReduceMotion;

    public static AmbientFx Create(DisplaySettings settings, string file, int frames, int w, int h, int frameMs, Vector2 centreInDecor, int topOffset, double phase = 0)
    {
        var fx = new AmbientFx { _settings = settings, _frames = frames, _w = w, _h = h, _frameSeconds = frameMs / 1000.0, _clock = phase };
        fx._atlas = Art.Frame(UiKit.Tex($"{Art.Root}ambient/{file}"), 0, w, h);
        fx.Texture = fx._atlas;
        fx.Size = fx.CustomMinimumSize = new Vector2(w, h);
        // Centre de l'effet = point d'ancrage du décor ; le fond est affiché à y = -topOffset.
        fx.Position = new Vector2(centreInDecor.X - w / 2f, centreInDecor.Y - h / 2f - topOffset);
        fx.MouseFilter = MouseFilterEnum.Ignore;
        fx.FocusMode = FocusModeEnum.None;
        fx.TextureFilter = TextureFilterEnum.Nearest;
        fx.Advance();
        return fx;
    }

    public override void _EnterTree()
    {
        _settings.Changed += OnSettingsChanged;
        OnSettingsChanged();
    }

    public override void _ExitTree() => _settings.Changed -= OnSettingsChanged;

    private void OnSettingsChanged()
    {
        Visible = !_settings.ReduceMotion;
        SetProcess(!_settings.ReduceMotion);
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        Advance();
    }

    private void Advance()
    {
        var frame = (int)(_clock / _frameSeconds) % _frames;
        if (frame == _frame) return;
        _frame = frame;
        _atlas.Region = new Rect2(frame * _w, 0, _w, _h);
    }
}
