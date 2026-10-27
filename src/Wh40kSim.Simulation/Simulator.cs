using System.Diagnostics;
using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Engine;
using Wh40kSim.Core.Model;
using Wh40kSim.Simulation.Statistics;

namespace Wh40kSim.Simulation;

/// <summary>
/// Lance N simulations en parallèle (Monte Carlo) : chacune est une séquence d'attaque résolue par le Core.
/// <para>Les simulations sont découpées en paquets de <see cref="ChunkSize"/>, chacun avec sa propre graine
/// dérivée de la graine principale : à graine égale, le résultat est identique quel que soit le nombre de cœurs.</para>
/// <para>Les résultats sont agrégés en histogrammes au fil de l'eau (un par thread, fusionnés à la fin) :
/// aucun résultat individuel n'est conservé.</para>
/// <para>L'excédent (attaques après la destruction de l'unité) est tiré sur un second flux de dés par paquet :
/// les résultats réels sont les mêmes que sans excédent.</para>
/// </summary>
public static class Simulator
{
    public const int ChunkSize = 10_000;

    /// <param name="progress">
    /// Reçoit la progression (entre 0 et 1) à la fin de chaque paquet : valeurs strictement croissantes, la dernière vaut 1.
    /// Appelé depuis les threads de calcul ; elle n'influe pas sur les dés, le résultat reste identique à graine égale.
    /// </param>
    public static SimulationResult Run(
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
        var total = new Accumulator(scenario.Defender);
        var mergeLock = new object();
        var progressLock = new object();
        int completedChunks = 0;
        var stopwatch = Stopwatch.StartNew();
        int chunkCount = (int)(((long)iterations + ChunkSize - 1) / ChunkSize);

        Parallel.For(
            0,
            chunkCount,
            new ParallelOptions { CancellationToken = cancellationToken },
            () => new Worker(scenario),
            (chunk, _, worker) =>
            {
                int count = Math.Min(ChunkSize, iterations - chunk * ChunkSize);
                var dice = new RandomDiceRoller(DeriveSeed(baseSeed, chunk, MainStream));
                var excessDice = new RandomDiceRoller(DeriveSeed(baseSeed, chunk, ExcessStream));
                for (int i = 0; i < count; i++)
                {
                    worker.Resolver.Run(dice, excessDice);
                    worker.Accumulator.Record(worker.Resolver.Target);
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
                    total.Merge(worker.Accumulator);
                }
            });

        stopwatch.Stop();
        return total.ToResult(scenario, baseSeed, stopwatch.Elapsed);
    }

    private const int MainStream = 0;
    private const int ExcessStream = 1;

    /// <summary>
    /// Graine indépendante par paquet et par flux de dés (mélange SplitMix64), sur 64 bits : deux paquets n'ont en
    /// pratique jamais la même graine.
    /// </summary>
    private static ulong DeriveSeed(int baseSeed, int chunk, int stream)
    {
        unchecked
        {
            ulong z = (ulong)(uint)baseSeed * 0x9E3779B97F4A7C15UL + (ulong)chunk * 0xBF58476D1CE4E5B9UL
                      + (ulong)stream * 0xD6E8FEB86659FD93UL + 0x94D049BB133111EBUL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>Un resolver et un accumulateur par thread : rien n'est alloué à chaque simulation.</summary>
    private sealed class Worker(Scenario scenario)
    {
        public AttackResolver Resolver { get; } = new(scenario);

        public Accumulator Accumulator { get; } = new(scenario.Defender);
    }

    /// <summary>Histogrammes en cours de remplissage, convertis en <see cref="SimulationResult"/> à la fin.</summary>
    private sealed class Accumulator(DefenderUnit defender)
    {
        private readonly long[] _kills = new long[defender.TotalModels + 1];        // _kills[k] = simulations avec exactement k figurines tuées
        private readonly long[] _damage = new long[defender.TotalWounds + 1];       // _damage[d] = simulations avec exactement d PV retirés
        private readonly long[] _killedPerGroup = new long[defender.Groups.Count];  // figurines tuées par groupe, cumulées
        private readonly long[] _damagePerGroup = new long[defender.Groups.Count];  // PV retirés par groupe, cumulés

        // Distributions prolongées (réel + excédent) : pas de maximum connu à l'avance, les tableaux s'agrandissent
        private long[] _extendedKills = new long[defender.TotalModels + 1];
        private long[] _extendedDamage = new long[defender.TotalWounds + 1];

        public void Record(DefenderState state)
        {
            _kills[state.ModelsKilled]++;
            _damage[state.DamageDealt]++;
            for (int g = 0; g < _killedPerGroup.Length; g++)
            {
                _killedPerGroup[g] += state.KilledInGroup(g);
                _damagePerGroup[g] += state.DamageInGroup(g);
            }

            Increment(ref _extendedKills, state.ModelsKilled + state.ExcessModelsKilled);
            Increment(ref _extendedDamage, state.DamageDealt + state.ExcessDamage);
        }

        public void Merge(Accumulator other)
        {
            for (int i = 0; i < _kills.Length; i++) _kills[i] += other._kills[i];
            for (int i = 0; i < _damage.Length; i++) _damage[i] += other._damage[i];
            for (int i = 0; i < _killedPerGroup.Length; i++) _killedPerGroup[i] += other._killedPerGroup[i];
            for (int i = 0; i < _damagePerGroup.Length; i++) _damagePerGroup[i] += other._damagePerGroup[i];
            Add(ref _extendedKills, other._extendedKills);
            Add(ref _extendedDamage, other._extendedDamage);
        }

        public SimulationResult ToResult(Scenario scenario, int seed, TimeSpan elapsed) =>
            new(scenario, seed, elapsed,
                new DiscreteDistribution(_kills), new DiscreteDistribution(_damage), _killedPerGroup, _damagePerGroup,
                new DiscreteDistribution(TrimTrailingZeros(_extendedKills, _kills.Length)),
                new DiscreteDistribution(TrimTrailingZeros(_extendedDamage, _damage.Length)));

        private static void Increment(ref long[] histogram, int value)
        {
            if (value >= histogram.Length)
            {
                Array.Resize(ref histogram, Math.Max(value + 1, histogram.Length * 2));
            }
            histogram[value]++;
        }

        private static void Add(ref long[] histogram, long[] other)
        {
            if (other.Length > histogram.Length)
            {
                Array.Resize(ref histogram, other.Length);
            }
            for (int i = 0; i < other.Length; i++) histogram[i] += other[i];
        }

        /// <summary>Retire les zéros de fin ajoutés par l'agrandissement, sans descendre sous la taille réelle.</summary>
        private static long[] TrimTrailingZeros(long[] histogram, int minLength)
        {
            int length = histogram.Length;
            while (length > minLength && histogram[length - 1] == 0) length--;
            return histogram[..length];
        }
    }
}
