using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;
using Wh40kSim.Simulation.Statistics;

namespace Wh40kSim.Simulation.Tests;

/// <summary>Distributions prolongées au-delà de la destruction de l'unité (excédent).</summary>
public class ExcessTests
{
    /// <summary>Petite unité avec leader face à beaucoup de tirs : détruite dans la plupart des cas.</summary>
    private static Scenario OftenWiped() => new()
    {
        Attacker = new AttackerUnit
        {
            Weapons =
            [
                new WeaponGroup { ModelCount = 10, Weapon = new WeaponProfile { Attacks = 2, Skill = 3, Strength = 4, Ap = -1 } },
                new WeaponGroup { ModelCount = 3, Weapon = new WeaponProfile { Attacks = DiceExpression.Parse("D6"), Skill = 3, Strength = 9, Ap = -2, Damage = DiceExpression.Parse("D3") } },
            ],
        },
        Defender = new DefenderUnit
        {
            Groups =
            [
                new ModelGroup { Name = "Gardes", Count = 4, Toughness = 4, Wounds = 1, Save = 5 },
                new ModelGroup { Name = "Leader", IsCharacter = true, Toughness = 4, Wounds = 3, Save = 3 },
            ],
        },
    };

    /// <summary>
    /// Sous le maximum, la distribution prolongée a exactement les mêmes effectifs que la réelle ; au-delà, elle répartit
    /// les cas où l'unité est détruite.
    /// </summary>
    private static void AssertExtends(DiscreteDistribution real, DiscreteDistribution extended)
    {
        int max = real.MaxValue;
        Assert.True(extended.MaxValue >= max);
        Assert.Equal(real.Counts.Take(max), extended.Counts.Take(max));
        Assert.Equal(real.Counts[max], extended.Counts.Skip(max).Sum());
    }

    [Fact]
    public void Extended_distributions_match_the_real_ones_below_the_maximum()
    {
        var result = Simulator.Run(OftenWiped(), 100_000, seed: 11);

        AssertExtends(result.Kills, result.ExtendedKills);
        AssertExtends(result.Damage, result.ExtendedDamage);
    }

    [Fact]
    public void Wipe_probability_equals_reaching_the_maximum_in_the_extended_distribution()
    {
        var result = Simulator.Run(OftenWiped(), 100_000, seed: 11);

        Assert.True(result.ProbabilityWipe > 0.5);
        Assert.Equal(result.ProbabilityWipe, result.ExtendedKills.ProbabilityAtLeast(result.TotalModels));
        Assert.True(result.ExtendedKills.MaxValue > result.TotalModels);
        Assert.True(result.ExtendedKills.Mean > result.Kills.Mean);
    }

    [Fact]
    public void Without_wipe_the_extended_distributions_are_the_real_ones()
    {
        var scenario = Build.Scenario(new WeaponProfile { Skill = 3 }, Build.Horde(models: 100), models: 5);

        var result = Simulator.Run(scenario, 20_000, seed: 3);

        Assert.Equal(0, result.ProbabilityWipe);
        Assert.Equal(result.Kills.Counts, result.ExtendedKills.Counts);
        Assert.Equal(result.Damage.Counts, result.ExtendedDamage.Counts);
    }

    [Fact]
    public void Excess_profile_is_the_first_bodyguard_group()
    {
        var result = Simulator.Run(OftenWiped(), 10, seed: 1);

        Assert.Equal("Gardes", result.ExcessProfile.Name);
    }

    [Fact]
    public void Same_seed_gives_identical_extended_results()
    {
        var first = Simulator.Run(OftenWiped(), 50_000, seed: 7);
        var second = Simulator.Run(OftenWiped(), 50_000, seed: 7);

        Assert.Equal(first.ExtendedKills.Counts, second.ExtendedKills.Counts);
        Assert.Equal(first.ExtendedDamage.Counts, second.ExtendedDamage.Counts);
    }
}
