using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;

namespace Wh40kSim.Simulation.Tests;

/// <summary>
/// Résultats exacts à graine fixe, capturés le 2026-10-27 avec le simulateur d'origine (dans le Core) juste avant son
/// déplacement dans Wh40kSim.Simulation. Toute différence signale un changement de la séquence d'attaque ou de la
/// consommation des dés : si elle est voulue (nouvelle règle…), recapturer ces valeurs en le justifiant.
/// </summary>
public class NonRegressionTests
{
    private const int Iterations = 100_000;

    [Fact]
    public void Example_scenario_gives_the_reference_result()
    {
        var scenario = ScenarioJson.Load(Path.Combine(Build.RepositoryRoot(), "scenarios", "exemple-escouade-vs-leader.json"));

        var result = Simulator.Run(scenario, Iterations, seed: 42);

        Assert.Equal([419L, 4578, 15590, 25830, 24949, 16924, 7990, 2814, 857, 36, 13, 0], result.Kills.Counts);
        Assert.Equal(
            [54L, 365, 2092, 2486, 9733, 5857, 18724, 7106, 19700, 5249, 14096, 2828, 6948, 1042, 2508, 306, 838, 19, 34, 2, 12, 0, 0, 1, 0],
            result.Damage.Counts);
        AssertKilledPerGroup(result, 372_550, 62, 0);
    }

    [Fact]
    public void Scenario_using_most_rules_gives_the_reference_result()
    {
        var result = Simulator.Run(RichScenario(), Iterations, seed: 2026);

        Assert.Equal([1866L, 9662, 21063, 25870, 21246, 12280, 5886, 1983, 133, 9, 2], result.Kills.Counts);
        Assert.Equal(
            [207L, 518, 2669, 1574, 7435, 4173, 11767, 8227, 11984, 11135, 8786, 10677, 4936, 7285, 1989, 3881, 626, 1786, 114, 86, 109, 13, 12, 8, 0, 1, 0, 2],
            result.Damage.Counts);
        AssertKilledPerGroup(result, 297_952, 239, 27_951, 2);
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
            Boosts = new AttackerBoosts { HitRoll = 1, HitReroll = RerollPolicy.Ones, WoundReroll = RerollPolicy.NonCritical },
            Weapons =
            [
                new WeaponGroup { ModelCount = 5, Weapon = new WeaponProfile { Name = "Bolter", Attacks = 2, Skill = 3, Strength = 4, Abilities = new WeaponAbilities { SustainedHits = DiceExpression.Parse("D3"), LethalHits = true, RapidFire = 1 } } },
                new WeaponGroup { ModelCount = 2, Weapon = new WeaponProfile { Name = "Plasma", Attacks = DiceExpression.Parse("D3"), Skill = 3, Strength = 8, Ap = -3, Damage = 2, Abilities = new WeaponAbilities { DevastatingWounds = true, Precision = true, Melta = 2 } } },
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
