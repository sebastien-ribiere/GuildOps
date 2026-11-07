using GuildOps.Core.Domain;

namespace GuildOps.Core.Engine;

/// <summary>Résultat d'une commande. En cas de refus, aucun état n'a été modifié.</summary>
public sealed class Result<T> where T : class
{
    private Result(T? value, IReadOnlyList<RefusalReason> reasons)
    {
        Value = value;
        Reasons = reasons;
    }

    public bool Ok => Value is not null;
    public T? Value { get; }
    public IReadOnlyList<RefusalReason> Reasons { get; }
    public string Message => Ok ? "" : string.Join(" ; ", Reasons.Select(RefusalMessages.For));

    internal static Result<T> Success(T value) => new(value, []);
    internal static Result<T> Refused(IReadOnlyList<RefusalReason> reasons) => new(null, reasons);
}

public static class RefusalMessages
{
    public static string For(RefusalReason reason) => reason switch
    {
        RefusalReason.UnknownOffer => "Offre inconnue.",
        RefusalReason.UnknownHero => "Héros inconnu.",
        RefusalReason.UnknownQuest => "Quête inconnue.",
        RefusalReason.OfferNotAvailable => "Cette offre n'est plus disponible.",
        RefusalReason.HeroBusy => "Ce héros est déjà engagé sur une mission.",
        RefusalReason.SkillMismatch => "La compétence du héros ne correspond pas à la quête.",
        RefusalReason.LevelTooLow => "Le niveau du héros est inférieur au niveau minimal de la quête.",
        RefusalReason.NotEnoughEnergy => "Le héros n'a pas assez d'énergie.",
        RefusalReason.NotEnoughTreasury => "La trésorerie ne couvre pas le coût d'engagement.",
        RefusalReason.DailyLimitReached => "Limite de missions de la journée atteinte.",
        RefusalReason.QuestNotAssigned => "Cette mission n'est pas en cours (déjà résolue ?).",
        _ => reason.ToString(),
    };
}

/// <summary>Prévisualisation pure (aucune mutation) d'une affectation.</summary>
public sealed record AssignmentCheck(
    bool CanAssign,
    IReadOnlyList<RefusalReason> Reasons,
    int EngagementCostCopper,
    ChanceBreakdown? Chance)
{
    public string Message => string.Join(" ; ", Reasons.Select(RefusalMessages.For));
}

public sealed record MissionOutcome(
    string QuestId,
    string HeroId,
    bool Success,
    int Roll,
    int ChancePercent,
    int RewardPaidCopper,
    int EnergySpent,
    int ReputationBefore,
    int ReputationAfter,
    string ReputationReason)
{
    public int ReputationDelta => ReputationAfter - ReputationBefore;
}
