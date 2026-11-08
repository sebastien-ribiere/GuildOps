using Godot;

namespace GuildOps.Game.Ui;

/// <summary>
/// Thème 16 bits : une seule famille, Pixeloid Sans 1.0 (GGBotNet, OFL), 9-slices, boutons et fabriques de widgets. Aucune logique métier.
/// <list type="bullet">
/// <item><b>Corps</b> — Regular 9 px : descriptions, journaux, dialogues, toasts. Défaut du thème : <see cref="Lbl"/>, <see cref="Rich"/>.</item>
/// <item><b>Titre</b> — Bold 9 px : HUD, boutons, noms, stats, titres de panneaux, étiquettes du hall. Fabriques <see cref="Title"/> / <see cref="AsTitle{T}"/>.</item>
/// <item><b>Verdict</b> — Bold 18 px (2 × 9) : uniquement les grands verdicts SUCCÈS / ÉCHEC.</item>
/// </list>
/// Seules ces tailles (9 et 18) sont autorisées ; un test statique le vérifie.
/// </summary>
public static class UiKit
{
    public const string AssetRoot = "res://Assets/";

    // Palette tirée des PNG fournis (panel_*_9slice, button_*).
    public static readonly Color Ink = new(27 / 255f, 21 / 255f, 39 / 255f);
    public static readonly Color Cream = new(0.95f, 0.89f, 0.75f);
    public static readonly Color Gold = new(0.94f, 0.75f, 0.31f);
    public static readonly Color Muted = new(0.55f, 0.50f, 0.58f);
    public static readonly Color MutedOnLight = new(0.36f, 0.30f, 0.30f);
    public static readonly Color GoodOnLight = new(0.14f, 0.40f, 0.12f);
    public static readonly Color BadOnLight = new(0.62f, 0.10f, 0.10f);
    public static readonly Color GoodOnDark = new(0.56f, 0.85f, 0.44f);
    public static readonly Color BadOnDark = new(0.97f, 0.45f, 0.45f);
    /// <summary>Focus clavier : cadre vif de 2 px liseré d'encre, lisible sur parchemin clair comme sur panneau sombre.</summary>
    public static readonly Color FocusColor = new(1f, 0.87f, 0.2f);

    /// <summary>Grille native de Pixeloid Sans : taille unique du texte courant, des titres, des boutons et du HUD.</summary>
    public const int TextSize = 9;
    /// <summary>Multiple entier de la grille, réservé aux grands verdicts.</summary>
    public const int VerdictSize = 18;
    public const int TitleSize = TextSize;
    public const int BodySize = TextSize;

    private static readonly Dictionary<string, Texture2D> Cache = [];

    /// <summary>Pixeloid Sans Bold : titres, boutons, HUD, noms, étiquettes du hall, verdicts.</summary>
    public static Font Font { get; private set; } = null!;
    /// <summary>Pixeloid Sans Regular : corps de texte, journaux, descriptions.</summary>
    public static Font BodyFont { get; private set; } = null!;
    public static Theme Theme { get; private set; } = null!;

    public static Texture2D Tex(string relativePath)
    {
        if (!Cache.TryGetValue(relativePath, out var texture))
            Cache[relativePath] = texture = GD.Load<Texture2D>(AssetRoot + relativePath);
        return texture;
    }

    private static FontFile PixelFont(string file)
    {
        var font = GD.Load<FontFile>(AssetRoot + "fonts/" + file);
        // Police pixel : pas de lissage, pas de positionnement sous-pixel.
        font.Antialiasing = TextServer.FontAntialiasing.None;
        font.SubpixelPositioning = TextServer.SubpixelPositioning.Disabled;
        font.Hinting = TextServer.Hinting.None;
        font.GenerateMipmaps = false;
        // Jamais de police système en repli silencieux : un glyphe absent doit se voir (et être détecté par le contrôle de glyphes).
        font.AllowSystemFallback = false;
        return font;
    }

    public static void Init()
    {
        Font = PixelFont("PixeloidSans-Bold.ttf");
        BodyFont = PixelFont("PixeloidSans.ttf");

        var theme = new Theme { DefaultFont = BodyFont, DefaultFontSize = TextSize };
        theme.SetColor("font_color", "Label", Cream);
        theme.SetColor("default_color", "RichTextLabel", Cream);
        theme.SetFont("normal_font", "RichTextLabel", BodyFont);
        theme.SetFontSize("normal_font_size", "RichTextLabel", TextSize);
        theme.SetFont("bold_font", "RichTextLabel", Font);
        theme.SetFontSize("bold_font_size", "RichTextLabel", TextSize);
        theme.SetConstant("separation", "VBoxContainer", 3);
        theme.SetConstant("separation", "HBoxContainer", 3);
        theme.SetConstant("line_spacing", "Label", 0);
        theme.SetConstant("line_separation", "RichTextLabel", 0);

        theme.SetFont("font", "Button", Font);
        theme.SetFontSize("font_size", "Button", TextSize);
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

    public static StyleBoxTexture Nine(string relativePath, int texMargin, int content, Color? modulate = null, int? bottomMargin = null)
    {
        var box = new StyleBoxTexture { Texture = Tex(relativePath) };
        box.SetTextureMarginAll(texMargin);
        if (bottomMargin is { } bottom) box.TextureMarginBottom = bottom;
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

    /// <summary>Cadre de focus clavier : 2 px vifs + liseré d'encre extérieur.</summary>
    public static StyleBoxFlat FocusBox() => new()
    {
        BgColor = new Color(0, 0, 0, 0),
        BorderColor = FocusColor,
        BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
        DrawCenter = false,
        ShadowColor = Ink, ShadowSize = 1,
    };

    /// <summary>Cadre plat (puces, bandeaux) : fond, bordure et marges.</summary>
    public static StyleBoxFlat Flat(Color fill, Color? border = null, int borderWidth = 1, int margin = 2)
    {
        var box = new StyleBoxFlat { BgColor = fill };
        if (border is { } b)
        {
            box.BorderColor = b;
            box.SetBorderWidthAll(borderWidth);
        }
        box.SetContentMarginAll(margin);
        return box;
    }

    // ---- Fabriques -------------------------------------------------------------------------------------------

    /// <summary>Étiquette du hall : Bold 9 px, sans ombre ni contour.</summary>
    public static Label HallLbl(string text, Color color) => AsTitle(Lbl(text, color));

    /// <summary>Texte « corps » (Regular 9 px).</summary>
    public static Label Lbl(string text, Color? color = null, bool wrap = false, bool clip = false)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        if (color is { } c) label.AddThemeColorOverride("font_color", c);
        if (wrap) label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (clip) label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        return label;
    }

    /// <summary>Texte « titre » (Bold 9 px) : titres, badges, HUD.</summary>
    public static Label Title(string text, Color? color = null, bool clip = false)
        => AsTitle(Lbl(text, color ?? Gold, clip: clip));

    /// <summary>Bascule un libellé existant sur le style « titre » (Bold). <paramref name="size"/> : 9 (défaut) ou 18 pour un grand verdict.</summary>
    public static T AsTitle<T>(T label, int size = TextSize) where T : Label
    {
        if (size != TextSize && size != VerdictSize) throw new ArgumentOutOfRangeException(nameof(size), size, "Seules les tailles 9 et 18 sont autorisées.");
        label.AddThemeFontOverride("font", Font);
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    /// <summary>Pictogramme 16×16 : <c>icons/</c> (kit B5) par défaut, ou un autre dossier (ex. <see cref="B53.Icons"/> pour le kit B5.3).</summary>
    public static TextureRect Icon(string name, float scale = 1f, string folder = "icons") => new()
    {
        Texture = Tex($"{folder}/{name}_16.png"),
        CustomMinimumSize = new Vector2(16 * scale, 16 * scale),
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    /// <summary>Icône 16 px + texte. Avec <paramref name="wrap"/>, le texte passe à la ligne sous l'icône alignée en haut.</summary>
    public static HBoxContainer IconText(string icon, string text, Color? color = null, bool wrap = false, string folder = "icons")
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 1);
        var glyph = Icon(icon, 1f, folder);
        if (wrap) glyph.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        row.AddChild(glyph);
        var label = Lbl(text, color, wrap: wrap);
        if (wrap) label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        else label.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(label);
        return row;
    }

    /// <summary>Panneau 9-slice du kit. Les PNG portent une ligne décorative en bas de la zone centrale (rangée 39) : elle est rattachée
    /// à la bordure basse (marge de 9 px) pour ne pas être étirée en bande épaisse sur les grands panneaux.</summary>
    public static PanelContainer Panel(string kind, int content = 6)
    {
        var panel = new PanelContainer();
        var box = Nine($"ui/panel_{kind}_9slice.png", 8, content, bottomMargin: 9);
        // Correctif B5.3 : le centre du PNG contient quelques pixels isolés (« poussière ») qui, étirés, devenaient de gros carrés gris sur les
        // grands panneaux. En mosaïque à l'échelle 1, ils redeviennent un grain discret. Le PNG lui-même n'est pas modifié.
        box.AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile;
        box.AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Tile;
        panel.AddThemeStyleboxOverride("panel", box);
        return panel;
    }

    /// <summary>Fond uni des vues (sinon le gris par défaut du moteur transparaît entre les panneaux).</summary>
    public static readonly Color Backdrop = new(0.11f, 0.08f, 0.18f);

    /// <summary>Pastille de texte (catégorie, étape, état) : fond plein, liseré d'encre, texte au style « titre ».</summary>
    public static Control Pill(string text, Color fill, Color? textColor = null, Color? border = null)
    {
        var chip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        chip.AddThemeStyleboxOverride("panel", Flat(fill, border ?? new Color(Ink, 0.9f), 1, 1));
        chip.AddChild(AsTitle(Lbl(text, textColor ?? Cream)));
        return chip;
    }

    /// <summary>Filet horizontal de 1 px.</summary>
    public static ColorRect Divider(Color color) => new()
    {
        Color = color, CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    /// <summary>Zone défilante verticale uniquement (le texte passe à la ligne ; le clavier fait défiler quand un contrôle reçoit le focus).</summary>
    public static ScrollContainer Scroll() => new()
    {
        HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    };

    /// <summary>Barre de proportion purement graphique (0–100) : n'exprime aucune règle, seulement la valeur donnée par Core.</summary>
    public static Control Bar(int percent, Color fill, Color back)
    {
        percent = Math.Clamp(percent, 0, 100);
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 6), MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 0);
        var done = new ColorRect { Color = fill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = Math.Max(percent, 0.001f), MouseFilter = Control.MouseFilterEnum.Ignore };
        var rest = new ColorRect { Color = back, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = Math.Max(100 - percent, 0.001f), MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(done);
        row.AddChild(rest);
        return row;
    }

    public static Button Btn(string text, Action onPressed, float minWidth = 0, float height = 20)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(minWidth, height), ClipText = false };
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

    /// <summary>Texte BBCode qui prend sa hauteur naturelle (à placer dans une zone défilante) : pas de barre de défilement propre.</summary>
    public static RichTextLabel RichFit(bool onLight, string bbcode = "")
    {
        var rich = new RichTextLabel
        {
            BbcodeEnabled = true, FitContent = true, ScrollActive = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Text = bbcode,
        };
        rich.AddThemeColorOverride("default_color", onLight ? Ink : Cream);
        return rich;
    }

    /// <summary>
    /// Texte venu de Core (journal, verdicts) : les flèches Unicode, absentes de Pixeloid Sans, sont rendues en ASCII
    /// (« -> ») plutôt que de dépendre d'une police système de repli, variable d'un OS à l'autre. Affichage seulement.
    /// </summary>
    public static string Plain(string text) => text.Replace("→", "->").Replace("←", "<-");

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
