using GuildOps.Core.Data;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using GuildOps.Core.Randomness;

namespace GuildOps.Tests;

/// <summary>
/// RNG scripté : renvoie des valeurs fixes, puis (script épuisé) la valeur maximale, c'est-à-dire "jamais" pour un test
/// de probabilité : aucun storylet ne se déclenche sans que le test l'ait demandé. <see cref="Calls"/> compte les tirages consommés.
/// </summary>
internal sealed class ScriptedRandom(params int[] values) : ISeededRandom
{
    private int _i;
    public int Calls { get; private set; }
    public int NextInt(int maxExclusive)
    {
        Calls++;
        return _i < values.Length ? values[_i++] % maxExclusive : maxExclusive - 1;
    }
}

internal static class TestSupport
{
    public const long Seed = 1142026;

    public static GameData Data { get; } = GameDataLoader.LoadEmbedded();

    /// <summary>Les tests de règles de base n'activent pas les traits ; ceux-ci ont leurs propres tests (<c>TraitTests</c>).</summary>
    public static GameSession NewSession(long seed = Seed) => GameSession.Start(Data, seed, traits: new NoTraitEffects());

    /// <summary>Session dont les tirages de résolution sont forcés (la génération du jour 1 utilise le vrai RNG semé).</summary>
    public static GameSession NewSessionWithRolls(params int[] rolls) => NewSessionWithScript(out _, rolls);

    public static GameSession NewSessionWithScript(out ScriptedRandom script, params int[] rolls)
    {
        var rng = new OverridableRandom(new SeededRandom(Seed));
        var session = GameSession.Start(Data, Seed, _ => rng, new NoTraitEffects());
        script = new ScriptedRandom(rolls);
        rng.Override = script;
        return session;
    }

    /// <summary>Session au RNG réel dont on peut remplacer le flux plus tard (<c>rng.Override = new ScriptedRandom(...)</c>).</summary>
    public static GameSession NewSessionDeferred(out OverridableRandom rng)
    {
        var r = new OverridableRandom(new SeededRandom(Seed));
        rng = r;
        return GameSession.Start(Data, Seed, _ => r, new NoTraitEffects());
    }

    /// <summary>Publie une offre arbitraire (cas que le tirage seedé ne produit pas).</summary>
    public static QuestOffer Inject(GameSession s, string templateId) =>
        new QuestOfferProbe(s).Inject(s.Data.Templates.Single(t => t.Id == templateId));

    /// <summary>Trouve un couple (offre, héros) admissible, de préférence pour la catégorie demandée.</summary>
    public static (string OfferId, string HeroId) FindAssignable(GameSession s, Func<Core.Domain.QuestOffer, bool>? filter = null)
    {
        foreach (var o in s.Offers.Where(o => filter?.Invoke(o) ?? true))
            foreach (var h in s.Heroes)
                if (s.Preview(o.Id, h.Id).CanAssign) return (o.Id, h.Id);
        throw new InvalidOperationException("Aucune affectation admissible dans ce scénario.");
    }

    public static string Snapshot(GameSession s) =>
        $"{s.Guild.TreasuryCopper}|{s.Guild.Reputation}|{s.Guild.MissionsAcceptedToday}|{s.Guild.MissionsSucceeded}|" +
        string.Join(",", s.Heroes.Select(h => $"{h.Id}:{h.Energy}:{h.AssignedQuestId}")) + "|" +
        string.Join(",", s.Offers.Select(o => $"{o.Id}:{o.Template.Id}:{o.State}")) + "|" +
        string.Join(",", s.Quests.Select(q => $"{q.Id}:{q.State}")) + "|" + s.Events.Count;
}

internal sealed class OverridableRandom(ISeededRandom inner) : ISeededRandom
{
    public ISeededRandom? Override { get; set; }
    public int NextInt(int maxExclusive) => (Override ?? inner).NextInt(maxExclusive);
}
