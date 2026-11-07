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
        RefusalReason.PendingMissions => "Des missions engagées ne sont pas résolues : elles doivent l'être avant la fin de la journée.",
        RefusalReason.GameFinished => "La partie est terminée : recommencez pour rejouer.",
        RefusalReason.RegenerationDisabled => "La régénération du tableau n'est pas activée dans les règles.",
        RefusalReason.RegenerationAlreadyUsed => "La régénération du tableau a déjà été utilisée dans cette partie.",
        RefusalReason.MissionsAlreadyEngaged => "Impossible de régénérer le tableau : une mission a déjà été engagée aujourd'hui.",
        RefusalReason.UnknownComplaint => "Réclamation inconnue.",
        RefusalReason.ComplaintNotOpen => "Cette réclamation est déjà close ou expirée.",
        RefusalReason.NoPreparationTrait => "Ce héros n'a pas de trait de préparation.",
        RefusalReason.AlreadyPrepared => "Cette catégorie est déjà préparée par ce héros.",
        _ => reason.ToString(),
    };
}

/// <summary>Prévisualisation pure (aucune mutation) d'une affectation.</summary>
public sealed record AssignmentCheck(
    bool CanAssign,
    IReadOnlyList<RefusalReason> Reasons,
    int EngagementCostCopper,
    ChanceBreakdown? Chance,
    int EnergyCost = 0,
    int RewardCopper = 0,
    IReadOnlyList<string>? TraitNotes = null)
{
    /// <summary>Effets de trait actifs (ou en attente d'une condition) pour cette affectation, avant engagement.</summary>
    public IReadOnlyList<string> Notes => TraitNotes ?? [];
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
    string ReputationReason,
    IncidentReport? Incident = null)
{
    public int ReputationDelta => ReputationAfter - ReputationBefore;
}

/// <summary>Bilan royal après la dernière journée. Un échec n'est pas un game-over : l'état est conservé et on peut recommencer.</summary>
public sealed record FinalReport(
    bool Success,
    int Reputation,
    int TreasuryCopper,
    int MissionsSucceeded,
    bool ReputationOk,
    bool TreasuryOk,
    bool MissionsOk,
    string Message,
    int OpenComplaints);

public sealed record DayTransition(int EndedDay, int? NewDay, IReadOnlyList<QuestOffer> NewOffers, FinalReport? Report);

public sealed record Regeneration(int Day, IReadOnlyList<QuestOffer> NewOffers, int ReputationBefore, int ReputationAfter);

/// <summary>Incident déclenché par une mission : l'événement narratif, le dommage collatéral et le dossier ouvert.</summary>
public sealed record IncidentReport(string StoryletId, string Title, string Text, string ComplaintId, int CollateralReputationDelta);

public sealed record ComplaintOutcome(
    string ComplaintId,
    ComplaintChoice Choice,
    int CostPaidCopper,
    int ReputationBefore,
    int ReputationAfter,
    string Message,
    int TraitBonus = 0)
{
    public int ReputationDelta => ReputationAfter - ReputationBefore;
}

public sealed record PreparationOutcome(string HeroId, QuestCategory Category, int EnergySpent, int CopperSpent, string Message);
