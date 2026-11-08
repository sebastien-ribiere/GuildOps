using System.Text.RegularExpressions;

namespace GuildOps.Tests;

/// <summary>
/// Contrôle statique de la décision typographique B5.1 (docs/b5-1-typography-decision.md) : Pixeloid Sans seul, tailles 9 et 18,
/// aucune police système en repli, aucune réintroduction de Jersey 15, Silkscreen ou Tiny5.
/// </summary>
public class FontPolicyTests
{
    private static readonly string Game = Path.Combine(RepoRoot(), "src", "GuildOps.Game");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "GuildOps.Game"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Racine du dépôt introuvable.");
    }

    private static IEnumerable<string> GameFiles(params string[] extensions) =>
        Directory.EnumerateFiles(Game, "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.godot{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && extensions.Contains(Path.GetExtension(f)));

    [Fact]
    public void Aucune_reference_a_Jersey_Silkscreen_ou_Tiny5_dans_le_jeu()
    {
        var banned = new Regex("Jersey|Silkscreen|Tiny5", RegexOptions.IgnoreCase);
        var hits = GameFiles(".cs", ".tscn", ".tres", ".godot", ".import", ".cfg", ".csproj", ".ttf", ".otf")
            .SelectMany(f => banned.IsMatch(Path.GetFileName(f)) ? [$"{f} (nom de fichier)"] : File.ReadLines(f).Where(l => banned.IsMatch(l)).Select(l => $"{f}: {l.Trim()}"))
            .ToList();
        Assert.True(hits.Count == 0, "Polices interdites : " + string.Join(" | ", hits));
    }

    [Fact]
    public void Seules_les_deux_polices_Pixeloid_sont_embarquees()
    {
        var fonts = Directory.EnumerateFiles(Path.Combine(Game, "Assets"), "*", SearchOption.AllDirectories)
            .Where(f => new[] { ".ttf", ".otf", ".woff", ".woff2" }.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Select(f => Path.GetFileName(f)!).OrderBy(n => n).ToArray();
        Assert.Equal(["PixeloidSans-Bold.ttf", "PixeloidSans.ttf"], fonts);
        Assert.True(File.Exists(Path.Combine(Game, "Assets", "fonts", "OFL-PixeloidSans.txt")), "Licence OFL manquante.");
    }

    [Theory]
    [InlineData("PixeloidSans.ttf.import")]
    [InlineData("PixeloidSans-Bold.ttf.import")]
    public void Import_sans_repli_systeme_ni_lissage(string file)
    {
        var text = File.ReadAllText(Path.Combine(Game, "Assets", "fonts", file));
        Assert.Contains("allow_system_fallback=false", text);
        Assert.Contains("antialiasing=0", text);
        Assert.Contains("subpixel_positioning=0", text);
    }

    [Fact]
    public void Les_polices_et_leurs_tailles_sont_choisies_uniquement_dans_UiKit()
    {
        var offenders = GameFiles(".cs")
            .Where(f => Path.GetFileName(f) is not "UiKit.cs")
            .SelectMany(f => File.ReadLines(f).Select((l, i) => (f, l, i))
                .Where(x => Regex.IsMatch(x.l, @"AddThemeFontOverride|AddThemeFontSizeOverride|FontFile|SystemFont|FontVariation|\bGD\.Load<Font")))
            .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}: {x.l.Trim()}").ToList();
        Assert.True(offenders.Count == 0, "Sélection de police hors UiKit : " + string.Join(" | ", offenders));
    }

    [Fact]
    public void UiKit_ne_charge_que_Pixeloid_et_n_autorise_que_9_et_18()
    {
        var source = File.ReadAllText(Path.Combine(Game, "Ui", "UiKit.cs"));
        var loaded = Regex.Matches(source, @"PixelFont\(""([^""]+)""\)").Select(m => m.Groups[1].Value).Distinct().OrderBy(n => n).ToArray();
        Assert.Equal(["PixeloidSans-Bold.ttf", "PixeloidSans.ttf"], loaded);
        Assert.Contains("TextSize = 9;", source);
        Assert.Contains("VerdictSize = 18;", source);
        Assert.Contains("AllowSystemFallback = false", source);
        // Aucune taille numérique improvisée dans les scènes : seuls UiKit.TextSize / UiKit.VerdictSize sont admis.
        var sizes = GameFiles(".cs").Where(f => Path.GetFileName(f) is not "UiKit.cs")
            .SelectMany(f => File.ReadLines(f).Where(l => Regex.IsMatch(l, @"AsTitle\([^;]*,\s*\d+\)")).Select(l => $"{Path.GetFileName(f)}: {l.Trim()}")).ToList();
        Assert.True(sizes.Count == 0, "Taille de police numérique : " + string.Join(" | ", sizes));
    }
}
