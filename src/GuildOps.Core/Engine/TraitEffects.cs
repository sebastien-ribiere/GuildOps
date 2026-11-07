using GuildOps.Core.Domain;

namespace GuildOps.Core.Engine;

/// <summary>
/// Point d'extension des traits (Phase B : familles de modificateurs typés). En Phase A les traits sont
/// seulement affichés ; l'implémentation par défaut n'applique aucun bonus chiffré.
/// </summary>
public interface ITraitEffects
{
    /// <summary>Modificateur de probabilité de réussite, en points de pourcentage.</summary>
    int SuccessModifierPp(HeroDefinition hero, QuestTemplate quest);
}

public sealed class NoTraitEffects : ITraitEffects
{
    public int SuccessModifierPp(HeroDefinition hero, QuestTemplate quest) => 0;
}
