namespace GuildOps.Core.Domain;

public sealed class Quest
{
    internal Quest(string id, QuestOffer offer, string heroId, int engagementPaidCopper)
    {
        Id = id;
        Offer = offer;
        HeroId = heroId;
        EngagementPaidCopper = engagementPaidCopper;
    }

    public string Id { get; }
    public QuestOffer Offer { get; }
    public QuestTemplate Template => Offer.Template;
    public string HeroId { get; }
    public int EngagementPaidCopper { get; }
    public QuestState State { get; internal set; } = QuestState.Assigned;
}
