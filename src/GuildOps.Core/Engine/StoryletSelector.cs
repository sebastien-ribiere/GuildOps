using GuildOps.Core.Domain;
using GuildOps.Core.Randomness;

namespace GuildOps.Core.Engine;

public sealed record MissionContext(string HeroId, QuestTemplate Template, bool Success);

/// <summary>
/// Choisit au plus UN storylet par mission. Les storylets éligibles sont parcourus par priorité décroissante puis par id ;
/// chacun tire sa probabilité dans le flux seedé jusqu'au premier qui se déclenche. Aucun tirage n'est consommé sans storylet éligible.
/// </summary>
public static class StoryletSelector
{
    public static bool Matches(Storylet s, MissionContext m)
    {
        var c = s.Conditions;
        if (c.Outcome == OutcomeFilter.Success && !m.Success) return false;
        if (c.Outcome == OutcomeFilter.Failure && m.Success) return false;
        if (c.HeroIds.Count > 0 && !c.HeroIds.Contains(m.HeroId)) return false;
        if (c.Categories.Count > 0 && !c.Categories.Contains(m.Template.Category)) return false;
        if (c.TemplateIds.Count > 0 && !c.TemplateIds.Contains(m.Template.Id)) return false;
        return true;
    }

    public static Storylet? Select(IReadOnlyList<Storylet> storylets, MissionContext mission, ISeededRandom rng)
    {
        foreach (var s in storylets.Where(x => Matches(x, mission)).OrderByDescending(x => x.Priority).ThenBy(x => x.Id, StringComparer.Ordinal))
            if (rng.NextInt(100) < s.ProbabilityPercent) return s;
        return null;
    }
}
