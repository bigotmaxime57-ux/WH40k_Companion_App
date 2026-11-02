using Microsoft.Extensions.Options;
using Wh40kSim.Core.Model;
using Wh40kSim.Simulation;
using Wh40kSim.Simulation.Comparisons;

namespace Wh40kSim.Web;

/// <summary>Mène un calcul ; derrière une interface pour que les tests contrôlent l'attente, l'annulation et les erreurs.</summary>
public interface ISimulationRunner
{
    Task<SimulationResult> RunAsync(Scenario scenario, int iterations, int? seed, IProgress<double> progress, CancellationToken cancellationToken);

    /// <param name="maxFinalists">Plafond de finalistes (voir <see cref="EliminationRounds.Run"/>).</param>
    Task<ComparisonResult> RunComparisonAsync(
        Comparison comparison, int iterations, int? seed, int maxFinalists, IProgress<RoundProgress> progress, CancellationToken cancellationToken);
}

/// <summary>
/// Calcul réel : <see cref="Simulator.Run"/> ou <see cref="Comparator.Run"/> hors du thread de la requête, threads limités
/// par la configuration.
/// </summary>
public sealed class SimulatorRunner(IOptions<SimulationOptions> options) : ISimulationRunner
{
    public Task<SimulationResult> RunAsync(Scenario scenario, int iterations, int? seed, IProgress<double> progress, CancellationToken cancellationToken)
    {
        var threads = new SimulationThreads(options.Value.ThreadsPerSimulation);
        return Task.Run(() => Simulator.Run(scenario, iterations, seed, progress, cancellationToken, threads), cancellationToken);
    }

    public Task<ComparisonResult> RunComparisonAsync(
        Comparison comparison, int iterations, int? seed, int maxFinalists, IProgress<RoundProgress> progress, CancellationToken cancellationToken)
    {
        var threads = new SimulationThreads(options.Value.ThreadsPerSimulation);
        return Task.Run(() => Comparator.Run(comparison, iterations, seed, progress, cancellationToken, threads, maxFinalists), cancellationToken);
    }
}
