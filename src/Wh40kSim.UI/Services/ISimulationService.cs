using Wh40kSim.Core.Model;
using Wh40kSim.Simulation;

namespace Wh40kSim.UI.Services;

/// <summary>
/// Lance une simulation sans bloquer l'interface. L'implémentation dépend de l'hôte :
/// calcul local (desktop) ou appel à une API (client web, phase 4).
/// </summary>
public interface ISimulationService
{
    /// <param name="progress">Progression entre 0 et 1 (voir <see cref="Simulator.Run"/>).</param>
    /// <exception cref="OperationCanceledException">Si <paramref name="cancellationToken"/> est annulé.</exception>
    Task<SimulationResult> RunAsync(
        Scenario scenario,
        int iterations,
        int? seed = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
