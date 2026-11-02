using System.Diagnostics;

namespace Wh40kSim.Simulation.Comparisons;

/// <summary>Lance une comparaison : les attaquants s'affrontent en rondes éliminatoires contre le défenseur commun.</summary>
public static class Comparator
{
    /// <summary>
    /// Rondes éliminatoires selon le nombre d'attaquants (<see cref="RoundPlan.For"/>, <see cref="EliminationRounds"/>) :
    /// la finale fait <paramref name="iterations"/> simulations, avec la même graine (tirée une fois si absente) pour
    /// toutes les entrées et toutes les rondes ; chaque finaliste est identique à une simulation seule de son scénario à
    /// graine égale. Chaque attaquant est simulé à son tour, sur tous les cœurs.
    /// </summary>
    /// <param name="progress">Ronde en cours et son avancement ; appelée depuis les threads de calcul.</param>
    /// <exception cref="ArgumentException">Si la comparaison est invalide.</exception>
    /// <exception cref="OperationCanceledException">Si le calcul est annulé (aucun résultat partiel).</exception>
    public static ComparisonResult Run(
        Comparison comparison,
        int iterations,
        int? seed = null,
        IProgress<RoundProgress>? progress = null,
        CancellationToken cancellationToken = default,
        SimulationThreads? threads = null)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        comparison.EnsureValid();

        var stopwatch = Stopwatch.StartNew();
        // Les variantes (combinaisons de profils) s'affrontent : le plan des rondes les compte toutes
        var scenarios = comparison.ToScenarios();
        var plan = RoundPlan.For(scenarios.Count, iterations);
        var outcomes = EliminationRounds.Run(scenarios, plan, seed ?? Random.Shared.Next(), progress, cancellationToken, threads);
        stopwatch.Stop();
        return new ComparisonResult(new ComparisonData
        {
            Comparison = comparison,
            Rounds = plan,
            Elapsed = stopwatch.Elapsed,
            Entries = outcomes.Select(o => new ComparisonEntryData { Simulation = o.Result.Data, EliminatedInRound = o.EliminatedInRound }).ToList(),
        });
    }
}
