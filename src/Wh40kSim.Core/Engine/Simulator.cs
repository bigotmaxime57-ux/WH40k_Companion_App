using System.Diagnostics;
using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Engine;

/// <summary>
/// Lance N simulations en parallèle (Monte Carlo).
/// <para>Les simulations sont découpées en paquets de <see cref="ChunkSize"/>, chacun avec sa propre graine
/// dérivée de la graine principale : à graine égale, le résultat est identique quel que soit le nombre de cœurs.</para>
/// </summary>
public static class Simulator
{
    public const int ChunkSize = 10_000;

    /// <param name="progress">
    /// Reçoit la progression (entre 0 et 1) à la fin de chaque paquet : valeurs strictement croissantes, la dernière vaut 1.
    /// Appelé depuis les threads de calcul ; elle n'influe pas sur les dés, le résultat reste identique à graine égale.
    /// </param>
    public static SimulationSummary Run(
        Scenario scenario,
        int iterations,
        int? seed = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        scenario.EnsureValid();

        int baseSeed = seed ?? Random.Shared.Next();
        var total = new SimulationSummary(scenario, baseSeed);
        var mergeLock = new object();
        var progressLock = new object();
        int completedChunks = 0;
        var stopwatch = Stopwatch.StartNew();
        int chunkCount = (int)(((long)iterations + ChunkSize - 1) / ChunkSize);

        Parallel.For(
            0,
            chunkCount,
            new ParallelOptions { CancellationToken = cancellationToken },
            () => new Worker(scenario, baseSeed),
            (chunk, _, worker) =>
            {
                int count = Math.Min(ChunkSize, iterations - chunk * ChunkSize);
                var dice = new RandomDiceRoller(DeriveSeed(baseSeed, chunk));
                for (int i = 0; i < count; i++)
                {
                    worker.Resolver.Run(dice);
                    worker.Summary.Record(worker.Resolver.Target);
                }

                if (progress is not null)
                {
                    // Sous verrou : les paquets finissent dans le désordre, les valeurs rapportées doivent rester croissantes
                    lock (progressLock)
                    {
                        completedChunks++;
                        progress.Report((double)completedChunks / chunkCount);
                    }
                }
                return worker;
            },
            worker =>
            {
                lock (mergeLock)
                {
                    total.Merge(worker.Summary);
                }
            });

        stopwatch.Stop();
        total.Elapsed = stopwatch.Elapsed;
        return total;
    }

    /// <summary>Graine indépendante par paquet (mélange SplitMix64).</summary>
    private static int DeriveSeed(int baseSeed, int chunk)
    {
        unchecked
        {
            ulong z = (ulong)(uint)baseSeed * 0x9E3779B97F4A7C15UL + (ulong)chunk * 0xBF58476D1CE4E5B9UL + 0x94D049BB133111EBUL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (int)(z & 0x7FFFFFFF);
        }
    }

    private sealed class Worker(Scenario scenario, int seed)
    {
        public AttackResolver Resolver { get; } = new(scenario);

        public SimulationSummary Summary { get; } = new(scenario, seed);
    }
}
