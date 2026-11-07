using GuildOps.Core.Domain;

namespace GuildOps.Core.Data;

public sealed record ReputationTier(string Id, int Min, int Max);

public sealed record CategoryParameters(
    int RewardCopperMin, int RewardCopperMax,
    int EnergyCostMin, int EnergyCostMax,
    int LevelMin, int LevelMax,
    int RiskPenaltyPp,
    bool WaiveHeroEngagementCost);

public sealed record SuccessFormula(
    int BaseSuccessPercent,
    int LevelBonusPerAdditionalLevelPp,
    int LowEnergyMultiple,
    int LowEnergyPenaltyPp,
    int MinSuccessPercent,
    int MaxSuccessPercent)
{
    /// <summary>Domaine du tirage : entier 0..99 (rules.missionResolution.rollDomain).</summary>
    public const int RollRange = 100;
}

public sealed record ReputationDeltas(
    int RegularSuccess, int CommunitySuccess, int RegularFailure, int CommunityFailure);

/// <summary>Paramètres de jeu typés, issus de rules.v0.1.json. Aucun seuil n'est dupliqué ailleurs.</summary>
public sealed record VictoryConditions(int MinTreasuryCopper, int MinReputation, int MinSucceededMissions);

public sealed record GameRules(
    int InitialTreasuryCopper,
    int InitialReputation,
    int InitialDay,
    int CopperPerGold,
    int HeroCount,
    int MaxEnergy,
    int OffersPerDay,
    int MaxMissionsPerDay,
    int Days,
    int EnergyRegenerationAtDayStart,
    VictoryConditions Victory,
    int ReputationMin,
    int ReputationMax,
    IReadOnlyList<ReputationTier> Tiers,
    IReadOnlyDictionary<string, IReadOnlyList<int>> CategoryWeightsByTier,
    IReadOnlyList<QuestCategory> CategoryOrder,
    IReadOnlyDictionary<QuestCategory, CategoryParameters> Categories,
    ReputationDeltas Deltas,
    SuccessFormula Formula,
    string RecoveryTemplateId,
    int RecoveryReputationThreshold,
    long TestSeed)
{
    public ReputationTier TierFor(int reputation)
    {
        foreach (var tier in Tiers)
            if (reputation >= tier.Min && reputation <= tier.Max) return tier;
        throw new ArgumentOutOfRangeException(nameof(reputation), reputation, "Réputation hors des paliers.");
    }
}
