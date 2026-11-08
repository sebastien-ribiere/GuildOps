using Godot;
using GuildOps.Core.Domain;

namespace GuildOps.Game.Ui;

/// <summary>
/// Fabrique de textures du kit B5.2 (<c>res://Assets/b5_2/</c>). Les illustrations de contrats sont adressées EXCLUSIVEMENT par l'identifiant
/// du modèle du catalogue (<c>QuestTemplate.Id</c>) : jamais par le titre, jamais de repli narratif. Présentation pure, aucune logique métier.
/// </summary>
public static class Art
{
    public const string Root = "b5_2/";
    public const int ThumbW = 96, ThumbH = 64;
    public const int HdW = 384, HdH = 256;

    private static readonly Dictionary<string, Texture2D> HalfCache = [];

    /// <summary>Miniature 96×64 (listes et espaces compacts), à afficher à l'échelle 1 ou ×2 entier, sans lissage.</summary>
    public static Texture2D Thumb(string templateId) => UiKit.Tex($"{Root}quests/{templateId}_{ThumbW}x{ThumbH}.png");

    /// <summary>Illustration détaillée 384×256, à afficher à l'échelle 1 (visionneuse) en respectant le ratio 3:2.</summary>
    public static Texture2D Hd(string templateId) => UiKit.Tex($"{Root}quests_hd/{templateId}_{HdW}x{HdH}.png");

    /// <summary>
    /// Illustration HD réduite d'un facteur 2 exact (192×128) pour les fiches qui ne peuvent pas accueillir le 384×256. Rééchantillonnée
    /// une seule fois (filtre trilinéaire sur 2×2 pixels = moyenne de zone), puis mise en cache : pas d'échantillonnage au plus proche,
    /// qui sauterait un pixel sur deux et détruirait le détail.
    /// </summary>
    public static Texture2D HdHalf(string templateId)
    {
        if (HalfCache.TryGetValue(templateId, out var cached)) return cached;
        var image = Hd(templateId).GetImage();
        image.Resize(HdW / 2, HdH / 2, Image.Interpolation.Trilinear);
        return HalfCache[templateId] = ImageTexture.CreateFromImage(image);
    }

    /// <summary>Emblème de catégorie 24×24 (toujours accompagné du nom de la catégorie en texte).</summary>
    public static Texture2D Category(QuestCategory c) => UiKit.Tex($"{Root}categories/{c.ToString().ToLowerInvariant()}_24.png");

    /// <summary>Icône de compétence 16×16 (décorative : le nom reste écrit).</summary>
    public static Texture2D Skill(Skill s) => UiKit.Tex($"{Root}skills/{s.ToString().ToLowerInvariant()}_16.png");

    /// <summary>Marqueur 40×12 du niveau MINIMAL requis par le contrat (1 à 4). Ce n'est ni une difficulté ni une probabilité.</summary>
    public static Texture2D RequiredLevel(int minLevel) => UiKit.Tex($"{Root}levels/niveau_requis_{Math.Clamp(minLevel, 1, 4)}_40x12.png");

    /// <summary>Image d'une frame d'une bande horizontale de cadres carrés/rectangulaires de taille fixe.</summary>
    public static AtlasTexture Frame(Texture2D strip, int index, int w, int h)
        => new() { Atlas = strip, Region = new Rect2(index * w, 0, w, h) };

    /// <summary>Texture réduite au plus proche à l'entier, utilisée par TextureRect.</summary>
    public static TextureRect Picture(Texture2D texture, Vector2 size) => new()
    {
        Texture = texture,
        CustomMinimumSize = size,
        Size = size,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };
}
