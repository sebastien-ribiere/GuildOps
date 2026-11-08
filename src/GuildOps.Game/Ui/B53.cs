using Godot;

namespace GuildOps.Game.Ui;

/// <summary>Fabrique de textures du kit B5.3 (<c>res://Assets/b5_3/</c>) : titre, scènes narratives, tutoriel, branding. Présentation pure.</summary>
public static class B53
{
    public const string Root = "b5_3/";
    public const int TutorialPages = 4;

    public static Texture2D Title => UiKit.Tex($"{Root}backgrounds/title_640x360.png");
    public static Texture2D Intro => UiKit.Tex($"{Root}scenes/intro_384x256.png");
    public static Texture2D Victory => UiKit.Tex($"{Root}scenes/victory_384x256.png");
    public static Texture2D Defeat => UiKit.Tex($"{Root}scenes/defeat_384x256.png");
    public static Texture2D Crest(int size) => UiKit.Tex($"{Root}branding/guild_crest_{size}.png");
    public static Texture2D Divider => UiKit.Tex($"{Root}ui/divider_160x8.png");
    public static Texture2D Ribbon(string kind) => UiKit.Tex($"{Root}ui/ribbon_{kind}_192x32.png");

    /// <summary>Illustration de l'étape de tutoriel (1 à 4), 192×128, à afficher à l'échelle 1.</summary>
    public static Texture2D Tutorial(int page) => UiKit.Tex($"{Root}tutorial/step_{page}_192x128.png");

    /// <summary>Image décorative : jamais interactive, sans lissage, taille native.</summary>
    public static TextureRect Picture(Texture2D texture) => Art.Picture(texture, texture.GetSize());

    /// <summary>Ruban sans texte (192×32) avec un libellé Godot centré par-dessus.</summary>
    public static Control RibbonWithText(string kind, string text)
    {
        var holder = new Control { CustomMinimumSize = new Vector2(192, 32), MouseFilter = Control.MouseFilterEnum.Ignore };
        holder.AddChild(Picture(Ribbon(kind)));
        var label = UiKit.AsTitle(UiKit.Lbl(text, UiKit.Cream));
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        label.OffsetLeft = 22; label.OffsetRight = -22;
        holder.AddChild(label);
        return holder;
    }

    /// <summary>Séparateur ornemental 160×8 (aide, crédits).</summary>
    public static Control OrnamentDivider()
    {
        var row = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(Picture(Divider));
        return row;
    }
}
