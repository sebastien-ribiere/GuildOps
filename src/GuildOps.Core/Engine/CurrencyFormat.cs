namespace GuildOps.Core.Engine;

/// <summary>Conversion d'affichage uniquement : l'état interne reste en cuivres entiers.</summary>
public static class CurrencyFormat
{
    public static string Format(int copper, int copperPerGold)
    {
        if (copper < 0) throw new ArgumentOutOfRangeException(nameof(copper));
        var gold = copper / copperPerGold;
        var rest = copper % copperPerGold;
        if (gold == 0) return $"{rest} cuivre{(rest > 1 ? "s" : "")}";
        return rest == 0 ? $"{gold} or" : $"{gold} or {rest} cuivre{(rest > 1 ? "s" : "")}";
    }
}
