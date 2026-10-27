using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Simulation.Tests;

/// <summary>
/// Le Monte Carlo doit retrouver des probabilités calculées exactement (loi binomiale), à quelques écarts-types près.
/// Base : 20 attaques CT 3+, F4 contre E4 (1/2), aucune sauvegarde, D1, 8 figurines à 1 PV : le nombre de figurines
/// tuées suit une loi binomiale B(20, p) plafonnée à 8.
/// <para>Contrôle du générateur de dés (ticket #10) : l'ancien <c>new Random(graine)</c> s'écartait de ces valeurs
/// d'environ 0,1 point, ce qu'un test rapide ne peut pas détecter sûrement ; la vérification complète (80 millions de
/// simulations par cas, comparaison à un générateur indépendant) a été faite à la main. Ces tests gardent contre un
/// défaut grossier du tirage ou de la séquence.</para>
/// </summary>
public class ExactProbabilityTests
{
    private const int Iterations = 1_000_000;
    private const double Sigmas = 4; // graine fixe : résultat déterministe, 4 écarts-types laissent une marge confortable
    private const int Attacks = 20;
    private const int Models = 8;

    [Theory]
    [InlineData(RerollPolicy.None, 4.0 / 6)]
    [InlineData(RerollPolicy.Ones, 4.0 / 6 + 1.0 / 6 * 4.0 / 6)]
    [InlineData(RerollPolicy.Failed, 4.0 / 6 + 2.0 / 6 * 4.0 / 6)]
    [InlineData(RerollPolicy.NonCritical, 1.0 / 6 + 5.0 / 6 * 4.0 / 6)]
    public void Wipe_probability_matches_the_binomial_law(RerollPolicy hitReroll, double hitProbability)
    {
        AssertMatches(Rerolling(new AttackerBoosts { HitReroll = hitReroll }), hitProbability * 0.5);
    }

    [Fact]
    public void Rerolling_everything_but_criticals_on_both_rolls_matches_the_binomial_law()
    {
        // Touche : 6 du premier jet, sinon relance réussie sur 3+ ; blessure : 6 du premier jet, sinon relance sur 4+
        var boosts = new AttackerBoosts { HitReroll = RerollPolicy.NonCritical, WoundReroll = RerollPolicy.NonCritical };

        AssertMatches(Rerolling(boosts), (1.0 / 6 + 5.0 / 6 * 4.0 / 6) * (1.0 / 6 + 5.0 / 6 * 0.5));
    }

    [Fact]
    public void D3_damage_matches_the_exact_probability()
    {
        // Une touche automatique, blessure sur 4+, D3 dégâts contre une figurine à 2 PV : détruite = 1/2 × 2/3
        var weapon = new WeaponProfile { Skill = 3, Strength = 4, Damage = DiceExpression.Parse("D3"), Abilities = new WeaponAbilities { Torrent = true } };
        var defender = new DefenderUnit { Groups = [new ModelGroup { Name = "Cible", Count = 1, Toughness = 4, Wounds = 2, Save = 7 }] };

        var result = Simulator.Run(Build.Scenario(weapon, defender), Iterations, seed: 3);

        AssertClose(1.0 / 3, result.ProbabilityWipe);
    }

    /// <summary>Scénario de base (20 attaques contre 8 figurines) avec les relances données.</summary>
    private static Scenario Rerolling(AttackerBoosts boosts)
    {
        var scenario = Build.Scenario(new WeaponProfile { Skill = 3, Strength = 4 }, Build.Horde(models: Models), models: Attacks);
        return scenario with { Attacker = scenario.Attacker with { Boosts = boosts } };
    }

    private static void AssertMatches(Scenario scenario, double successProbability)
    {
        var result = Simulator.Run(scenario, Iterations, seed: 2026);

        AssertClose(BinomialAtLeast(Attacks, successProbability, Models), result.ProbabilityWipe);
    }

    private static void AssertClose(double expected, double actual)
    {
        double tolerance = Sigmas * Math.Sqrt(expected * (1 - expected) / Iterations);
        Assert.InRange(actual, expected - tolerance, expected + tolerance);
    }

    /// <summary>P(X ≥ k) pour X de loi binomiale B(n, p).</summary>
    private static double BinomialAtLeast(int n, double p, int k)
    {
        double sum = 0;
        double choose = 1; // C(n, i), mis à jour à chaque pas
        for (int i = 0; i <= n; i++)
        {
            if (i >= k) sum += choose * Math.Pow(p, i) * Math.Pow(1 - p, n - i);
            choose = choose * (n - i) / (i + 1);
        }
        return sum;
    }
}
