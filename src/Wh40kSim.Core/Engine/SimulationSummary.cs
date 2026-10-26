using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Engine;

/// <summary>
/// Résultats agrégés de N simulations, sous forme de distributions complètes
/// (histogrammes) à partir desquelles on calcule moyennes, centiles et probabilités.
/// </summary>
public sealed class SimulationSummary
{
    private readonly long[] _kills;          // _kills[k] = nombre de simulations avec exactement k figurines tuées
    private readonly long[] _damage;         // _damage[d] = nombre de simulations avec exactement d PV retirés
    private readonly long[] _killedPerGroup; // total des figurines tuées par groupe, toutes simulations confondues

    internal SimulationSummary(Scenario scenario, int seed)
    {
        Scenario = scenario;
        Seed = seed;
        _kills = new long[scenario.Defender.TotalModels + 1];
        _damage = new long[scenario.Defender.TotalWounds + 1];
        _killedPerGroup = new long[scenario.Defender.Groups.Count];
    }

    public Scenario Scenario { get; }

    public int Seed { get; }

    public long Iterations { get; private set; }

    public TimeSpan Elapsed { get; internal set; }

    public int TotalModels => _kills.Length - 1;

    public int TotalWounds => _damage.Length - 1;

    public IReadOnlyList<long> KillsHistogram => _kills;

    public IReadOnlyList<long> DamageHistogram => _damage;

    public double MeanKills => Mean(_kills);

    public double StdDevKills => StdDev(_kills);

    public double MeanDamage => Mean(_damage);

    public double StdDevDamage => StdDev(_damage);

    /// <summary>Probabilité de détruire toute l'unité.</summary>
    public double ProbabilityWipe => ProbabilityKillsAtLeast(TotalModels);

    public double MeanKilledInGroup(int group) => Iterations == 0 ? 0 : (double)_killedPerGroup[group] / Iterations;

    public double ProbabilityKillsExactly(int kills) => Probability(_kills, kills);

    public double ProbabilityKillsAtLeast(int kills) => ProbabilityAtLeast(_kills, kills);

    public double ProbabilityDamageExactly(int damage) => Probability(_damage, damage);

    public double ProbabilityDamageAtLeast(int damage) => ProbabilityAtLeast(_damage, damage);

    /// <summary>Plus petit nombre de figurines tuées k tel que P(tuées ≤ k) ≥ p (p entre 0 et 1, 0,5 = médiane).</summary>
    public int PercentileKills(double p) => Percentile(_kills, p);

    public int PercentileDamage(double p) => Percentile(_damage, p);

    internal void Record(DefenderState state)
    {
        _kills[state.ModelsKilled]++;
        _damage[state.DamageDealt]++;
        for (int g = 0; g < _killedPerGroup.Length; g++)
        {
            _killedPerGroup[g] += state.KilledInGroup(g);
        }
        Iterations++;
    }

    internal void Merge(SimulationSummary other)
    {
        for (int i = 0; i < _kills.Length; i++) _kills[i] += other._kills[i];
        for (int i = 0; i < _damage.Length; i++) _damage[i] += other._damage[i];
        for (int i = 0; i < _killedPerGroup.Length; i++) _killedPerGroup[i] += other._killedPerGroup[i];
        Iterations += other.Iterations;
    }

    private double Mean(long[] histogram)
    {
        if (Iterations == 0) return 0;
        double sum = 0;
        for (int k = 0; k < histogram.Length; k++) sum += (double)k * histogram[k];
        return sum / Iterations;
    }

    private double StdDev(long[] histogram)
    {
        if (Iterations == 0) return 0;
        double mean = Mean(histogram);
        double sum = 0;
        for (int k = 0; k < histogram.Length; k++) sum += (k - mean) * (k - mean) * histogram[k];
        return Math.Sqrt(sum / Iterations);
    }

    private double Probability(long[] histogram, int value)
    {
        if (Iterations == 0 || value < 0 || value >= histogram.Length) return 0;
        return (double)histogram[value] / Iterations;
    }

    private double ProbabilityAtLeast(long[] histogram, int value)
    {
        if (Iterations == 0) return 0;
        if (value <= 0) return 1;
        long count = 0;
        for (int k = value; k < histogram.Length; k++) count += histogram[k];
        return (double)count / Iterations;
    }

    private int Percentile(long[] histogram, double p)
    {
        if (Iterations == 0) return 0;
        double threshold = p * Iterations;
        long cumulative = 0;
        for (int k = 0; k < histogram.Length; k++)
        {
            cumulative += histogram[k];
            if (cumulative >= threshold) return k;
        }
        return histogram.Length - 1;
    }
}
