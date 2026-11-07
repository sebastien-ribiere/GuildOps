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

    private readonly HashSet<QuestCategory> _prepared = [];
    private readonly Dictionary<QuestCategory, int> _missionsDone = [];

    /// <summary>Vrai si le héros a préparé cette catégorie (trait à préparation explicite).</summary>
    public bool IsPrepared(QuestCategory category) => _prepared.Contains(category);
    /// <summary>Nombre de missions terminées (réussies ou non) dans la catégorie, depuis le début de la partie.</summary>
    public int MissionsDone(QuestCategory category) => _missionsDone.GetValueOrDefault(category);
    public IReadOnlyCollection<QuestCategory> PreparedCategories => _prepared;

    internal void Prepare(QuestCategory category) => _prepared.Add(category);
    internal void RecordMission(QuestCategory category) => _missionsDone[category] = MissionsDone(category) + 1;
}
