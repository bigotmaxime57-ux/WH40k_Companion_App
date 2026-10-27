using Wh40kSim.Core.Model;
using Wh40kSim.Simulation.Statistics;

namespace Wh40kSim.Simulation;

/// <summary>
/// Résultat de N simulations : distributions complètes des figurines tuées et des PV retirés, pertes par groupe.
/// Tout ce qui s'affiche se lit ici, sans calcul côté console ou interface. Immuable.
/// </summary>
public sealed class SimulationResult
{
    private readonly long[] _killedPerGroup; // total des figurines tuées par groupe, toutes simulations confondues

    internal SimulationResult(Scenario scenario, int seed, TimeSpan elapsed, DiscreteDistribution kills, DiscreteDistribution damage, long[] killedPerGroup)
    {
        Scenario = scenario;
        Seed = seed;
        Elapsed = elapsed;
        Kills = kills;
        Damage = damage;
        _killedPerGroup = killedPerGroup;
    }

    public Scenario Scenario { get; }

    /// <summary>Graine utilisée (tirée au hasard si aucune n'a été fournie) : la redonner reproduit ce résultat.</summary>
    public int Seed { get; }

    public long Iterations => Kills.Total;

    public TimeSpan Elapsed { get; }

    /// <summary>Distribution du nombre de figurines tuées (0 à <see cref="TotalModels"/>).</summary>
    public DiscreteDistribution Kills { get; }

    /// <summary>Distribution des PV effectivement retirés (0 à <see cref="TotalWounds"/>).</summary>
    public DiscreteDistribution Damage { get; }

    public int TotalModels => Kills.MaxValue;

    public int TotalWounds => Damage.MaxValue;

    /// <summary>Probabilité de détruire toute l'unité.</summary>
    public double ProbabilityWipe => Kills.ProbabilityAtLeast(TotalModels);

    /// <summary>Nombre moyen de figurines tuées dans le groupe <paramref name="group"/> (ordre d'allocation du défenseur).</summary>
    public double MeanKilledInGroup(int group) => (double)_killedPerGroup[group] / Iterations;
}
