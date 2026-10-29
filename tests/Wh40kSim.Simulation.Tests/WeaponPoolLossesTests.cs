using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Simulation.Tests;

/// <summary>Rapport par arme : pertes par pool cohérentes avec les totaux, tirs sur une unité déjà détruite.</summary>
public class WeaponPoolLossesTests
{
    /// <summary>Canon puis bolters et pistolets (même pool) contre 10 figurines à 2 PV.</summary>
    private static Scenario Squad(int bolters = 10) => new()
    {
        Attacker = new AttackerUnit
        {
            Weapons =
            [
                new WeaponGroup
                {
                    Weapon = new WeaponProfile { Name = "Canon", Attacks = DiceExpression.Parse("D6"), Skill = 3, Strength = 9, Ap = -2, Damage = DiceExpression.Parse("D3") },
                },
                new WeaponGroup { ModelCount = bolters, Weapon = new WeaponProfile { Name = "Bolter", Attacks = 2, Skill = 3, Strength = 4 } },
                new WeaponGroup { ModelCount = 2, Weapon = new WeaponProfile { Name = "Pistolet", Attacks = 1, Skill = 3, Strength = 4 } },
            ],
        },
        Defender = new DefenderUnit { Groups = [new ModelGroup { Name = "Cibles", Count = 10, Wounds = 2, Save = 4 }] },
    };

    [Fact]
    public void Identical_weapons_share_a_pool_named_after_them_in_resolution_order()
    {
        var result = Simulator.Run(Squad(), 10_000, seed: 21);

        Assert.Equal(2, result.Pools.Count);
        Assert.Equal(["Canon"], result.Pools[0].Weapons.Select(w => w.Weapon.Name));
        Assert.Equal(["Bolter", "Pistolet"], result.Pools[1].Weapons.Select(w => w.Weapon.Name));
        Assert.Equal([0, 1], result.Pools.Select(p => p.Index));
    }

    [Fact]
    public void Pool_losses_add_up_to_the_real_totals()
    {
        var result = Simulator.Run(Squad(), 100_000, seed: 22);

        Assert.Equal(result.Damage.Mean, result.Pools.Sum(p => p.Damage.Mean), 10);
        Assert.Equal(result.Kills.Mean, result.Pools.Sum(p => p.MeanKills), 10);
        Assert.Equal(1, result.Pools.Sum(p => p.DamageShare), 10);
        Assert.Equal(1, result.Pools.Sum(p => p.KillShare), 10);
        Assert.All(result.Pools, p => Assert.True(p.MeanRawDamage >= p.Damage.Mean));
        // Canon : au plus 6 attaques, donc au plus 6 figurines achevées
        Assert.Equal(0, result.Pools[0].Kills.ProbabilityAtLeast(7));
        Assert.True(result.Pools[0].Kills.ProbabilityAtLeast(1) > 0);
        Assert.True(result.Pools[0].WastedShare > 0);      // canon D3 contre 2 PV : des dégâts perdus
        Assert.Equal(0, result.Pools[1].MeanWastedDamage, 10);   // armes D1 : rien à perdre par figurine
    }

    [Fact]
    public void Only_later_pools_can_fire_on_a_destroyed_unit()
    {
        var result = Simulator.Run(Squad(bolters: 40), 50_000, seed: 23);

        Assert.Equal(0, result.Pools[0].ProbabilityUnitAlreadyDestroyed);
        // Le canon ne détruit jamais l'unité à lui seul (au plus 6 morts sur 10) : les bolters tirent toujours
        Assert.Equal(0, result.Pools[1].ProbabilityUnitAlreadyDestroyed);

        // Ordre inversé : 40 bolters détruisent parfois (≈ 5 %) l'unité avant que le canon ne tire
        var reversed = Squad(bolters: 40);
        reversed = reversed with { Attacker = reversed.Attacker with { Weapons = [.. reversed.Attacker.Weapons.Skip(1), reversed.Attacker.Weapons[0]] } };
        var late = Simulator.Run(reversed, 50_000, seed: 23);
        Assert.InRange(late.Pools[1].ProbabilityUnitAlreadyDestroyed, 0.01, 1.0);
        Assert.True(late.Pools[1].ProbabilityUnitAlreadyDestroyed <= late.ProbabilityWipe);
    }
}
