using Wh40kSim.Core.Dice;

namespace Wh40kSim.Core.Tests;

public class RandomDiceRollerTests
{
    [Fact]
    public void Xoshiro_gives_the_reference_sequence()
    {
        // Sorties de l'implémentation de référence (Blackman et Vigna) pour l'état { 1, 2, 3, 4 }
        var generator = new Xoshiro256StarStar(1, 2, 3, 4);

        Assert.Equal(11520UL, generator.NextUInt64());
        Assert.Equal(0UL, generator.NextUInt64());
        Assert.Equal(1509978240UL, generator.NextUInt64());
        Assert.Equal(1215971899390074240UL, generator.NextUInt64());
    }

    [Fact]
    public void Xoshiro_rejects_an_all_zero_state()
    {
        Assert.Throws<ArgumentException>(() => new Xoshiro256StarStar(0, 0, 0, 0));
    }

    [Fact]
    public void Same_seed_gives_the_same_rolls()
    {
        var first = new RandomDiceRoller(42);
        var second = new RandomDiceRoller(42);

        Assert.Equal(Rolls(first, 6, 1000), Rolls(second, 6, 1000));
    }

    [Fact]
    public void Close_seeds_give_different_rolls()
    {
        Assert.NotEqual(Rolls(new RandomDiceRoller(1), 6, 100), Rolls(new RandomDiceRoller(2), 6, 100));
    }

    [Fact]
    public void A_one_sided_die_always_gives_one()
    {
        Assert.All(Rolls(new RandomDiceRoller(3), 1, 100), roll => Assert.Equal(1, roll));
    }

    [Fact]
    public void A_die_needs_at_least_one_side()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RandomDiceRoller(3).Roll(0));
    }

    // Khi-deux à graine fixe (résultat déterministe) : seuils à 99,99 %, un échec signale un vrai défaut du tirage

    [Theory]
    [InlineData(3, 18.4)]  // 2 degrés de liberté
    [InlineData(6, 25.7)]  // 5 degrés de liberté
    public void Faces_are_equally_likely(int sides, double chiSquareLimit)
    {
        const int rolls = 3_000_000;
        var counts = new long[sides + 1];
        var dice = new RandomDiceRoller(2026);
        for (int i = 0; i < rolls; i++)
        {
            int roll = dice.Roll(sides);
            Assert.InRange(roll, 1, sides);
            counts[roll]++;
        }

        double expected = (double)rolls / sides;
        double chiSquare = counts.Skip(1).Sum(c => (c - expected) * (c - expected) / expected);
        Assert.True(chiSquare < chiSquareLimit, $"Khi-deux {chiSquare:F1} ≥ {chiSquareLimit}");
    }

    [Fact]
    public void Consecutive_D6_rolls_are_independent()
    {
        // Paires de jets consécutifs : 36 cases, 35 degrés de liberté
        const int pairs = 3_600_000;
        var counts = new long[7, 7];
        var dice = new RandomDiceRoller(7);
        int previous = dice.D6();
        for (int i = 0; i < pairs; i++)
        {
            int roll = dice.D6();
            counts[previous, roll]++;
            previous = roll;
        }

        double expected = pairs / 36.0;
        double chiSquare = 0;
        for (int a = 1; a <= 6; a++)
        {
            for (int b = 1; b <= 6; b++)
            {
                chiSquare += (counts[a, b] - expected) * (counts[a, b] - expected) / expected;
            }
        }
        Assert.True(chiSquare < 73.4, $"Khi-deux {chiSquare:F1} ≥ 73,4");
    }

    private static int[] Rolls(IDiceRoller dice, int sides, int count) =>
        Enumerable.Range(0, count).Select(_ => dice.Roll(sides)).ToArray();
}
