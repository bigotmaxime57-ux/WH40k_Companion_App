namespace Wh40kSim.Core.Dice;

/// <summary>Dés aléatoires. Avec une graine (seed) fixe, la séquence de jets est reproductible.</summary>
public sealed class RandomDiceRoller : IDiceRoller
{
    private readonly Random _random;

    public RandomDiceRoller(int seed) => _random = new Random(seed);

    public RandomDiceRoller(Random random) => _random = random;

    public int Roll(int sides) => _random.Next(1, sides + 1);
}
