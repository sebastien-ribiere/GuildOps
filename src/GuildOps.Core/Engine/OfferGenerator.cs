using GuildOps.Core.Data;
using GuildOps.Core.Domain;
using GuildOps.Core.Randomness;

namespace GuildOps.Core.Engine;

/// <summary>
/// Génération déterministe des nouvelles offres : catalogue trié par id, RNG injecté, pondération par palier de réputation.
/// Repli documenté (non validé) : si aucune catégorie pondérée n'a de modèle réalisable, on publie moins d'offres
/// plutôt que d'ouvrir une catégorie interdite par le palier.
/// </summary>
public static class OfferGenerator
{
    public static IReadOnlyList<QuestTemplate> Generate(
        GameData data, int reputation, int treasuryCopper, IReadOnlyList<Hero> heroes, ISeededRandom rng)
    {
        var rules = data.Rules;
        var chosen = new List<QuestTemplate>();
        var used = new HashSet<string>();

        // Garantie anti-softlock : l'offre de redressement compte parmi les quatre, jamais en plus.
        var cheapestHero = heroes.Min(h => h.Definition.BaseCostCopper);
        if (reputation < rules.RecoveryReputationThreshold || treasuryCopper < cheapestHero)
        {
            var recovery = data.Templates.First(t => t.Id == rules.RecoveryTemplateId);
            chosen.Add(recovery);
            used.Add(recovery.Id);
        }

        var weights = rules.CategoryWeightsByTier[rules.TierFor(reputation).Id];
        while (chosen.Count < rules.OffersPerDay)
        {
            var candidates = rules.CategoryOrder
                .Select(c => data.Templates.Where(t => t.Category == c && !used.Contains(t.Id)
                    && heroes.Any(h => AssignmentRules.Evaluate(rules, h, t, treasuryCopper).Count == 0)).ToList())
                .ToList();

            var effective = weights.Select((w, i) => candidates[i].Count > 0 ? w : 0).ToList();
            var total = effective.Sum();
            if (total == 0) break;

            var roll = rng.NextInt(total);
            var index = 0;
            while (roll >= effective[index]) { roll -= effective[index]; index++; }

            var pool = candidates[index];
            var template = pool[rng.NextInt(pool.Count)];
            chosen.Add(template);
            used.Add(template.Id);
        }
        return chosen;
    }
}
