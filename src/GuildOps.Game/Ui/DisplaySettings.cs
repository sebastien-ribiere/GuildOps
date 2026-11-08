using Godot;

namespace GuildOps.Game.Ui;

/// <summary>
/// Préférences d'AFFICHAGE du joueur : fenêtré / plein écran, échelle entière de la fenêtre, « Réduire les animations ».
/// Totalement séparées de Core : aucune donnée de partie n'est lue ni écrite ici, et aucun réglage ne peut modifier un résultat, un tirage ou une
/// règle (les effets cosmétiques s'en abstiennent, c'est tout). Enregistrées avec <see cref="ConfigFile"/> quand un chemin est fourni ;
/// sans chemin (lancements automatisés), rien n'est lu ni écrit sur le disque. Aucun réglage audio : GuildOps est silencieux.
/// </summary>
public sealed class DisplaySettings(Window window, string? path)
{
    public const string Section = "display";
    public const int DefaultScale = 2;
    public const int LogicalWidth = 640, LogicalHeight = 360;
    private const int DecorationAllowance = 48;   // barre de titre et bordures retirées de la hauteur utilisable

    public bool Fullscreen { get; private set; }
    public int Scale { get; private set; } = DefaultScale;
    public bool ReduceMotion { get; private set; }

    /// <summary>Levé après chaque changement (les effets cosmétiques s'y abonnent pour réagir immédiatement).</summary>
    public event Action? Changed;

    /// <summary>Plus grande échelle entière dont la fenêtre tient dans l'écran (au moins 1).</summary>
    public int MaxScale
    {
        get
        {
            var area = DisplayServer.ScreenGetUsableRect(window.CurrentScreen);
            return Math.Max(1, Math.Min(area.Size.X / LogicalWidth, (area.Size.Y - DecorationAllowance) / LogicalHeight));
        }
    }

    public Vector2I WindowSize => new(LogicalWidth * Scale, LogicalHeight * Scale);

    /// <summary>Lit le fichier s'il existe. Un fichier absent, illisible ou aux valeurs absurdes redonne les réglages par défaut. Retourne vrai si un fichier a été lu.</summary>
    public bool Load()
    {
        if (path is null) return false;
        var file = new ConfigFile();
        if (file.Load(path) != Error.Ok) return false;
        Fullscreen = ReadBool(file, "fullscreen");
        var scale = file.GetValue(Section, "scale", DefaultScale);
        Scale = scale.VariantType == Variant.Type.Int ? Math.Clamp((int)scale, 1, MaxScale) : Math.Min(DefaultScale, MaxScale);
        ReduceMotion = ReadBool(file, "reduce_motion");
        return true;
    }

    private static bool ReadBool(ConfigFile file, string key)
    {
        var value = file.GetValue(Section, key, false);
        return value.VariantType == Variant.Type.Bool && (bool)value;
    }

    public void Save()
    {
        if (path is null) return;
        var file = new ConfigFile();
        file.SetValue(Section, "fullscreen", Fullscreen);
        file.SetValue(Section, "scale", Scale);
        file.SetValue(Section, "reduce_motion", ReduceMotion);
        var error = file.Save(path);
        if (error != Error.Ok) GD.PushWarning($"Préférences d'affichage non enregistrées ({error}) : {path}");
    }

    /// <summary>Applique plein écran / taille de fenêtre. Le rendu reste en 640×360 logique, filtrage au plus proche, échelle entière (project.godot).</summary>
    public void ApplyWindow()
    {
        if (Fullscreen)
        {
            window.Mode = Window.ModeEnum.Fullscreen;
            return;
        }
        window.Mode = Window.ModeEnum.Windowed;
        window.Size = WindowSize;
        var area = DisplayServer.ScreenGetUsableRect(window.CurrentScreen);
        window.Position = area.Position + (area.Size - WindowSize) / 2;
    }

    public void SetFullscreen(bool on)
    {
        if (Fullscreen == on) return;
        Fullscreen = on;
        Commit(applyWindow: true);
    }

    public void SetScale(int scale)
    {
        scale = Math.Clamp(scale, 1, MaxScale);
        if (Scale == scale) return;
        Scale = scale;
        Commit(applyWindow: !Fullscreen);
    }

    public void SetReduceMotion(bool on)
    {
        if (ReduceMotion == on) return;
        ReduceMotion = on;
        Commit(applyWindow: false);
    }

    private void Commit(bool applyWindow)
    {
        if (applyWindow) ApplyWindow();
        Save();
        Changed?.Invoke();
    }
}
