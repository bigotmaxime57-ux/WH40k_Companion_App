namespace Wh40kSim.Simulation.Comparisons;

/// <summary>Lance une comparaison : chaque attaquant est simulé à son tour contre le défenseur commun.</summary>
public static class Comparator
{
    /// <summary>
    /// Simule les attaquants l'un après l'autre (<see cref="Simulator.Run"/>, tous les cœurs pour chacun), avec le même
    /// nombre de simulations et la même graine (tirée une fois si absente) : chaque entrée est identique à une
    /// simulation seule de son scénario à graine égale.
    /// </summary>
    /// <param name="progress">
    /// Progression de l'ensemble (entre 0 et 1), strictement croissante, la dernière valeur vaut 1 ; appelée depuis les
    /// threads de calcul.
    /// </param>
    /// <exception cref="ArgumentException">Si la comparaison est invalide.</exception>
    /// <exception cref="OperationCanceledException">Si le calcul est annulé (aucun résultat partiel).</exception>
    public static ComparisonResult Run(
        Comparison comparison,
        int iterations,
        int? seed = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        SimulationThreads? threads = null)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        comparison.EnsureValid();

        int baseSeed = seed ?? Random.Shared.Next();
        var scenarios = comparison.ToScenarios();
        var entries = new List<SimulationData>(scenarios.Count);
        for (int i = 0; i < scenarios.Count; i++)
        {
            var entryProgress = progress is null ? null : new EntryProgress(progress, i, scenarios.Count);
            entries.Add(Simulator.Run(scenarios[i], iterations, baseSeed, entryProgress, cancellationToken, threads).Data);
        }
        return new ComparisonResult(new ComparisonData { Entries = entries });
    }

    /// <summary>Ramène la progression d'une entrée (0 à 1) à sa part de l'ensemble.</summary>
    private sealed class EntryProgress(IProgress<double> total, int index, int count) : IProgress<double>
    {
        public void Report(double value) => total.Report((index + value) / count);
    }
}
