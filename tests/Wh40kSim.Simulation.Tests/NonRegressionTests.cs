using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;

namespace Wh40kSim.Simulation.Tests;

/// <summary>
/// Résultats exacts à graine fixe. Toute différence signale un changement de la séquence d'attaque ou de la
/// consommation des dés : si elle est voulue (nouvelle règle…), recapturer ces valeurs en le justifiant.
/// <para>Historique : capturés le 2026-10-27 avant le déplacement du simulateur dans Wh40kSim.Simulation ;
/// recapturés le 2026-10-27 au passage au générateur xoshiro256** (ticket #10), seul changement de cette étape.
/// Avant de recapturer, les nouveaux résultats ont été vérifiés : conformes à la théorie sur les cas exacts et à un
/// générateur indépendant sur ces deux scénarios (l'ancien générateur surestimait les PV retirés d'environ 0,1 %).
/// Scénario riche recapturé le 2026-10-30 (phase 3, étape 10) : la relance « tout sauf critique » de toutes les armes
/// n'existe plus ; relance complète des blessures et pêche aux critiques sur le plasma (Devastating Wounds) seulement, les
/// autres armes relançant leurs échecs. L'exemple, sans cette relance, est resté identique.</para>
/// </summary>
public class NonRegressionTests
{
    private const int Iterations = 100_000;

    [Fact]
    public void Example_scenario_gives_the_reference_result()
    {
        var scenario = ScenarioJson.Load(Path.Combine(Build.RepositoryRoot(), "scenarios", "exemple-escouade-vs-leader.json"));

        var result = Simulator.Run(scenario, Iterations, seed: 42);

        Assert.Equal([387L, 4686, 15757, 25636, 25296, 16711, 7834, 2781, 840, 66, 6, 0], result.Kills.Counts);
        Assert.Equal(
            [58L, 329, 2097, 2589, 9845, 5912, 18594, 7042, 19978, 5318, 13850, 2861, 6769, 1065, 2498, 283, 823, 17, 63, 3, 6, 0, 0, 0, 0],
            result.Damage.Counts);
        AssertKilledPerGroup(result, 371_614, 78, 0);
    }

    [Fact]
    public void Scenario_using_most_rules_gives_the_reference_result()
    {
        var result = Simulator.Run(RichScenario(), Iterations, seed: 2026);

        Assert.Equal([636L, 4646, 13944, 22649, 23966, 18115, 11358, 4286, 352, 36, 12], result.Kills.Counts);
        Assert.Equal(
            [56L, 179, 1209, 778, 4514, 2511, 9093, 5797, 11588, 9202, 10708, 10778, 7877, 9568, 3576, 6377, 1468, 3774, 294, 245, 289, 40, 31, 27, 0, 6, 3, 12],
            result.Damage.Counts);
        AssertKilledPerGroup(result, 359_356, 770, 28_192, 12);
    }

    private static void AssertKilledPerGroup(SimulationResult result, params long[] totals)
    {
        for (int g = 0; g < totals.Length; g++)
        {
            Assert.Equal((double)totals[g] / Iterations, result.MeanKilledInGroup(g));
        }
    }

    /// <summary>
    /// Pools mêlant tir et mêlée, Sustained Hits D3, Lethal Hits, Rapid Fire, Devastating Wounds, Precision, Melta,
    /// Twin-linked, Lance, critique sur 5+, couvert, modificateurs, relances, FNP, invulnérables, réduction de dégâts,
    /// deux PERSONNAGES avec une cible de Precision et une Endurance qui change en cours de simulation.
    /// </summary>
    private static Scenario RichScenario() => new()
    {
        Attacker = new AttackerUnit
        {
            Boosts = new AttackerBoosts { HitRoll = 1, HitReroll = RerollPolicy.Ones, WoundReroll = RerollPolicy.Failed },
            Weapons =
            [
                new WeaponGroup { ModelCount = 5, Weapon = new WeaponProfile { Name = "Bolter", Attacks = 2, Skill = 3, Strength = 4, Abilities = new WeaponAbilities { SustainedHits = DiceExpression.Parse("D3"), LethalHits = true, RapidFire = 1 } } },
                new WeaponGroup { ModelCount = 2, Weapon = new WeaponProfile { Name = "Plasma", Attacks = DiceExpression.Parse("D3"), Skill = 3, Strength = 8, Ap = -3, Damage = 2, Abilities = new WeaponAbilities { DevastatingWounds = true, Precision = true, Melta = 2 }, FishForCriticalWounds = true } },
                new WeaponGroup { ModelCount = 2, Weapon = new WeaponProfile { Name = "Épée", Type = WeaponType.Melee, Attacks = 3, Skill = 3, Strength = 5, Ap = -2, Damage = DiceExpression.Parse("D3+1"), Abilities = new WeaponAbilities { TwinLinked = true, Lance = true, CriticalHitOn = 5 } } },
            ],
        },
        Defender = new DefenderUnit
        {
            Boosts = new DefenderBoosts { InCover = true, HitRoll = -1 },
            Groups =
            [
                new ModelGroup { Name = "Gardes", Count = 6, Toughness = 4, Wounds = 2, Save = 3, FeelNoPain = 6 },
                new ModelGroup { Name = "Lourds", Count = 2, Toughness = 5, Wounds = 3, Save = 2, InvulnerableSave = 4, DamageReduction = 1 },
                new ModelGroup { Name = "Leader", IsCharacter = true, Toughness = 4, Wounds = 5, Save = 2, InvulnerableSave = 4, FeelNoPain = 5 },
                new ModelGroup { Name = "Lieutenant", IsCharacter = true, Toughness = 4, Wounds = 4, Save = 3 },
            ],
        },
        Context = new AttackContext { HalfRange = true, Charged = true, PrecisionTarget = "Leader" },
    };
}
