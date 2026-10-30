using Microsoft.Extensions.Options;
using Wh40kSim.Core.Model;
using Wh40kSim.Simulation;

namespace Wh40kSim.Web;

/// <summary>Mène un calcul ; derrière une interface pour que les tests contrôlent l'attente, l'annulation et les erreurs.</summary>
public interface ISimulationRunner
{
    Task<SimulationResult> RunAsync(Scenario scenario, int iterations, int? seed, IProgress<double> progress, CancellationToken cancellationToken);
}

/// <summary>Calcul réel : <see cref="Simulator.Run"/> hors du thread de la requête, threads limités par la configuration.</summary>
public sealed class SimulatorRunner(IOptions<SimulationOptions> options) : ISimulationRunner
{
    public Task<SimulationResult> RunAsync(Scenario scenario, int iterations, int? seed, IProgress<double> progress, CancellationToken cancellationToken)
    {
        var threads = new SimulationThreads(options.Value.ThreadsPerSimulation);
        return Task.Run(() => Simulator.Run(scenario, iterations, seed, progress, cancellationToken, threads), cancellationToken);
    }
}
