using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Simulation.Tests;

/// <summary>Distribution des dégâts bruts et gaspillage (dégâts perdus au-delà des PV de la figurine touchée).</summary>
public class RawDamageTests
{
    /// <summary>Armes à dégâts aléatoires contre une petite unité avec leader : gaspillage et marge à la fois.</summary>
    private static Scenario Mixed() => new()
    {
        Attacker = new AttackerUnit
        {
            Weapons =
            [
                new WeaponGroup { ModelCount = 5, Weapon = new WeaponProfile { Attacks = 2, Skill = 3, Strength = 5, Ap = -1, Damage = DiceExpression.Parse("D3") } },
                new WeaponGroup { ModelCount = 2, Weapon = new WeaponProfile { Attacks = DiceExpression.Parse("D3"), Skill = 3, Strength = 8, Ap = -2, Damage = DiceExpression.Parse("D6") } },
            ],
        },
        Defender = new DefenderUnit
        {
            Groups =
            [
                new ModelGroup { Name = "Gardes", Count = 4, Toughness = 4, Wounds = 2, Save = 4 },
                new ModelGroup { Name = "Leader", IsCharacter = true, Toughness = 4, Wounds = 4, Save = 3 },
            ],
        },
    };

    [Fact]
    public void Raw_damage_is_never_below_the_wounds_removed_including_the_excess()
    {
        var result = Simulator.Run(Mixed(), 100_000, seed: 5);

        Assert.True(result.RawDamage.Mean > result.ExtendedDamage.Mean);
        Assert.True(result.MeanWastedDamage > 0);
        Assert.InRange(result.WastedShare, 0.0, 1.0);
        // Distribution par distribution : P(bruts ≥ k) ≥ P(PV retirés et excédent ≥ k) pour tout k
        for (int k = 0; k <= result.ExtendedDamage.MaxValue; k++)
        {
            Assert.True(result.RawDamage.ProbabilityAtLeast(k) >= result.ExtendedDamage.ProbabilityAtLeast(k));
        }
    }

    [Fact]
    public void Damage_one_weapons_waste_nothing()
    {
        var scenario = Build.Scenario(new WeaponProfile { Attacks = 3, Skill = 3, Strength = 4 }, Build.Horde(save: 5, models: 10), models: 10);

        var result = Simulator.Run(scenario, 50_000, seed: 3);

        Assert.Equal(result.ExtendedDamage.Counts, result.RawDamage.Counts);
        Assert.Equal(0, result.MeanWastedDamage);
        Assert.Equal(0, result.WastedShare);
    }

    [Fact]
    public void Raw_damage_of_a_fixed_damage_weapon_has_the_exact_expected_value()
    {
        // 1 attaque Torrent F8 contre E4 (blesse sur 2+), sans sauvegarde, D3 fixe contre une figurine à 1 PV :
        // bruts = 3 si la blessure passe (5/6), sinon 0 ; PV retirés = 1 ou 0 ; gaspillage moyen exact = 2 × 5/6
        var weapon = new WeaponProfile { Attacks = 1, Strength = 8, Damage = DiceExpression.Parse("3"), Abilities = new WeaponAbilities { Torrent = true } };
        var result = Simulator.Run(Build.Scenario(weapon, Build.Horde(models: 5)), 200_000, seed: 9);

        Assert.Equal(new[] { 0, 3 }, Enumerable.Range(0, result.RawDamage.MaxValue + 1).Where(k => result.RawDamage.Counts[k] > 0));
        Assert.Equal(result.Damage.Counts[1], result.RawDamage.Counts[3]);
        Assert.Equal(2 * result.Damage.Mean, result.MeanWastedDamage, precision: 10);
        Assert.Equal(2.0 / 3, result.WastedShare, precision: 10);
        // Moyenne théorique 2,5 ; écart-type d'une moyenne de 200 000 tirages ≈ 0,0025
        Assert.InRange(result.RawDamage.Mean, 2.5 - 0.0125, 2.5 + 0.0125);
    }
}
