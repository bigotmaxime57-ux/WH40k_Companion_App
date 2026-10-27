using System.Numerics;

namespace Wh40kSim.Core.Dice;

/// <summary>
/// Générateur pseudo-aléatoire xoshiro256** (Blackman et Vigna) : rapide, période 2^256 - 1, sans biais connu.
/// Remplace <c>new Random(graine)</c>, qui garde l'ancien algorithme de .NET (générateur soustractif de Knuth),
/// légèrement biaisé. À graine égale, la séquence est identique sur toutes les machines.
/// </summary>
public sealed class Xoshiro256StarStar
{
    private ulong _s0, _s1, _s2, _s3;

    /// <summary>État initial donné tel quel (tests de référence) ; il ne doit pas être entièrement nul.</summary>
    public Xoshiro256StarStar(ulong s0, ulong s1, ulong s2, ulong s3)
    {
        if ((s0 | s1 | s2 | s3) == 0)
        {
            throw new ArgumentException("L'état du générateur ne doit pas être entièrement nul.");
        }
        (_s0, _s1, _s2, _s3) = (s0, s1, s2, s3);
    }

    /// <summary>
    /// État initial déduit d'une graine par SplitMix64, comme le recommandent les auteurs : des graines proches donnent
    /// des états sans rapport. SplitMix64 est une bijection : ses quatre sorties ne peuvent pas être toutes nulles.
    /// </summary>
    public static Xoshiro256StarStar FromSeed(ulong seed) =>
        new(SplitMix64(ref seed), SplitMix64(ref seed), SplitMix64(ref seed), SplitMix64(ref seed));

    public ulong NextUInt64()
    {
        ulong result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = BitOperations.RotateLeft(_s3, 45);

        return result;
    }

    private static ulong SplitMix64(ref ulong state)
    {
        ulong z = state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
