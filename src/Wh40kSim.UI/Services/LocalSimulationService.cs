using System.Runtime.Versioning;
using Wh40kSim.Core.Model;
using Wh40kSim.Simulation;

namespace Wh40kSim.UI.Services;

/// <summary>
/// Exécute la simulation sur la machine, hors du thread de l'interface. Pour que l'interface et Windows restent
/// réactifs pendant un long calcul (ticket #12), il utilise tous les cœurs sauf un, sur des threads en priorité basse ;
/// le calcul est un peu plus lent, le résultat identique à graine égale.
/// </summary>
public sealed class LocalSimulationService : ISimulationService
{
    /// <summary>Threads de calcul : un cœur laissé libre (au moins un thread).</summary>
    private static readonly int Workers = Math.Max(1, Environment.ProcessorCount - 1);

    private static readonly object SchedulerLock = new();
    private static TaskScheduler? _scheduler;

    public Task<SimulationResult> RunAsync(
        Scenario scenario,
        int iterations,
        int? seed = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        IProgress<int>? queuePosition = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            // Pas de threads dans le navigateur (le client web passera par l'API, phase 4)
            return Task.Run(() => Simulator.Run(scenario, iterations, seed, progress, cancellationToken), cancellationToken);
        }

        var scheduler = LowPriorityScheduler();
        // Lancé lui-même sur ces threads : le thread qui attend la fin du calcul y participe, en priorité basse lui aussi
        return Task.Factory.StartNew(
            () => Simulator.Run(scenario, iterations, seed, progress, cancellationToken, new SimulationThreads(Workers, scheduler)),
            cancellationToken, TaskCreationOptions.None, scheduler);
    }

    /// <summary>Ordonnanceur partagé par toutes les simulations, créé à la première.</summary>
    [UnsupportedOSPlatform("browser")]
    private static TaskScheduler LowPriorityScheduler()
    {
        lock (SchedulerLock)
        {
            return _scheduler ??= new LowPriorityTaskScheduler(Workers);
        }
    }
}
