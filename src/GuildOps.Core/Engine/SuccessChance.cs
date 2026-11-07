using GuildOps.Core.Data;

namespace GuildOps.Core.Engine;

/// <summary>Décomposition de la probabilité de réussite, en points de pourcentage, pour l'afficher au joueur.</summary>
public sealed record ChanceBreakdown(
    int Base,
    int RiskPenalty,
    int LevelBonus,
    int TraitModifier,
    int LowEnergyPenalty,
    int Unclamped,
    int Final);

public static class SuccessChance
{
    public static ChanceBreakdown Compute(
        GameRules rules, Domain.QuestCategory category, int heroLevel, int questMinLevel,
        int traitModifierPp, int energyBefore, int questEnergyCost)
    {
        var f = rules.Formula;
        var risk = rules.Categories[category].RiskPenaltyPp;
        var levelBonus = f.LevelBonusPerAdditionalLevelPp * (heroLevel - questMinLevel);
        var lowEnergy = energyBefore < f.LowEnergyMultiple * questEnergyCost ? f.LowEnergyPenaltyPp : 0;
        var raw = f.BaseSuccessPercent - risk + levelBonus + traitModifierPp - lowEnergy;
        return new ChanceBreakdown(f.BaseSuccessPercent, risk, levelBonus, traitModifierPp, lowEnergy, raw,
            Math.Clamp(raw, f.MinSuccessPercent, f.MaxSuccessPercent));
    }
}
