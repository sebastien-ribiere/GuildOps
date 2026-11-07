namespace GuildOps.Core.Domain;

public enum Skill { Combat, Magie, Exploration, Ingenierie }

public enum QuestCategory { Corvee, Village, Aventure, Notable, Royale }

public enum OfferState { Available, Accepted, Expired }

public enum GameStatus { InProgress, Finished }

public enum QuestState { Assigned, Succeeded, Failed }

public enum GameEventType { GameStarted, OffersPublished, MissionAssigned, MissionResolved, ReputationChanged, DayEnded, EnergyRecovered, OffersRegenerated, GameFinished }

public enum RefusalReason
{
    UnknownOffer,
    UnknownHero,
    UnknownQuest,
    OfferNotAvailable,
    HeroBusy,
    SkillMismatch,
    LevelTooLow,
    NotEnoughEnergy,
    NotEnoughTreasury,
    DailyLimitReached,
    QuestNotAssigned,
    PendingMissions,
    GameFinished,
    RegenerationDisabled,
    RegenerationAlreadyUsed,
    MissionsAlreadyEngaged,
}
