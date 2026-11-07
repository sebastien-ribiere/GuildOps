namespace GuildOps.Core.Domain;

public sealed class Guild
{
    internal Guild(int treasuryCopper, int reputation, int day)
    {
        TreasuryCopper = treasuryCopper;
        Reputation = reputation;
        Day = day;
    }

    public int TreasuryCopper { get; private set; }
    public int Reputation { get; private set; }
    public int Day { get; }
    public int MissionsAcceptedToday { get; internal set; }
    public int MissionsSucceeded { get; internal set; }

    internal void Debit(int copper)
    {
        if (copper < 0 || copper > TreasuryCopper) throw new InvalidOperationException("Débit invalide : trésorerie insuffisante.");
        TreasuryCopper -= copper;
    }

    internal void Credit(int copper)
    {
        if (copper < 0) throw new InvalidOperationException("Crédit négatif interdit.");
        TreasuryCopper += copper;
    }

    /// <summary>Applique un delta borné à [min, max] ; renvoie la réputation après application.</summary>
    internal int ApplyReputation(int delta, int min, int max)
    {
        Reputation = Math.Clamp(Reputation + delta, min, max);
        return Reputation;
    }
}
