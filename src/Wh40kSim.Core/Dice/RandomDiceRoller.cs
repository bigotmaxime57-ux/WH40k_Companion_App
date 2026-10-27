namespace Wh40kSim.Core.Dice;

/// <summary>
/// Dés aléatoires (xoshiro256**, voir <see cref="Xoshiro256StarStar"/>). Avec une graine (seed) fixe, la séquence de
/// jets est reproductible.
/// </summary>
public sealed class RandomDiceRoller : IDiceRoller
{
    private readonly Xoshiro256StarStar _generator;

    public RandomDiceRoller(ulong seed) => _generator = Xoshiro256StarStar.FromSeed(seed);

    /// <summary>
    /// Tirage sans biais de modulo (méthode de Lemire) : 32 bits aléatoires multipliés par le nombre de faces, la
    /// partie haute donne la face ; les rares tirages qui favoriseraient certaines faces sont rejetés et refaits.
    /// </summary>
    public int Roll(int sides)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sides, 1);
        uint range = (uint)sides;
        ulong product = NextUInt32() * (ulong)range;
        uint low = (uint)product;
        if (low < range)
        {
            uint threshold = (0u - range) % range; // 2^32 mod range
            while (low < threshold)
            {
                product = NextUInt32() * (ulong)range;
                low = (uint)product;
            }
        }
        return (int)(product >> 32) + 1;
    }

    // Les bits de poids fort de xoshiro256** sont les meilleurs
    private uint NextUInt32() => (uint)(_generator.NextUInt64() >> 32);
}
