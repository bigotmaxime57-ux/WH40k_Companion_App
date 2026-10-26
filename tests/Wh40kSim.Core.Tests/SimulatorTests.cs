using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Engine;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;

namespace Wh40kSim.Core.Tests;

/// <summary>
/// Le Monte Carlo doit retrouver les moyennes calculées à la main.
/// Base : 10 attaques, CT 3+ (2/3), F4 contre E4 (1/2), aucune sauvegarde, D1, cibles à 1 PV.
/// </summary>
public class SimulatorTests
{
    private const int Iterations = 200_000;
    private const double Tolerance = 0.03; // ≈ 9 erreurs-types : un échec signale un vrai bug, pas du bruit

    private static WeaponProfile BaseWeapon => new() { Skill = 3, Strength = 4 };

    private static void AssertMeanKills(double expected, Scenario scenario)
    {
        var summary = Simulator.Run(scenario, Iterations, seed: 12345);
        Assert.InRange(summary.MeanKills, expected - Tolerance, expected + Tolerance);
    }

    [Fact]
    public void Basic_profile_matches_the_analytic_mean()
    {
        AssertMeanKills(10 * (2.0 / 3) * 0.5, Build.Scenario(BaseWeapon, Build.Horde(), models: 10));
    }

    [Fact]
    public void Lethal_hits_matches_the_analytic_mean()
    {
        // 6 : blessure auto (1/6) ; 3-5 : jet de blessure normal (3/6 × 1/2)
        var weapon = BaseWeapon with { Abilities = new WeaponAbilities { LethalHits = true } };
        AssertMeanKills(10 * (1.0 / 6 + 0.5 * 0.5), Build.Scenario(weapon, Build.Horde(), models: 10));
    }

    [Fact]
    public void Sustained_hits_matches_the_analytic_mean()
    {
        // touches par attaque = 2/3 + 1/6
        var weapon = BaseWeapon with { Abilities = new WeaponAbilities { SustainedHits = 1 } };
        AssertMeanKills(10 * (2.0 / 3 + 1.0 / 6) * 0.5, Build.Scenario(weapon, Build.Horde(), models: 10));
    }

    [Fact]
    public void Devastating_wounds_matches_the_analytic_mean_against_a_2_up_save()
    {
        // blessure critique (1/6) sans svg + blessure normale (2/6) × svg 2+ ratée (1/6)
        var weapon = BaseWeapon with { Abilities = new WeaponAbilities { DevastatingWounds = true } };
        AssertMeanKills(10 * (2.0 / 3) * (1.0 / 6 + 2.0 / 6 * 1.0 / 6), Build.Scenario(weapon, Build.Horde(save: 2), models: 10));
    }

    [Fact]
    public void Twin_linked_matches_the_analytic_mean()
    {
        // blessure avec relance des échecs : 1/2 + 1/2 × 1/2
        var weapon = BaseWeapon with { Abilities = new WeaponAbilities { TwinLinked = true } };
        AssertMeanKills(10 * (2.0 / 3) * 0.75, Build.Scenario(weapon, Build.Horde(), models: 10));
    }

    [Fact]
    public void Cover_matches_the_analytic_mean()
    {
        // CT 3+ à couvert = 4+
        AssertMeanKills(10 * 0.5 * 0.5, Build.Scenario(BaseWeapon, Build.Horde() with { InCover = true }, models: 10));
    }

    [Fact]
    public void Same_seed_gives_identical_results()
    {
        var scenario = Build.Scenario(BaseWeapon with { Damage = DiceExpression.Parse("D3") }, Build.Horde(), models: 10);

        var first = Simulator.Run(scenario, 50_000, seed: 7);
        var second = Simulator.Run(scenario, 50_000, seed: 7);

        Assert.Equal(first.KillsHistogram, second.KillsHistogram);
        Assert.Equal(first.DamageHistogram, second.DamageHistogram);
    }

    [Fact]
    public void Iteration_count_is_exact_even_when_not_a_multiple_of_the_chunk_size()
    {
        var summary = Simulator.Run(Build.Scenario(BaseWeapon, Build.Horde()), 25_001, seed: 1);
        Assert.Equal(25_001, summary.Iterations);
        Assert.Equal(25_001, summary.KillsHistogram.Sum());
    }

    [Fact]
    public void Invalid_scenario_is_rejected()
    {
        var scenario = Build.Scenario(BaseWeapon with { Skill = 9 }, Build.Horde());
        Assert.Throws<ArgumentException>(() => Simulator.Run(scenario, 10));
    }

    [Fact]
    public void Scenario_survives_a_json_round_trip()
    {
        var scenario = Build.Scenario(
            BaseWeapon with { Damage = DiceExpression.Parse("D6+1"), Abilities = new WeaponAbilities { Melta = 2, SustainedHits = DiceExpression.Parse("D3") } },
            Build.Horde() with { InCover = true },
            models: 3,
            context: new AttackContext { HalfRange = true, HitReroll = RerollPolicy.Ones });

        var copy = ScenarioJson.Deserialize(ScenarioJson.Serialize(scenario));

        Assert.Equal(ScenarioJson.Serialize(scenario), ScenarioJson.Serialize(copy));
        Assert.Equal(scenario.Attacker.Weapons[0].Weapon, copy.Attacker.Weapons[0].Weapon);
    }

    [Fact]
    public void Example_scenario_file_loads_and_runs()
    {
        string path = Path.Combine(FindRepositoryRoot(), "scenarios", "exemple-escouade-vs-leader.json");
        var scenario = ScenarioJson.Load(path);

        var summary = Simulator.Run(scenario, 10_000, seed: 3);

        Assert.Equal(11, summary.TotalModels);
        Assert.InRange(summary.MeanKills, 0.5, 11);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Wh40kSim.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Racine de la solution introuvable.");
    }
}
