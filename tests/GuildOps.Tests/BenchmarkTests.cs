using System.Diagnostics;
using System.Text;
using GuildOps.Core.Data;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using GuildOps.Core.Randomness;
using static GuildOps.Tests.TestSupport;

namespace GuildOps.Tests;

/// <summary>
/// Benchmark 100 / 1 000 / 10 000 modèles de quêtes (<c>performance.catalogueSizesForBenchmark</c> des règles).
/// Protocole identique avant/après une modification : mêmes tailles, même catalogue synthétique (graine 42), mêmes graines de jeu,
/// un tour d'échauffement non mesuré, puis <c>Repeats</c> mesures dont on retient la MÉDIANE. Les temps dépendent du poste : ils
/// sont écrits dans <c>artifacts/benchmark-latest.md</c> et ne sont comparés qu'entre exécutions sur la même machine.
/// Lancement : <c>GUILDOPS_SLOW=1 dotnet test --filter "Category=Slow"</c>.
/// </summary>
[Trait("Category", "Slow")]
public class BenchmarkTests
{
    private const int Repeats = 15;
    private static readonly int[] Reputations = [10, 30, 50, 70, 90];   // un point par palier de réputation

    private static double MedianMs(Action action)
    {
        action();   // échauffement
        var samples = new List<double>();
        for (var i = 0; i < Repeats; i++)
        {
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }
        samples.Sort();
        return samples[samples.Count / 2];
    }

    private static GameData WithCatalogue(int n) => Data with { Templates = SyntheticCatalogue.Build(Data, n) };

    private static int[] SizesFromRules()
    {
        var json = File.ReadAllText(Path.Combine(DataTests.FindGameData(), "rules.v0.1.json"));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("performance").GetProperty("catalogueSizesForBenchmark")
            .EnumerateArray().Select(x => x.GetInt32()).ToArray();
    }

    [SlowFact]
    public void Synthetic_catalogue_is_deterministic_and_within_category_bounds()
    {
        Assert.Equal([100, 1000, 10000], SizesFromRules());
        var a = SyntheticCatalogue.Build(Data, 1000);
        Assert.Equal(1000, a.Count);
        Assert.Equal(a.Select(t => (t.Id, t.RewardCopper, t.EnergyCost, t.MinLevel)), SyntheticCatalogue.Build(Data, 1000).Select(t => (t.Id, t.RewardCopper, t.EnergyCost, t.MinLevel)));
        Assert.All(a, t =>
        {
            var p = Data.Rules.Categories[t.Category];
            Assert.InRange(t.RewardCopper, p.RewardCopperMin, p.RewardCopperMax);
            Assert.InRange(t.EnergyCost, p.EnergyCostMin, p.EnergyCostMax);
            Assert.InRange(t.MinLevel, p.LevelMin, p.LevelMax);
        });
        Assert.Equal(a.Count, a.Select(t => t.Id).Distinct().Count());
    }

    [SlowFact]
    public void Run_benchmark_and_write_the_report()
    {
        var rows = new StringBuilder();
        rows.AppendLine("| Modèles | Génération d'un tableau (médiane, ms) | Meilleure affectation modèles × 12 héros (médiane, ms) | Partie complète 7 jours, robot glouton (médiane, ms) |");
        rows.AppendLine("| ---: | ---: | ---: | ---: |");

        foreach (var n in SizesFromRules())
        {
            var data = WithCatalogue(n);
            var heroes = GameSession.Start(data, Seed, traits: new NoTraitEffects()).Heroes;

            // 1. Génération : un tableau pour chacun des cinq paliers de réputation
            IReadOnlyList<QuestTemplate> last = [];
            var generation = MedianMs(() =>
            {
                foreach (var rep in Reputations)
                {
                    last = OfferGenerator.Generate(data, rep, 1200, heroes, new SeededRandom(Seed + rep));
                    Assert.Equal(4, last.Count);
                    Assert.Equal(4, last.Select(t => t.Id).Distinct().Count());
                }
            }) / Reputations.Length;

            // 2. Meilleure affectation : tous les modèles × tous les héros (même critère que le robot glouton)
            (string Template, string Hero) best = default;
            var assignment = MedianMs(() =>
            {
                long bestScore = long.MinValue;
                foreach (var t in data.Templates)
                    foreach (var h in heroes)
                    {
                        if (AssignmentRules.Evaluate(data.Rules, h, t, 1200).Count > 0) continue;
                        var chance = SuccessChance.Compute(data.Rules, t.Category, h.Definition.Level, t.MinLevel, 0, h.Energy, t.EnergyCost);
                        var score = (long)chance.Final * t.RewardCopper - 100L * AssignmentRules.EngagementCost(data.Rules, h, t);
                        if (score > bestScore) { bestScore = score; best = (t.Id, h.Id); }
                    }
            });
            Assert.NotEqual(default, best);

            // 3. Partie complète avec ce catalogue
            var finished = 0;
            var game = MedianMs(() =>
            {
                var s = GameSession.Start(data, Seed);
                Bots.Greedy(s);
                if (s.Status == GameStatus.Finished) finished++;
            });
            Assert.Equal(Repeats + 1, finished);

            rows.AppendLine($"| {n} | {generation:F3} | {assignment:F3} | {game:F3} |");
        }

        var report = new StringBuilder()
            .AppendLine("# Benchmark GuildOps (résultat local, non versionné)")
            .AppendLine()
            .AppendLine($"Machine : {Artifacts.Machine()}")
            .AppendLine($"Protocole : catalogue synthétique déterministe (graine 42), {Repeats} mesures + 1 échauffement, médiane. Tailles 100 / 1 000 / 10 000.")
            .AppendLine()
            .Append(rows)
            .ToString();
        File.WriteAllText(Path.Combine(Artifacts.Directory(), "benchmark-latest.md"), report);
        Assert.Contains("10000", report);
    }
}
