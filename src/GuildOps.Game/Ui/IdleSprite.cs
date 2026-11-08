using Godot;

namespace GuildOps.Game.Ui;

/// <summary>Animation d'attente décorative (4 frames de 32×48). Indépendante de la simulation : aucun tirage, aucune mutation.</summary>
public partial class IdleSprite : TextureRect
{
    private const int Frames = 4;
    private const double FrameTime = 0.35;
    private AtlasTexture _atlas = null!;
    private double _clock;
    private int _frame = -1;

    public void Setup(string heroId, int phase)
    {
        _atlas = new AtlasTexture { Atlas = UiKit.Tex($"sprites/{heroId}_idle.png"), Region = new Rect2(0, 0, 32, 48) };
        Texture = _atlas;
        CustomMinimumSize = new Vector2(32, 48);
        Size = new Vector2(32, 48);
        MouseFilter = MouseFilterEnum.Ignore;
        _clock = phase * 0.4;
        Advance();
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        Advance();
    }

    private void Advance()
    {
        var frame = (int)(_clock / FrameTime) % Frames;
        if (frame == _frame) return;
        _frame = frame;
        _atlas.Region = new Rect2(frame * 32, 0, 32, 48);
    }
}
