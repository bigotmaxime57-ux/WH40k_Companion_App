using Wh40kSim.Core.Model;
using Wh40kSim.Simulation;

namespace Wh40kSim.UI.Services;

/// <summary>
/// Exécute la simulation sur la machine, hors du thread de l'interface (Task.Run) :
/// l'interface reste réactive pendant le calcul, qui utilise tous les cœurs.
/// </summary>
public sealed class LocalSimulationService : ISimulationService
{
    public Task<SimulationResult> RunAsync(
        Scenario scenario,
        int iterations,
        int? seed = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Simulator.Run(scenario, iterations, seed, progress, cancellationToken), cancellationToken);
}
