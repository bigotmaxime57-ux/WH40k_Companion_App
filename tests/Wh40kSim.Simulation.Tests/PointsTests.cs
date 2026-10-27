using Wh40kSim.Core.Model;

namespace Wh40kSim.Simulation.Tests;

/// <summary>Points détruits (au prorata des PV retirés) et rendement.</summary>
public class PointsTests
{
    private const int Iterations = 200_000;

    // 6 touches automatiques, F10 contre E4 : blessure sur 2+ (5/6), aucune sauvegarde, 1 dégât
    private static WeaponProfile SixShots => new() { Attacks = 6, Strength = 10, Abilities = new WeaponAbilities { Torrent = true } };

    private static Scenario WithAttackerPoints(Scenario scenario, int? points) =>
        scenario with { Attacker = scenario.Attacker with { Points = points } };

    [Fact]
    public void A_damaged_vehicle_counts_for_its_share_of_points()
    {
        // Char à 12 PV et 180 points : 5 PV retirés en moyenne, jamais détruit, mais 5/12 de sa valeur
        var tank = new DefenderUnit { Groups = [new ModelGroup { Name = "Char", Count = 1, Toughness = 4, Wounds = 12, Points = 180 }] };

        var result = Simulator.Run(Build.Scenario(SixShots, tank), Iterations, seed: 4);

        Assert.Equal(0, result.Kills.Mean);
        Assert.InRange(result.MeanPointsDestroyed!.Value, 75 - 0.5, 75 + 0.5);
    }

    [Fact]
    public void Damage_per_group_adds_up_to_the_damage_dealt()
    {
        var defender = new DefenderUnit
        {
            Groups =
            [
                new ModelGroup { Name = "Gardes", Count = 3, Wounds = 2, Points = 90 },
                new ModelGroup { Name = "Leader", IsCharacter = true, Wounds = 4, Points = 70 },
            ],
        };

        var result = Simulator.Run(Build.Scenario(SixShots with { Damage = 2 }, defender), Iterations, seed: 5);

        Assert.Equal(result.Damage.Mean, result.MeanDamageInGroup(0) + result.MeanDamageInGroup(1), 9);
        double expected = 90 * result.MeanDamageInGroup(0) / 6 + 70 * result.MeanDamageInGroup(1) / 4;
        Assert.Equal(expected, result.MeanPointsDestroyed!.Value, 9);
    }

    [Fact]
    public void A_unit_always_destroyed_gives_exactly_its_points()
    {
        // Une figurine à 1 PV contre 6 blessures sur 2+ : détruite sauf si les 6 jets font 1 (1 chance sur 46 656)
        var target = new DefenderUnit { Groups = [new ModelGroup { Name = "Cible", Points = 100 }] };

        var result = Simulator.Run(Build.Scenario(SixShots, target), Iterations, seed: 6);

        Assert.InRange(result.MeanPointsDestroyed!.Value, 99.9, 100);
    }

    [Fact]
    public void Efficiency_is_points_destroyed_per_attacker_point()
    {
        var tank = new DefenderUnit { Groups = [new ModelGroup { Name = "Char", Wounds = 12, Points = 180 }] };

        var result = Simulator.Run(WithAttackerPoints(Build.Scenario(SixShots, tank), 150), Iterations, seed: 7);

        Assert.Equal(result.MeanPointsDestroyed!.Value / 150, result.Efficiency!.Value, 12);
    }

    [Theory]
    [InlineData(null, 100)]  // attaquant sans points : pas de rendement
    [InlineData(0, 100)]     // attaquant gratuit : rendement indéfini
    public void No_efficiency_without_attacker_points(int? attackerPoints, int defenderPoints)
    {
        var target = new DefenderUnit { Groups = [new ModelGroup { Name = "Cible", Points = defenderPoints }] };

        var result = Simulator.Run(WithAttackerPoints(Build.Scenario(SixShots, target), attackerPoints), 10_000, seed: 8);

        Assert.NotNull(result.MeanPointsDestroyed);
        Assert.Null(result.Efficiency);
    }

    [Fact]
    public void No_points_destroyed_when_a_group_has_no_points()
    {
        var defender = new DefenderUnit
        {
            Groups =
            [
                new ModelGroup { Name = "Gardes", Count = 3, Points = 90 },
                new ModelGroup { Name = "Leader", IsCharacter = true, Wounds = 4 },
            ],
        };

        var result = Simulator.Run(WithAttackerPoints(Build.Scenario(SixShots, defender), 150), 10_000, seed: 9);

        Assert.Null(result.MeanPointsDestroyed);
        Assert.Null(result.Efficiency);
    }
}
