using Wh40kSim.Core.Model;
using Wh40kSim.Simulation.Statistics;

namespace Wh40kSim.Simulation.Tests;

/// <summary>
/// Score d'efficacité : 100 × (0,4 × part des PV + 0,3 × part des figurines + 0,3 × unité détruite) par simulation,
/// moyenne et erreur type exactes, tirées des histogrammes et du produit figurines × PV cumulé.
/// </summary>
public class EfficiencyScoreTests
{
    [Fact]
    public void Mean_and_standard_error_are_exact()
    {
        // Simulations connues (figurines tuées, PV retirés) contre 3 figurines de 2 PV : l'unité est détruite à la 4e
        (int Kills, int Damage)[] simulations = [(0, 0), (1, 2), (1, 3), (3, 6), (2, 5)];
        const int models = 3, wounds = 6;
        var kills = new long[models + 1];
        var damage = new long[wounds + 1];
        foreach (var (k, d) in simulations)
        {
            kills[k]++;
            damage[d]++;
        }

        var score = EfficiencyScore.Compute(
            new DiscreteDistribution(kills), new DiscreteDistribution(damage), simulations.Sum(s => (long)s.Kills * s.Damage));

        var each = simulations.Select(s => 100 * (0.4 * s.Damage / wounds + 0.3 * s.Kills / models + 0.3 * (s.Kills == models ? 1 : 0))).ToList();
        double mean = each.Average();
        double standardError = Math.Sqrt(each.Sum(v => (v - mean) * (v - mean)) / each.Count / each.Count);
        Assert.Equal(mean, score.Mean, 1e-10);
        Assert.Equal(standardError, score.StandardError, 1e-10);
        Assert.Equal(score.Mean, score.DamagePart + score.KillsPart + score.WipePart, 1e-10);
        Assert.Equal(100 * 0.3 * 1 / 5.0, score.WipePart, 1e-10);
    }

    [Fact]
    public void A_unit_always_destroyed_scores_100_without_noise()
    {
        var score = EfficiencyScore.Compute(new DiscreteDistribution([0, 0, 10]), new DiscreteDistribution([0, 0, 0, 0, 10]), 10 * 2 * 4);

        Assert.Equal(100, score.Mean, 1e-10);
        Assert.Equal(0, score.StandardError, 1e-10);
    }

    [Fact]
    public void The_simulator_counts_kills_times_damage()
    {
        // Figurines à 1 PV : PV retirés = figurines tuées, donc le produit cumulé vaut la somme des carrés
        var result = Simulator.Run(Build.Scenario(new WeaponProfile { Attacks = 6, Skill = 3, Strength = 4 }, Build.Horde(models: 10)), 50_000, seed: 3);

        long squares = result.Kills.Counts.Select((count, k) => count * k * k).Sum();
        Assert.Equal(squares, result.Data.KillsTimesDamage);
    }

    [Fact]
    public void The_score_combines_the_three_indicators_of_a_simulation()
    {
        var result = Simulator.Run(NonRegressionTests.RichScenario(), 100_000, seed: 5);

        double expected = 40 * result.MeanDamageShare + 30 * result.Kills.Mean / result.TotalModels + 30 * result.ProbabilityWipe;
        Assert.Equal(expected, result.EfficiencyScore.Mean, 1e-9);
    }

    [Fact]
    public void The_standard_error_matches_the_spread_between_seeds()
    {
        // 30 graines : l'écart-type des scores moyens doit valoir l'erreur type annoncée (rapport entre 0,6 et 1,5,
        // soit environ 3 écarts-types de l'estimation de chaque côté ; graines fixes, donc test déterministe)
        var scenario = Build.Scenario(new WeaponProfile { Attacks = 10, Skill = 3, Strength = 4, Damage = 2 },
            new DefenderUnit { Groups = [new ModelGroup { Name = "Gardes", Count = 5, Wounds = 2, Toughness = 4, Save = 4 }] });
        var results = Enumerable.Range(1, 30).Select(seed => Simulator.Run(scenario, 20_000, seed: seed)).ToList();

        var means = results.Select(r => r.EfficiencyScore.Mean).ToList();
        double average = means.Average();
        double spread = Math.Sqrt(means.Sum(m => (m - average) * (m - average)) / (means.Count - 1));
        double announced = results.Average(r => r.EfficiencyScore.StandardError);
        Assert.InRange(spread / announced, 0.6, 1.5);
    }
}
