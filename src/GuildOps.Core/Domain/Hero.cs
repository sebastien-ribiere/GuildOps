namespace GuildOps.Core.Domain;

public sealed class Hero
{
    internal Hero(HeroDefinition definition)
    {
        Definition = definition;
        Energy = definition.StartingEnergy;
    }

    public HeroDefinition Definition { get; }
    public string Id => Definition.Id;
    public int Energy { get; internal set; }
    public string? AssignedQuestId { get; internal set; }
    public bool IsFree => AssignedQuestId is null;
}
