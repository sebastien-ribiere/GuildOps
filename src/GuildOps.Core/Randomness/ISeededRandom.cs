namespace GuildOps.Core.Randomness;

/// <summary>Source de hasard injectable. Le moteur n'utilise jamais System.Random ni l'horloge.</summary>
public interface ISeededRandom
{
    /// <summary>Entier uniforme dans [0, maxExclusive[.</summary>
    int NextInt(int maxExclusive);
}
