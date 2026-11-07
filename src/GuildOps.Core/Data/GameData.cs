using GuildOps.Core.Domain;

namespace GuildOps.Core.Data;

/// <summary>Ensemble des données de jeu validées. Catalogue trié par identifiant (ordre stable pour le tirage).</summary>
public sealed record GameData(
    GameRules Rules,
    IReadOnlyList<HeroDefinition> Heroes,
    IReadOnlyList<QuestTemplate> Templates);

public sealed class GameDataException(string message) : Exception(message);
