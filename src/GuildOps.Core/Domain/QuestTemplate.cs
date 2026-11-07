namespace GuildOps.Core.Domain;

/// <summary>Modèle de quête du catalogue local. Toutes les sommes sont en cuivres entiers.</summary>
public sealed record QuestTemplate(
    string Id,
    string Title,
    string Description,
    QuestCategory Category,
    Skill Skill,
    int MinLevel,
    int EnergyCost,
    int RewardCopper);
