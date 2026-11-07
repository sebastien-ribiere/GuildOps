using GuildOps.Core.Data;
using GuildOps.Core.Domain;

namespace GuildOps.Core.Engine;

/// <summary>Règles R2 à R6 : une seule implémentation, utilisée par le générateur d'offres et par la session.</summary>
public static class AssignmentRules
{
    public static int EngagementCost(GameRules rules, Hero hero, QuestTemplate quest) =>
        rules.Categories[quest.Category].WaiveHeroEngagementCost ? 0 : hero.Definition.BaseCostCopper;

    public static List<RefusalReason> Evaluate(GameRules rules, Hero hero, QuestTemplate quest, int treasuryCopper) =>
        Evaluate(rules, hero, quest, treasuryCopper, EngagementCost(rules, hero, quest), quest.EnergyCost);

    /// <summary>Variante avec coûts effectifs (traits compris) : la session l'utilise ; le générateur d'offres reste sur les coûts de base.</summary>
    public static List<RefusalReason> Evaluate(GameRules rules, Hero hero, QuestTemplate quest, int treasuryCopper, int engagementCost, int energyCost)
    {
        var reasons = new List<RefusalReason>();
        if (!hero.IsFree) reasons.Add(RefusalReason.HeroBusy);
        if (hero.Definition.Skill != quest.Skill) reasons.Add(RefusalReason.SkillMismatch);
        if (hero.Definition.Level < quest.MinLevel) reasons.Add(RefusalReason.LevelTooLow);
        if (hero.Energy < energyCost) reasons.Add(RefusalReason.NotEnoughEnergy);
        if (treasuryCopper < engagementCost) reasons.Add(RefusalReason.NotEnoughTreasury);
        return reasons;
    }
}
