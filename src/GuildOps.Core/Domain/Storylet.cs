namespace GuildOps.Core.Domain;

public enum OutcomeFilter { Any, Success, Failure }

/// <summary>Conditions déclaratives (données). Une liste vide ne contraint pas ; plusieurs critères se combinent par ET.</summary>
public sealed record StoryletConditions(
    OutcomeFilter Outcome,
    IReadOnlyList<string> HeroIds,
    IReadOnlyList<QuestCategory> Categories,
    IReadOnlyList<string> TemplateIds);

public sealed record ChoiceLabels(string Repair, string Apologize, string Deny);

/// <summary>Situation narrative locale. Un storylet qui a un incident ouvre une réclamation dont le coût de réparation vient d'ici.</summary>
public sealed record Storylet(
    string Id,
    string Title,
    string Text,
    int Priority,
    int ProbabilityPercent,
    StoryletConditions Conditions,
    int RepairCostCopper,
    ChoiceLabels Labels);
