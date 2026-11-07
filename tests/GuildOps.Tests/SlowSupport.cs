using System.Runtime.InteropServices;
using GuildOps.Core.Data;
using GuildOps.Core.Domain;
using GuildOps.Core.Randomness;

namespace GuildOps.Tests;

/// <summary>
/// Test lent : ignoré par défaut pour garder <c>dotnet test</c> rapide. Activer avec <c>GUILDOPS_SLOW=1</c>
/// (voir README, section « Benchmark et équilibrage »).
/// </summary>
internal sealed class SlowFactAttribute : FactAttribute
{
    public const string Variable = "GUILDOPS_SLOW";

    public SlowFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(Variable) != "1")
            Skip = $"Test lent (benchmark/équilibrage). Pour l'exécuter : {Variable}=1 dotnet test --filter \"Category=Slow\"";
    }
}

internal static class Artifacts
{
    /// <summary>Dossier <c>artifacts/</c> à la racine du dépôt (ignoré par git) : résultats des tests lents.</summary>
    public static string Directory(string? sub = null)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GuildOps.sln"))) dir = dir.Parent;
        var path = Path.Combine(dir?.FullName ?? AppContext.BaseDirectory, "artifacts", sub ?? "");
        System.IO.Directory.CreateDirectory(path);
        return path;
    }

    public static string Machine() =>
        $"{RuntimeInformation.OSDescription.Trim()} · {RuntimeInformation.ProcessArchitecture} · {Environment.ProcessorCount} cœurs · {RuntimeInformation.FrameworkDescription}";
}

/// <summary>Catalogue synthétique déterministe : les 20 modèles réels, complétés jusqu'à N modèles dans les bornes de chaque catégorie.</summary>
internal static class SyntheticCatalogue
{
    public static IReadOnlyList<QuestTemplate> Build(GameData data, int total, long seed = 42)
    {
        var list = data.Templates.ToList();
        var rng = new SeededRandom(seed);
        for (var i = list.Count; i < total; i++)
        {
            var category = data.Rules.CategoryOrder[rng.NextInt(data.Rules.CategoryOrder.Count)];
            var p = data.Rules.Categories[category];
            list.Add(new QuestTemplate(
                $"synthetic-{i:00000}", $"Quête synthétique {i}", "Jeu de données de benchmark.", category,
                (Skill)rng.NextInt(4),
                p.LevelMin + rng.NextInt(p.LevelMax - p.LevelMin + 1),
                p.EnergyCostMin + rng.NextInt(p.EnergyCostMax - p.EnergyCostMin + 1),
                p.RewardCopperMin + rng.NextInt(p.RewardCopperMax - p.RewardCopperMin + 1)));
        }
        return list.Take(total).ToList();
    }
}
