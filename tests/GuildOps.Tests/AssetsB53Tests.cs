using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace GuildOps.Tests;

/// <summary>
/// Contrôle des 27 PNG runtime du kit B5.3 contre docs/b5-3/manifest.json : jeu de fichiers exact, dimensions (en-tête IHDR) et SHA-256.
/// Équivalent C# de tools/validate_b5_3.py (sans Python). Vérifie aussi que ni sources/ ni previews/ ne sont embarqués dans le jeu.
/// </summary>
public class AssetsB53Tests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "GuildOps.Game"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Racine du dépôt introuvable.");
    }

    private static string Runtime => Path.Combine(Root(), "src", "GuildOps.Game", "Assets", "b5_3");

    private static List<(string Path, int W, int H, string Sha)> Manifest()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "docs", "b5-3", "manifest.json")));
        return doc.RootElement.GetProperty("integrity").EnumerateArray()
            .Select(a => (a.GetProperty("path").GetString()!["assets/b5_3/".Length..], a.GetProperty("width").GetInt32(), a.GetProperty("height").GetInt32(), a.GetProperty("sha256").GetString()!))
            .ToList();
    }

    [Fact]
    public void Le_jeu_de_fichiers_runtime_est_exactement_celui_du_manifest()
    {
        var expected = Manifest().Select(m => m.Path).Order().ToArray();
        var actual = Directory.EnumerateFiles(Runtime, "*.png", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Runtime, f).Replace('\\', '/')).Order().ToArray();
        Assert.Equal(27, expected.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Chaque_PNG_a_la_bonne_taille_et_la_bonne_empreinte()
    {
        foreach (var (path, w, h, sha) in Manifest())
        {
            var bytes = File.ReadAllBytes(Path.Combine(Runtime, path));
            Assert.True(bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }), $"{path} : signature PNG");
            Assert.Equal(w, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
            Assert.Equal(h, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
            Assert.Equal(sha, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }
    }

    [Fact]
    public void Ni_sources_ni_previews_ne_sont_embarques_dans_le_jeu()
    {
        var assets = Path.Combine(Root(), "src", "GuildOps.Game", "Assets");
        var dirs = Directory.EnumerateDirectories(assets, "*", SearchOption.AllDirectories).Select(Path.GetFileName).ToList();
        Assert.DoesNotContain("sources", dirs);
        Assert.DoesNotContain("previews", dirs);
        Assert.DoesNotContain(Directory.EnumerateFiles(Runtime), f => true); // aucun fichier à la racine de b5_3
    }
}
