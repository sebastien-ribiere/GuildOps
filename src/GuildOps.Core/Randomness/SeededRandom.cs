namespace GuildOps.Core.Randomness;

/// <summary>
/// Générateur SplitMix64 : algorithme figé dans ce code, donc identique sur tout OS et toute version de .NET
/// (System.Random n'offre pas cette garantie). Le léger biais du modulo est négligeable pour des bornes &lt;= 100.
/// </summary>
public sealed class SeededRandom : ISeededRandom
{
    private ulong _state;

    public SeededRandom(long seed) => _state = unchecked((ulong)seed);

    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return (int)(NextUInt64() % (ulong)maxExclusive);
    }

    private ulong NextUInt64()
    {
        unchecked
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
