using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Simulation.Tests;

/// <summary>Rapport par arme : pertes par pool cohÃ©rentes avec les totaux, tirs sur une unitÃ© dÃ©jÃ  dÃ©truite.</summary>
public class WeaponPoolLossesTests
{
    /// <summary>Canon puis bolters et pistolets (mÃªme pool) contre 10 figurines Ã  2 PV.</summary>
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
        // Canon : au plus 6 attaques, donc au plus 6 figurines achevÃ©es
        Assert.Equal(0, result.Pools[0].Kills.ProbabilityAtLeast(7));
        Assert.True(result.Pools[0].Kills.ProbabilityAtLeast(1) > 0);
        Assert.True(result.Pools[0].WastedShare > 0);      // canon D3 contre 2 PV : des dÃ©gÃ¢ts perdus
        Assert.Equal(0, result.Pools[1].MeanWastedDamage, 10);   // armes D1 : rien Ã  perdre par figurine
    }

    [Fact]
    public void Waste_per_activation_matches_the_mean_waste_and_only_counts_when_the_unit_survives()
    {
        var result = Simulator.Run(Squad(), 100_000, seed: 24);

        Assert.Equal(result.MeanWastedDamage, result.Waste.Mean, 10);
        Assert.InRange(result.Waste.ProbabilityExactly(0), 0.01, 0.99);   // canon D3 : parfois rien, parfois des pertes
        Assert.NotNull(result.MeanWasteWhenUnitSurvives);
        Assert.True(result.MeanWasteWhenUnitSurvives > 0);

        // Part de chaque arme dans les PV perdus : 100 % au total pour chaque valeur atteinte, tout vient du canon (D3)
        for (int k = 1; k <= result.Waste.MaxValue; k++)
        {
            if (result.Waste.ProbabilityExactly(k) > 0)
            {
                Assert.Equal(1, result.Pools.Sum(p => p.WasteShareByTotal[k]), 10);
                Assert.Equal(1, result.Pools[0].WasteShareByTotal[k], 10);
            }
        }
    }

    [Fact]
    public void D1_weapons_never_waste_and_an_always_destroyed_unit_has_no_waste_when_it_survives()
    {
        // Bolters seuls (D1) : aucune perte possible par figurine
        var bolters = Squad() with { Attacker = Squad().Attacker with { Weapons = [.. Squad().Attacker.Weapons.Skip(1)] } };
        var d1 = Simulator.Run(bolters, 10_000, seed: 25);
        Assert.Equal(1, d1.Waste.ProbabilityExactly(0));
        Assert.Equal(0, d1.MeanWasteWhenUnitSurvives);

        // Une figurine Ã  1 PV, touchÃ©e et blessÃ©e Ã  coup sÃ»r, sans sauvegarde : toujours dÃ©truite
        var sure = new Scenario
        {
            Attacker = new AttackerUnit { Weapons = [new WeaponGroup { Weapon = new WeaponProfile { Name = "Lance", Attacks = 20, Skill = 2, Strength = 10, Damage = 3, Abilities = new WeaponAbilities { Torrent = true } } }] },
            Defender = new DefenderUnit { Groups = [new ModelGroup { Name = "Cible", Count = 1, Wounds = 1, Toughness = 1, Save = 7 }] },
        };
        var wiped = Simulator.Run(sure, 1_000, seed: 26);
        Assert.Equal(1, wiped.ProbabilityWipe);
        Assert.Null(wiped.MeanWasteWhenUnitSurvives);
    }

    [Fact]
    public void Conversion_rates_match_the_dice_and_keyword_gains_are_estimated()
    {
        var result = Simulator.Run(Squad(), 100_000, seed: 27);

        // Bolters et pistolets : 22 attaques, touche sur 3+, F4 contre E4 (4+), sauvegarde 4+ sans PA
        var bolters = result.Pools[1].Conversion!;
        Assert.Equal(22, bolters.MeanAttacks, 10);
        Assert.Equal(2.0 / 3, bolters.HitRate, 2);
        Assert.Equal(0.5, bolters.WoundRate, 2);
        Assert.Equal(0.5, bolters.FailedSaveRate, 2);
        Assert.Equal(0, bolters.LethalHitsGain);

        // Lethal Hits : une touche sur 6 (critiques) blesse sans jet ; sans eux, la moitié aurait raté (4+)
        var lethal = Squad() with
        {
            Attacker = Squad().Attacker with
            {
                Weapons = [.. Squad().Attacker.Weapons.Skip(1).Select(g => g with { Weapon = g.Weapon with { Abilities = new() { LethalHits = true } } })],
            },
        };
        var withLethal = Simulator.Run(lethal, 100_000, seed: 28).Pools[0].Conversion!;
        Assert.Equal(22.0 / 6, withLethal.MeanLethalHits, 1);
        Assert.Equal(withLethal.MeanLethalHits / 2, withLethal.LethalHitsGain, 1);
    }

    [Fact]
    public void A_pool_that_never_fires_on_a_living_unit_has_no_conversion()
    {
        // 20 attaques Torrent F10 contre une figurine E1 à 1 PV : le second pool tire toujours dans le vide
        var lance = new WeaponProfile { Name = "Lance", Attacks = 20, Strength = 10, Abilities = new WeaponAbilities { Torrent = true } };
        var scenario = new Scenario
        {
            Attacker = new AttackerUnit { Weapons = [new WeaponGroup { Weapon = lance }, new WeaponGroup { Weapon = lance with { Name = "Pistolet", Attacks = 1, Strength = 9 } }] },
            Defender = new DefenderUnit { Groups = [new ModelGroup { Name = "Cible", Count = 1, Wounds = 1, Toughness = 1 }] },
        };

        var result = Simulator.Run(scenario, 1_000, seed: 29);

        Assert.NotNull(result.Pools[0].Conversion);
        Assert.Null(result.Pools[1].Conversion);
    }

    [Fact]
    public void Only_later_pools_can_fire_on_a_destroyed_unit()
    {
        var result = Simulator.Run(Squad(bolters: 40), 50_000, seed: 23);

        Assert.Equal(0, result.Pools[0].ProbabilityUnitAlreadyDestroyed);
        // Le canon ne dÃ©truit jamais l'unitÃ© Ã  lui seul (au plus 6 morts sur 10) : les bolters tirent toujours
        Assert.Equal(0, result.Pools[1].ProbabilityUnitAlreadyDestroyed);

        // Ordre inversÃ© : 40 bolters dÃ©truisent parfois (â‰ˆ 5 %) l'unitÃ© avant que le canon ne tire
        var reversed = Squad(bolters: 40);
        reversed = reversed with { Attacker = reversed.Attacker with { Weapons = [.. reversed.Attacker.Weapons.Skip(1), reversed.Attacker.Weapons[0]] } };
        var late = Simulator.Run(reversed, 50_000, seed: 23);
        Assert.InRange(late.Pools[1].ProbabilityUnitAlreadyDestroyed, 0.01, 1.0);
        Assert.True(late.Pools[1].ProbabilityUnitAlreadyDestroyed <= late.ProbabilityWipe);
    }
}
