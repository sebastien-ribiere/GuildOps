using Godot;

namespace GuildOps.Game.Ui;

/// <summary>Thème 16 bits : police, 9-slices, boutons et fabriques de widgets. Aucune logique métier.</summary>
public static class UiKit
{
    public const string AssetRoot = "res://Assets/";

    // Palette tirée des PNG fournis (panel_*_9slice, button_*).
    public static readonly Color Ink = new(27 / 255f, 21 / 255f, 39 / 255f);
    public static readonly Color Cream = new(0.95f, 0.89f, 0.75f);
    public static readonly Color Gold = new(0.94f, 0.75f, 0.31f);
    public static readonly Color Muted = new(0.55f, 0.50f, 0.58f);
    public static readonly Color GoodOnLight = new(0.14f, 0.40f, 0.12f);
    public static readonly Color BadOnLight = new(0.62f, 0.10f, 0.10f);
    public static readonly Color GoodOnDark = new(0.56f, 0.85f, 0.44f);
    public static readonly Color BadOnDark = new(0.97f, 0.45f, 0.45f);

    private static readonly Dictionary<string, Texture2D> Cache = [];

    public static Font Font { get; private set; } = null!;
    public static Theme Theme { get; private set; } = null!;

    public static Texture2D Tex(string relativePath)
    {
        if (!Cache.TryGetValue(relativePath, out var texture))
            Cache[relativePath] = texture = GD.Load<Texture2D>(AssetRoot + relativePath);
        return texture;
    }

    public static void Init()
    {
        var font = GD.Load<FontFile>(AssetRoot + "fonts/Silkscreen-Regular.ttf");
        // Police pixel : pas de lissage, pas de positionnement sous-pixel.
        font.Antialiasing = TextServer.FontAntialiasing.None;
        font.SubpixelPositioning = TextServer.SubpixelPositioning.Disabled;
        font.Hinting = TextServer.Hinting.None;
        font.GenerateMipmaps = false;
        Font = font;

        var theme = new Theme { DefaultFont = font, DefaultFontSize = 8 };
        theme.SetColor("font_color", "Label", Cream);
        theme.SetColor("default_color", "RichTextLabel", Cream);
        theme.SetFontSize("normal_font_size", "RichTextLabel", 8);
        theme.SetFontSize("bold_font_size", "RichTextLabel", 8);
        theme.SetFont("bold_font", "RichTextLabel", font);
        theme.SetConstant("separation", "VBoxContainer", 3);
        theme.SetConstant("separation", "HBoxContainer", 3);
        theme.SetConstant("line_spacing", "Label", 1);
        theme.SetConstant("line_separation", "RichTextLabel", 1);

        theme.SetStylebox("normal", "Button", ButtonBox("button_normal_128x32.png"));
        theme.SetStylebox("hover", "Button", ButtonBox("button_hover_128x32.png"));
        theme.SetStylebox("hover_pressed", "Button", ButtonBox("button_hover_128x32.png"));
        theme.SetStylebox("pressed", "Button", ButtonBox("button_pressed_128x32.png"));
        theme.SetStylebox("disabled", "Button", ButtonBox("button_disabled_128x32.png"));
        theme.SetStylebox("focus", "Button", FocusBox());
        foreach (var name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
            theme.SetColor(name, "Button", Cream);
        theme.SetColor("font_disabled_color", "Button", Muted);
        theme.SetConstant("h_separation", "Button", 3);
        Theme = theme;
    }

    public static StyleBoxTexture Nine(string relativePath, int texMargin, int content, Color? modulate = null)
    {
        var box = new StyleBoxTexture { Texture = Tex(relativePath) };
        box.SetTextureMarginAll(texMargin);
        box.SetContentMarginAll(content);
        if (modulate is { } m) box.ModulateColor = m;
        return box;
    }

    private static StyleBoxTexture ButtonBox(string file)
    {
        var box = Nine("ui/" + file, 6, 0);
        box.ContentMarginLeft = 6; box.ContentMarginRight = 6; box.ContentMarginTop = 3; box.ContentMarginBottom = 3;
        return box;
    }

    public static StyleBoxFlat FocusBox() => new()
    {
        BgColor = new Color(0, 0, 0, 0),
        BorderColor = Gold,
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        DrawCenter = false,
    };

    // ---- Fabriques -------------------------------------------------------------------------------------------

    public static Label Lbl(string text, Color? color = null, bool wrap = false, bool clip = false)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        if (color is { } c) label.AddThemeColorOverride("font_color", c);
        if (wrap) label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (clip) label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        return label;
    }

    public static TextureRect Icon(string name, float scale = 1f) => new()
    {
        Texture = Tex($"icons/{name}_16.png"),
        CustomMinimumSize = new Vector2(16 * scale, 16 * scale),
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    public static HBoxContainer IconText(string icon, string text, Color? color = null)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 1);
        row.AddChild(Icon(icon));
        var label = Lbl(text, color);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(label);
        return row;
    }

    public static PanelContainer Panel(string kind, int content = 6)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Nine($"ui/panel_{kind}_9slice.png", 8, content));
        return panel;
    }

    public static Button Btn(string text, Action onPressed, float minWidth = 0)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(minWidth, 18), ClipText = false };
        button.Pressed += onPressed;
        return button;
    }

    public static RichTextLabel Rich(bool onLight)
    {
        var rich = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = true,
            MouseFilter = Control.MouseFilterEnum.Pass,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        rich.AddThemeColorOverride("default_color", onLight ? Ink : Cream);
        return rich;
    }

    public static Color Good(bool onLight) => onLight ? GoodOnLight : GoodOnDark;
    public static Color Bad(bool onLight) => onLight ? BadOnLight : BadOnDark;

    public static string Hex(Color c) => c.ToHtml(false);

    /// <summary>Retire tous les enfants d'un conteneur (libération immédiate pour éviter les doublons de focus).</summary>
    public static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
