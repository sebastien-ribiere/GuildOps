namespace GuildOps.Core.Domain;

public enum ComplaintChoice { Repair, Apologize, Deny }

public enum ComplaintState { Open, Resolved, Expired }

/// <summary>Dossier client lié à un incident réel. Il ne peut être clos qu'une fois ; la mission d'origine n'est jamais requalifiée.</summary>
public sealed class Complaint
{
    internal Complaint(string id, string questId, Storylet storylet, int dayOpened, int expiresAfterDay)
    {
        Id = id;
        QuestId = questId;
        Storylet = storylet;
        DayOpened = dayOpened;
        ExpiresAfterDay = expiresAfterDay;
    }

    public string Id { get; }
    public string QuestId { get; }
    public Storylet Storylet { get; }
    public int DayOpened { get; }
    /// <summary>Dernier jour où le dossier peut être traité : à la fin de ce jour, il expire.</summary>
    public int ExpiresAfterDay { get; }
    public int RepairCostCopper => Storylet.RepairCostCopper;
    public ComplaintState State { get; internal set; } = ComplaintState.Open;
    public ComplaintChoice? Choice { get; internal set; }
}
