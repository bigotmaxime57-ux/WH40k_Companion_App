using Wh40kSim.Core.Model;

namespace Wh40kSim.Simulation.Tests;

/// <summary>Tir en overwatch (snap shooting) : rapport par arme sans les armes de mêlée, touches sur 6 seulement.</summary>
public class SnapShootingTests
{
    private static Scenario Squad(bool snapShooting) => new()
    {
        Attacker = new AttackerUnit
        {
            Weapons =
            [
                new WeaponGroup { ModelCount = 10, Weapon = new WeaponProfile { Name = "Bolter", Attacks = 2, Skill = 3, Strength = 4 } },
                new WeaponGroup { ModelCount = 10, Weapon = new WeaponProfile { Name = "Épée", Type = WeaponType.Melee, Attacks = 3, Skill = 3, Strength = 5 } },
            ],
        },
        Defender = new DefenderUnit { Groups = [new ModelGroup { Name = "Cibles", Count = 20, Save = 7 }] },
        Context = new AttackContext { SnapShooting = snapShooting },
    };

    [Fact]
    public void Melee_weapons_are_left_out_of_the_weapon_report()
    {
        var result = Simulator.Run(Squad(snapShooting: true), 10_000, seed: 31);

        var pool = Assert.Single(result.Pools);
        Assert.Equal(["Bolter"], pool.Weapons.Select(w => w.Weapon.Name));
        Assert.Equal(1, pool.DamageShare, 10);
    }

    [Fact]
    public void Hits_only_on_sixes()
    {
        // 20 attaques F4 contre E4 sans sauvegarde : 20 × 1/6 × 1/2 = 1,67 figurine tuée en moyenne (contre 6,67 en tir normal)
        var snap = Simulator.Run(Squad(snapShooting: true), 200_000, seed: 32);

        Assert.Equal(20.0 / 12, snap.Kills.Mean, 0.02);
        Assert.Equal(20.0 / 6, snap.Pools[0].Conversion!.MeanHits, 0.02);
    }
}
