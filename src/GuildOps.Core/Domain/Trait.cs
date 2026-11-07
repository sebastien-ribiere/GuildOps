namespace GuildOps.Core.Domain;

/// <summary>
/// Les sept familles de modificateurs typés. Un trait est une petite liste de modificateurs (un atout, une contrepartie) :
/// il n'y a donc pas douze chaînes de « if » par héros, mais sept familles évaluées de façon générique.
/// </summary>
public enum TraitFamily
{
    /// <summary>Points de pourcentage ajoutés à la probabilité de réussite.</summary>
    Chance,
    /// <summary>Cuivres ajoutés (ou retirés) au coût d'engagement ; sans effet sur une catégorie exemptée de frais.</summary>
    EngagementCost,
    /// <summary>Points d'énergie ajoutés (ou retirés) au coût de la quête ; jamais en dessous de 1.</summary>
    EnergyCost,
    /// <summary>Pourcentage ajouté (ou retiré) à la prime, arrondi vers le bas, versée en cas de succès.</summary>
    Reward,
    /// <summary>Points de pourcentage ajoutés à la probabilité de chaque storylet éligible.</summary>
    IncidentProbability,
    /// <summary>Points de réputation ajoutés quand une réclamation de ce héros est réparée ou apaisée par des excuses.</summary>
    ComplaintReputation,
    /// <summary>Énergie rendue aux autres aventuriers quand ce héros termine une mission.</summary>
    TeamRecovery,
}

/// <summary>Condition d'activation d'un modificateur (état connu et affiché avant l'engagement).</summary>
public enum TraitCondition
{
    Always,
    /// <summary>Le héros a préparé la catégorie de la quête (commande explicite, voir <see cref="TraitPreparation"/>).</summary>
    PreparedCategory,
    /// <summary>Le héros a déjà terminé au moins une mission de la même catégorie.</summary>
    ExperiencedCategory,
}

/// <param name="Categories">Catégories concernées ; liste vide = toutes.</param>
public sealed record TraitModifier(TraitFamily Family, int Value, IReadOnlyList<QuestCategory> Categories, TraitCondition Condition = TraitCondition.Always);

/// <summary>Investissement explicite qui active les modificateurs <see cref="TraitCondition.PreparedCategory"/> pour une catégorie.</summary>
public sealed record TraitPreparation(int EnergyCost, int CopperCost);

public sealed record TraitDefinition(string TraitId, IReadOnlyList<TraitModifier> Modifiers, TraitPreparation? Preparation = null);
