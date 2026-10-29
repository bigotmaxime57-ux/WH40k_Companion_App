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
        var total = new Accumulator(scenario.Defender, scenario.UsesPrecision, AttackPool.Build(scenario.Attacker.Weapons).Count);
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

        public Accumulator Accumulator { get; } = new(scenario.Defender, scenario.UsesPrecision, AttackPool.Build(scenario.Attacker.Weapons).Count);
    }

    /// <summary>
    /// Histogrammes en cours de remplissage, convertis en <see cref="SimulationResult"/> à la fin. Travail des armes
    /// Precision par groupe suivi seulement si l'attaquant en a (<paramref name="tracksPrecisionWeapons"/>).
    /// </summary>
    private sealed class Accumulator(DefenderUnit defender, bool tracksPrecisionWeapons, int poolCount)
    {
        private readonly long[] _kills = new long[defender.TotalModels + 1];        // _kills[k] = simulations avec exactement k figurines tuées
        private readonly long[] _damage = new long[defender.TotalWounds + 1];       // _damage[d] = simulations avec exactement d PV retirés
        private readonly long[] _killedPerGroup = new long[defender.Groups.Count];  // figurines tuées par groupe, cumulées
        private readonly long[] _damagePerGroup = new long[defender.Groups.Count];  // PV retirés par groupe, cumulés

        // Distributions par groupe (_groupKills[g][k] = simulations avec exactement k figurines tuées dans le groupe g),
        // figurines tuées et PV retirés aux gardes du corps (groupes non-PERSONNAGES ensemble) et pertes « grâce à
        // Precision », cumulées
        private readonly long[][] _groupKills = defender.Groups.Select(g => new long[g.Count + 1]).ToArray();
        private readonly long[][] _groupDamage = defender.Groups.Select(g => new long[g.Count * g.Wounds + 1]).ToArray();
        private readonly bool[] _isBodyguard = defender.Groups.Select(g => !g.IsCharacter).ToArray();
        private readonly long[] _bodyguardKills = new long[defender.Groups.Where(g => !g.IsCharacter).Sum(g => g.Count) + 1];
        private readonly long[] _bodyguardDamage = new long[defender.Groups.Where(g => !g.IsCharacter).Sum(g => g.Count * g.Wounds) + 1];
        private readonly long[] _precisionKilled = new long[defender.Groups.Count];
        private readonly long[] _precisionDamage = new long[defender.Groups.Count];

        // Armes Precision, par groupe PERSONNAGE : PV retirés (cumulés, et par total de PV du groupe pour l'histogramme empilé),
        // simulations où le groupe est tué par elles seules / avec l'aide d'autres armes / alors que des gardes vivent,
        // simulations où elles ne le tuent pas seules et PV qu'elles lui laissent alors (cumulés)
        private readonly long[] _precisionWeaponDamage = new long[defender.Groups.Count];
        private readonly long[][] _precisionWeaponDamageByTotal = defender.Groups.Select(g => new long[g.Count * g.Wounds + 1]).ToArray();
        private readonly long[] _killedByPrecisionAlone = new long[defender.Groups.Count];
        private readonly long[] _killedWithHelp = new long[defender.Groups.Count];
        private readonly long[] _killedWhileBodyguardsAlive = new long[defender.Groups.Count];
        private readonly long[] _precisionFallsShort = new long[defender.Groups.Count];
        private readonly long[] _woundsLeftWhenShort = new long[defender.Groups.Count];

        // Par pool d'attaques (ordre de résolution) : distributions des PV retirés et des figurines achevées, dégâts bruts
        // (séquence réelle) cumulés, simulations où l'unité était déjà détruite quand le pool a tiré
        private readonly long[][] _poolDamage = Enumerable.Range(0, poolCount).Select(_ => new long[defender.TotalWounds + 1]).ToArray();
        private readonly long[][] _poolKills = Enumerable.Range(0, poolCount).Select(_ => new long[defender.TotalModels + 1]).ToArray();
        private readonly long[] _poolRawDamage = new long[poolCount];
        private readonly long[] _poolOnDestroyedUnit = new long[poolCount];

        // Distributions prolongées (réel + excédent) : pas de maximum connu à l'avance, les tableaux s'agrandissent
        private long[] _extendedKills = new long[defender.TotalModels + 1];
        private long[] _extendedDamage = new long[defender.TotalWounds + 1];
        private long[] _rawDamage = new long[defender.TotalWounds + 1];

        public void Record(DefenderState state)
        {
            _kills[state.ModelsKilled]++;
            _damage[state.DamageDealt]++;
            int bodyguardsKilled = 0;
            int bodyguardDamage = 0;
            bool bodyguardsAlive = false;
            for (int g = 0; g < _killedPerGroup.Length; g++)
            {
                int killed = state.KilledInGroup(g);
                int damage = state.DamageInGroup(g);
                _killedPerGroup[g] += killed;
                _damagePerGroup[g] += damage;
                _groupKills[g][killed]++;
                _groupDamage[g][damage]++;
                _precisionKilled[g] += state.PrecisionKilledInGroup(g);
                _precisionDamage[g] += state.PrecisionDamageInGroup(g);
                if (_isBodyguard[g])
                {
                    bodyguardsKilled += killed;
                    bodyguardDamage += damage;
                    bodyguardsAlive |= killed < defender.Groups[g].Count;
                }
                else if (tracksPrecisionWeapons)
                {
                    // Gardes du corps toujours avant les PERSONNAGES (05.03) : bodyguardsAlive est déjà complet ici
                    RecordPrecisionWeapons(state, g, killed, damage, bodyguardsAlive);
                }
            }
            _bodyguardKills[bodyguardsKilled]++;
            _bodyguardDamage[bodyguardDamage]++;
            for (int p = 0; p < _poolKills.Length; p++)
            {
                _poolDamage[p][state.DamageByPool(p)]++;
                _poolKills[p][state.KillsByPool(p)]++;
                _poolRawDamage[p] += state.RawDamageByPool(p);
                if (state.PoolFiredOnDestroyedUnit(p))
                {
                    _poolOnDestroyedUnit[p]++;
                }
            }

            Increment(ref _extendedKills, state.ModelsKilled + state.ExcessModelsKilled);
            Increment(ref _extendedDamage, state.DamageDealt + state.ExcessDamage);
            Increment(ref _rawDamage, state.RawDamage);
        }

        /// <summary>Travail des armes Precision sur un groupe PERSONNAGE (les seuls que Precision vise).</summary>
        private void RecordPrecisionWeapons(DefenderState state, int g, int killed, int damage, bool bodyguardsAlive)
        {
            var group = defender.Groups[g];
            int precision = state.PrecisionWeaponDamageInGroup(g);
            bool wiped = killed == group.Count;
            _precisionWeaponDamage[g] += precision;
            _precisionWeaponDamageByTotal[g][damage] += precision;
            if (wiped && precision == damage)
            {
                _killedByPrecisionAlone[g]++;
            }
            else
            {
                if (wiped)
                {
                    _killedWithHelp[g]++;
                }
                _precisionFallsShort[g]++;
                _woundsLeftWhenShort[g] += group.Count * group.Wounds - precision;
            }
            if (wiped && bodyguardsAlive)
            {
                _killedWhileBodyguardsAlive[g]++;
            }
        }

        public void Merge(Accumulator other)
        {
            for (int i = 0; i < _kills.Length; i++) _kills[i] += other._kills[i];
            for (int i = 0; i < _damage.Length; i++) _damage[i] += other._damage[i];
            for (int i = 0; i < _killedPerGroup.Length; i++) _killedPerGroup[i] += other._killedPerGroup[i];
            for (int i = 0; i < _damagePerGroup.Length; i++) _damagePerGroup[i] += other._damagePerGroup[i];
            for (int g = 0; g < _groupKills.Length; g++)
            {
                for (int i = 0; i < _groupKills[g].Length; i++) _groupKills[g][i] += other._groupKills[g][i];
                for (int i = 0; i < _groupDamage[g].Length; i++) _groupDamage[g][i] += other._groupDamage[g][i];
            }
            for (int i = 0; i < _bodyguardKills.Length; i++) _bodyguardKills[i] += other._bodyguardKills[i];
            for (int i = 0; i < _bodyguardDamage.Length; i++) _bodyguardDamage[i] += other._bodyguardDamage[i];
            for (int p = 0; p < _poolKills.Length; p++)
            {
                for (int i = 0; i < _poolDamage[p].Length; i++) _poolDamage[p][i] += other._poolDamage[p][i];
                for (int i = 0; i < _poolKills[p].Length; i++) _poolKills[p][i] += other._poolKills[p][i];
                _poolRawDamage[p] += other._poolRawDamage[p];
                _poolOnDestroyedUnit[p] += other._poolOnDestroyedUnit[p];
            }
            for (int i = 0; i < _precisionKilled.Length; i++) _precisionKilled[i] += other._precisionKilled[i];
            for (int i = 0; i < _precisionDamage.Length; i++) _precisionDamage[i] += other._precisionDamage[i];
            for (int g = 0; g < _precisionWeaponDamage.Length; g++)
            {
                _precisionWeaponDamage[g] += other._precisionWeaponDamage[g];
                for (int i = 0; i < _precisionWeaponDamageByTotal[g].Length; i++)
                {
                    _precisionWeaponDamageByTotal[g][i] += other._precisionWeaponDamageByTotal[g][i];
                }
                _killedByPrecisionAlone[g] += other._killedByPrecisionAlone[g];
                _killedWithHelp[g] += other._killedWithHelp[g];
                _killedWhileBodyguardsAlive[g] += other._killedWhileBodyguardsAlive[g];
                _precisionFallsShort[g] += other._precisionFallsShort[g];
                _woundsLeftWhenShort[g] += other._woundsLeftWhenShort[g];
            }
            Add(ref _extendedKills, other._extendedKills);
            Add(ref _extendedDamage, other._extendedDamage);
            Add(ref _rawDamage, other._rawDamage);
        }

        public SimulationResult ToResult(Scenario scenario, int seed, TimeSpan elapsed)
        {
            long iterations = _kills.Sum();
            var groups = scenario.Defender.Groups
                .Select((group, g) => new GroupLosses(
                    g, group, new DiscreteDistribution(_groupKills[g]), new DiscreteDistribution(_groupDamage[g]),
                    (double)_precisionKilled[g] / iterations, (double)_precisionDamage[g] / iterations,
                    tracksPrecisionWeapons && group.IsCharacter ? PrecisionWeapons(g, iterations) : null))
                .ToList();
            double totalKills = _killedPerGroup.Sum(), totalDamage = _damagePerGroup.Sum();
            var pools = AttackPool.Build(scenario.Attacker.Weapons)
                .Select((pool, p) =>
                {
                    var damage = new DiscreteDistribution(_poolDamage[p]);
                    var kills = new DiscreteDistribution(_poolKills[p]);
                    double damageSum = damage.Mean * iterations, killSum = kills.Mean * iterations;
                    return new WeaponPoolLosses(
                        p, pool.Members, damage, kills, (double)_poolRawDamage[p] / iterations,
                        (double)_poolOnDestroyedUnit[p] / iterations,
                        totalDamage > 0 ? damageSum / totalDamage : 0, totalKills > 0 ? killSum / totalKills : 0);
                })
                .ToList();
            return new(scenario, seed, elapsed,
                new DiscreteDistribution(_kills), new DiscreteDistribution(_damage), _killedPerGroup, _damagePerGroup,
                new DiscreteDistribution(TrimTrailingZeros(_extendedKills, _kills.Length)),
                new DiscreteDistribution(TrimTrailingZeros(_extendedDamage, _damage.Length)),
                new DiscreteDistribution(TrimTrailingZeros(_rawDamage, _damage.Length)),
                groups, new DiscreteDistribution(_bodyguardKills), new DiscreteDistribution(_bodyguardDamage), pools);
        }

        private PrecisionWeaponLosses PrecisionWeapons(int g, long iterations)
        {
            double meanDamage = (double)_damagePerGroup[g] / iterations;
            double meanPrecision = (double)_precisionWeaponDamage[g] / iterations;
            var shares = new double[_groupDamage[g].Length];
            for (int k = 1; k < shares.Length; k++)
            {
                long simulations = _groupDamage[g][k];
                shares[k] = simulations > 0 ? (double)_precisionWeaponDamageByTotal[g][k] / (k * simulations) : 0;
            }
            return new PrecisionWeaponLosses(
                (double)_killedByPrecisionAlone[g] / iterations,
                (double)_killedWithHelp[g] / iterations,
                meanPrecision,
                meanDamage - meanPrecision,
                (double)_killedWhileBodyguardsAlive[g] / iterations,
                (double)_precisionFallsShort[g] / iterations,
                _precisionFallsShort[g] > 0 ? (double)_woundsLeftWhenShort[g] / _precisionFallsShort[g] : 0,
                shares);
        }

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
