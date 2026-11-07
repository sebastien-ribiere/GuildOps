namespace GuildOps.Core.Domain;

/// <summary>Offre figée à sa publication : une évolution ultérieure de la réputation ne la modifie jamais.</summary>
public sealed class QuestOffer
{
    internal QuestOffer(string id, QuestTemplate template, int day)
    {
        Id = id;
        Template = template;
        Day = day;
    }

    public string Id { get; }
    public QuestTemplate Template { get; }
    public int Day { get; }
    public OfferState State { get; internal set; } = OfferState.Available;
}
