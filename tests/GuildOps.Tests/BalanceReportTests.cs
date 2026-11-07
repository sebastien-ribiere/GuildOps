using System.Text;
using GuildOps.Core.Data;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using static GuildOps.Tests.TestSupport;

namespace GuildOps.Tests;

/// <summary>
/// Outil de MESURE de l'équilibrage (il ne règle rien). Pour chaque variante de données et chaque politique de réponse aux
/// réclamations, joue <c>Games</c> parties sur les graines 1..Games avec le robot glouton et écrit un tableau comparatif dans
/// <c>artifacts/balance-latest.md</c>, ainsi que les journaux des parties de référence dans <c>artifacts/playtests/</c>.
/// Lancement : <c>GUILDOPS_SLOW=1 dotnet test --filter "Category=Slow"</c>.
/// </summary>
[Trait("Category", "Slow")]
public class BalanceReportTests
{
    private const int Games = 1000;

    private sealed record Variant(string Name, GameData Data, ITraitEffects Traits);

    private static IEnumerable<Variant> Variants()
    {
        var halved = Data.Storylets.Select(s => s.Id is "E05" or "E08" ? s with { ProbabilityPercent = 15 } : s).ToList();
        yield return new("Baseline actuelle (8 storylets, traits)", Data, TraitCatalog.Default);
        yield return new("Sans traits", Data, new NoTraitEffects());
        yield return new("E05 et E08 à 15 % au lieu de 30 %", Data with { Storylets = halved }, TraitCatalog.Default);
        yield return new("2 storylets seulement (état B2), traits", Data with { Storylets = Data.Storylets.Where(s => s.Id is "E01" or "E02").ToList() }, TraitCatalog.Default);
    }

    private sealed record Cell(int Wins, double Incidents, double Reputation, double Treasury, int LostByReputation, int LostByTreasury, int LostByMissions, int Losses);

    private static Cell Measure(Variant v, ComplaintPolicy policy)
    {
        int wins = 0, byRep = 0, byTre = 0, byMis = 0, incidents = 0;
        double rep = 0, tre = 0;
        for (var seed = 1; seed <= Games; seed++)
        {
            var s = GameSession.Start(v.Data, seed, traits: v.Traits);
            Bots.Greedy(s, null, policy);
            var r = s.Report!;
            if (r.Success) wins++;
            else
            {
                if (!r.ReputationOk) byRep++;
                if (!r.TreasuryOk) byTre++;
                if (!r.MissionsOk) byMis++;
            }
            incidents += s.Complaints.Count;
            rep += r.Reputation;
            tre += r.TreasuryCopper;
        }
        return new(wins, incidents / (double)Games, rep / Games, tre / Games, byRep, byTre, byMis, Games - wins);
    }

    [SlowFact]
    public void Write_the_balance_comparison_and_the_reference_journals()
    {
        var sb = new StringBuilder()
            .AppendLine("# Mesures d'équilibrage GuildOps (résultat local, non versionné)")
            .AppendLine()
            .AppendLine($"Machine : {Artifacts.Machine()}")
            .AppendLine($"Joueur : robot glouton (maximise chance × prime − engagement), {Games} parties par cellule, graines 1 à {Games}. Aucune valeur de jeu n'est modifiée par cet outil : les variantes ne vivent que dans le test.")
            .AppendLine()
            .AppendLine("| Variante | Réponse aux réclamations | Victoires | Incidents/partie | Réputation finale moy. | Caisse finale moy. (cuivres) | Défaites par réputation | par trésorerie | par missions |")
            .AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");

        foreach (var v in Variants())
            foreach (var policy in Enum.GetValues<ComplaintPolicy>())
            {
                var c = Measure(v, policy);
                sb.AppendLine($"| {v.Name} | {policy} | {100.0 * c.Wins / Games:F1} % | {c.Incidents:F2} | {c.Reputation:F1} | {c.Treasury:F0} | {c.LostByReputation} / {c.Losses} | {c.LostByTreasury} / {c.Losses} | {c.LostByMissions} / {c.Losses} |");
            }
        File.WriteAllText(Path.Combine(Artifacts.Directory(), "balance-latest.md"), sb.ToString());

        // journaux des parties de référence (graine officielle de test)
        foreach (var policy in Enum.GetValues<ComplaintPolicy>())
        {
            var s = GameSession.Start(Data, Seed);
            Bots.Greedy(s, null, policy);
            var r = s.Report!;
            var md = new StringBuilder()
                .AppendLine($"# Partie de référence — graine {Seed}, robot glouton, réclamations : {policy}")
                .AppendLine()
                .AppendLine($"**Bilan : {(r.Success ? "REDRESSEMENT RÉUSSI" : "BILAN INACHEVÉ")}** — réputation {r.Reputation} (seuil 55), caisse {s.Money(r.TreasuryCopper)} (seuil {s.Money(1000)}), {r.MissionsSucceeded} succès (seuil 8), {s.Complaints.Count} réclamation(s).")
                .AppendLine()
                .AppendLine("## Journal complet")
                .AppendLine();
            foreach (var e in s.Events) md.AppendLine($"- #{e.Sequence} (J{e.Day}) {e.Message}");
            File.WriteAllText(Path.Combine(Artifacts.Directory("playtests"), $"seed-{Seed}-{policy}.md"), md.ToString());
        }
        Assert.True(File.Exists(Path.Combine(Artifacts.Directory(), "balance-latest.md")));
    }
}
