namespace GuildOps.Core.Domain;

/// <summary>Fiche immuable d'un aventurier (roster + équilibrage).</summary>
public sealed record HeroDefinition(
    string Id,
    string Name,
    string Role,
    Skill Skill,
    int Level,
    int StartingEnergy,
    int BaseCostCopper,
    string TraitId,
    string TraitKey,
    string TraitText,
    string TraitAdvantage,
    string TraitDrawback,
    string Accessory,
    string Line);
