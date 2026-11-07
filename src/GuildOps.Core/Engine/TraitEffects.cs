using GuildOps.Core.Data;
using GuildOps.Core.Domain;

namespace GuildOps.Core.Engine;

/// <summary>Source des définitions de traits. Injectable : les tests de règles de base utilisent <see cref="NoTraitEffects"/>.</summary>
public interface ITraitEffects
{
    TraitDefinition? For(HeroDefinition hero);
}

public sealed class NoTraitEffects : ITraitEffects
{
    public TraitDefinition? For(HeroDefinition hero) => null;
}

/// <summary>
/// Catalogue des douze traits V0, indexé par le traitId du roster. Valeurs PROPOSÉES (non validées) : le contrat ne fixe
/// que les familles. Chaque trait = un atout + une contrepartie exprimés avec les mêmes familles génériques.
/// </summary>
public sealed class TraitCatalog : ITraitEffects
{
    private static readonly QuestCategory[] All = [];
    private static readonly QuestCategory[] Simple = [QuestCategory.Corvee, QuestCategory.Village];
    private static readonly QuestCategory[] Ambitious = [QuestCategory.Aventure, QuestCategory.Notable, QuestCategory.Royale];
    private static readonly QuestCategory[] Sensitive = [QuestCategory.Notable, QuestCategory.Royale];

    private static TraitModifier M(TraitFamily f, int v, QuestCategory[]? c = null, TraitCondition cond = TraitCondition.Always) => new(f, v, c ?? All, cond);

    private static readonly Dictionary<string, TraitDefinition> Table = new[]
    {
        new TraitDefinition("observer-puis-automatiser", [M(TraitFamily.Chance, 12, null, TraitCondition.PreparedCategory)], new TraitPreparation(10, 15)),
        new TraitDefinition("hotfix-muscle", [M(TraitFamily.EnergyCost, -3, Simple), M(TraitFamily.IncidentProbability, 15)]),
        new TraitDefinition("tester-avant-de-lancer", [M(TraitFamily.Chance, 6), M(TraitFamily.EngagementCost, 6)]),
        new TraitDefinition("livrer-avant-hier", [M(TraitFamily.EnergyCost, -4), M(TraitFamily.IncidentProbability, 10)]),
        new TraitDefinition("chemin-dore", [M(TraitFamily.EnergyCost, -4, null, TraitCondition.ExperiencedCategory), M(TraitFamily.EngagementCost, 4)]),
        new TraitDefinition("valeur-avant-tout", [M(TraitFamily.Reward, 20, Ambitious), M(TraitFamily.Chance, -6, Simple)]),
        new TraitDefinition("zero-trust", [M(TraitFamily.IncidentProbability, -20), M(TraitFamily.Chance, 4, Sensitive), M(TraitFamily.EngagementCost, 8)]),
        new TraitDefinition("optimiser-chaque-cuivre", [M(TraitFamily.EngagementCost, -6), M(TraitFamily.Chance, -4)]),
        new TraitDefinition("ecouter-les-villageois", [M(TraitFamily.ComplaintReputation, 2), M(TraitFamily.EnergyCost, 2)]),
        new TraitDefinition("analyser-avant-de-conclure", [M(TraitFamily.Chance, 8, Ambitious), M(TraitFamily.EnergyCost, 3)]),
        new TraitDefinition("elasticite-magique", [M(TraitFamily.Chance, 10, Ambitious), M(TraitFamily.EngagementCost, 12)]),
        new TraitDefinition("rituel-de-coordination", [M(TraitFamily.TeamRecovery, 3), M(TraitFamily.EnergyCost, 2)]),
    }.ToDictionary(t => t.TraitId);

    public static TraitCatalog Default { get; } = new();

    public static bool Contains(string traitId) => Table.ContainsKey(traitId);

    public TraitDefinition? For(HeroDefinition hero) => Table.GetValueOrDefault(hero.TraitId);
}

/// <summary>Évaluation générique des modificateurs d'un héros pour une quête. Aucune logique propre à un héros précis.</summary>
internal static class TraitMath
{
    public static bool Applies(TraitModifier m, Hero hero, QuestTemplate quest) =>
        (m.Categories.Count == 0 || m.Categories.Contains(quest.Category)) && ConditionMet(m.Condition, hero, quest.Category);

    public static bool ConditionMet(TraitCondition c, Hero hero, QuestCategory category) => c switch
    {
        TraitCondition.PreparedCategory => hero.IsPrepared(category),
        TraitCondition.ExperiencedCategory => hero.MissionsDone(category) > 0,
        _ => true,
    };

    private static int Sum(TraitDefinition? t, TraitFamily family, Hero hero, QuestTemplate quest) =>
        t?.Modifiers.Where(m => m.Family == family && Applies(m, hero, quest)).Sum(m => m.Value) ?? 0;

    public static int ChancePp(TraitDefinition? t, Hero hero, QuestTemplate q) => Sum(t, TraitFamily.Chance, hero, q);

    public static int EngagementCost(GameRules rules, TraitDefinition? t, Hero hero, QuestTemplate q) =>
        rules.Categories[q.Category].WaiveHeroEngagementCost ? 0 : Math.Max(0, hero.Definition.BaseCostCopper + Sum(t, TraitFamily.EngagementCost, hero, q));

    public static int EnergyCost(TraitDefinition? t, Hero hero, QuestTemplate q) => Math.Max(1, q.EnergyCost + Sum(t, TraitFamily.EnergyCost, hero, q));

    public static int Reward(TraitDefinition? t, Hero hero, QuestTemplate q) =>
        q.RewardCopper + q.RewardCopper * Math.Max(-100, Sum(t, TraitFamily.Reward, hero, q)) / 100;

    public static int IncidentPp(TraitDefinition? t, Hero hero, QuestTemplate q) => Sum(t, TraitFamily.IncidentProbability, hero, q);

    public static int ComplaintBonus(TraitDefinition? t) => t?.Modifiers.Where(m => m.Family == TraitFamily.ComplaintReputation).Sum(m => m.Value) ?? 0;

    public static int TeamRecovery(TraitDefinition? t) => t?.Modifiers.Where(m => m.Family == TraitFamily.TeamRecovery).Sum(m => m.Value) ?? 0;

    /// <summary>Texte joueur d'un modificateur (les effets doivent être prévisibles avant l'engagement).</summary>
    public static string Describe(TraitModifier m)
    {
        var v = m.Value;
        var what = m.Family switch
        {
            TraitFamily.Chance => $"{v:+#;-#} points de chance de réussite",
            TraitFamily.EngagementCost => $"{v:+#;-#} cuivres d'engagement",
            TraitFamily.EnergyCost => $"{v:+#;-#} énergie dépensée",
            TraitFamily.Reward => $"{v:+#;-#} % de prime",
            TraitFamily.IncidentProbability => $"{v:+#;-#} points de risque d'incident",
            TraitFamily.ComplaintReputation => $"{v:+#;-#} réputation si la réclamation est réparée ou apaisée",
            _ => $"{v:+#;-#} énergie rendue aux autres aventuriers après la mission",
        };
        var scope = m.Categories.Count == 0 ? "" : $" ({string.Join(", ", m.Categories)})";
        var cond = m.Condition switch
        {
            TraitCondition.PreparedCategory => " si la catégorie est préparée",
            TraitCondition.ExperiencedCategory => " après une première mission de la catégorie",
            _ => "",
        };
        return what + scope + cond;
    }
}
