namespace GuildOps.Core.Domain;

/// <summary>Entrée de journal. Le temps est logique (numéro de séquence), jamais l'horloge réelle.</summary>
public sealed record GameEvent(int Sequence, int Day, GameEventType Type, string Message);
