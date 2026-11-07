using GuildOps.Core.Data;
using GuildOps.Core.Domain;

namespace GuildOps.Core.Engine;

/// <summary>Règles R2 à R6 : une seule implémentation, utilisée par le générateur d'offres et par la session.</summary>
public static class AssignmentRules
{
    public static int EngagementCost(GameRules rules, Hero hero, QuestTemplate quest) =>
        rules.Categories[quest.Category].WaiveHeroEngagementCost ? 0 : hero.Definition.BaseCostCopper;

    public static List<RefusalReason> Evaluate(GameRules rules, Hero hero, QuestTemplate quest, int treasuryCopper)
    {
        var reasons = new List<RefusalReason>();
        if (!hero.IsFree) reasons.Add(RefusalReason.HeroBusy);
        if (hero.Definition.Skill != quest.Skill) reasons.Add(RefusalReason.SkillMismatch);
        if (hero.Definition.Level < quest.MinLevel) reasons.Add(RefusalReason.LevelTooLow);
        if (hero.Energy < quest.EnergyCost) reasons.Add(RefusalReason.NotEnoughEnergy);
        if (treasuryCopper < EngagementCost(rules, hero, quest)) reasons.Add(RefusalReason.NotEnoughTreasury);
        return reasons;
    }
}
