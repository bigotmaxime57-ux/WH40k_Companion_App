using Wh40kSim.Core.Model;

namespace Wh40kSim.Simulation.Comparisons;

/// <summary>
/// Place d'une entrée dans le classement par score d'efficacité moyen.
/// </summary>
/// <param name="Rank">Rang, de 1 au nombre d'entrées (pas de rang partagé : les égalités sont signalées à part).</param>
/// <param name="Index">Indice de l'entrée dans l'ordre de saisie (<see cref="ComparisonResult.Entries"/>).</param>
/// <param name="TiedWithPrevious">
/// L'écart de score avec l'entrée classée juste devant n'est pas significatif (dans le bruit de la simulation) ;
/// toujours faux pour la première.
/// </param>
public sealed record RankedEntry(int Rank, int Index, SimulationResult Result, bool TiedWithPrevious)
{
    public string Name => Result.Scenario.Attacker.Name;
}

/// <summary>
/// Résultat d'une comparaison : une simulation complète par attaquant, classement par score d'efficacité, top 3 et
/// meilleure(s) entrée(s) pour chaque indicateur. Deux valeurs sont à égalité quand leur écart ne dépasse pas
/// <see cref="SignificanceThreshold"/> erreurs types (entrées simulées indépendamment). Immuable, calculé depuis les
/// compteurs bruts (<see cref="Data"/>).
/// </summary>
public sealed class ComparisonResult
{
    /// <summary>Écart significatif : au-delà de 2 erreurs types de la différence (environ 95 % de confiance).</summary>
    public const double SignificanceThreshold = 2;

    /// <summary>Nombre de gagnants : le top 3, toujours 3 entrées (décision de l'utilisateur).</summary>
    public const int PodiumSize = 3;

    /// <exception cref="ArgumentException">Si les données sont incohérentes (<see cref="ComparisonData.Validate"/>).</exception>
    public ComparisonResult(ComparisonData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var errors = data.Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException("Données de comparaison incohérentes :" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        Data = data;
        Entries = data.Entries.Select(d => new SimulationResult(d)).ToList();
        var first = Entries[0].Scenario;
        Comparison = new Comparison { Defender = first.Defender, Context = first.Context, Attackers = Entries.Select(e => e.Scenario.Attacker).ToList() };

        var ordered = Entries
            .Select((result, index) => (result, index))
            .OrderByDescending(e => e.result.EfficiencyScore.Mean)
            .ThenBy(e => e.index)
            .ToList();
        Ranking = ordered
            .Select((e, rank) => new RankedEntry(
                rank + 1, e.index, e.result,
                rank > 0 && AreTied(ordered[rank - 1].result.EfficiencyScore, e.result.EfficiencyScore)))
            .ToList();
    }

    /// <summary>Compteurs bruts d'où ce résultat est calculé.</summary>
    public ComparisonData Data { get; }

    /// <summary>Définition comparée (défenseur, situation, attaquants dans l'ordre de saisie).</summary>
    public Comparison Comparison { get; }

    public DefenderUnit Defender => Comparison.Defender;

    /// <summary>Graine commune à toutes les entrées.</summary>
    public int Seed => Entries[0].Seed;

    /// <summary>Nombre de simulations de chaque entrée.</summary>
    public long Iterations => Entries[0].Iterations;

    /// <summary>Durée totale du calcul.</summary>
    public TimeSpan Elapsed => TimeSpan.FromTicks(Entries.Sum(e => e.Elapsed.Ticks));

    /// <summary>Résultat complet de chaque entrée, dans l'ordre de saisie.</summary>
    public IReadOnlyList<SimulationResult> Entries { get; }

    /// <summary>Toutes les entrées, de la meilleure à la moins bonne (score d'efficacité moyen ; ordre de saisie si égalité exacte).</summary>
    public IReadOnlyList<RankedEntry> Ranking { get; }

    /// <summary>Les gagnants : les <see cref="PodiumSize"/> premiers du classement (toutes les entrées s'il y en a moins).</summary>
    public IReadOnlyList<RankedEntry> Podium => Ranking.Take(PodiumSize).ToList();

    /// <summary>
    /// Le premier non retenu (4e) est à égalité statistique avec le dernier gagnant (3e) : le top 3 est alors incertain à
    /// sa limite, ce qu'il faut signaler.
    /// </summary>
    public bool PodiumBoundaryTied => Ranking.Count > PodiumSize && Ranking[PodiumSize].TiedWithPrevious;

    /// <summary>Meilleur(s) score(s) d'efficacité moyen(s) : indices des entrées (ordre de saisie), ex æquo compris.</summary>
    public IReadOnlyList<int> BestScore => Best(r => (r.EfficiencyScore.Mean, r.EfficiencyScore.StandardError));

    /// <summary>Le plus de figurines tuées en moyenne.</summary>
    public IReadOnlyList<int> BestMeanKills => Best(r => (r.Kills.Mean, MeanError(r.Kills.StdDev, r.Iterations)));

    /// <summary>Le plus de PV retirés en moyenne (même classement que la part de l'unité détruite : le défenseur est commun).</summary>
    public IReadOnlyList<int> BestMeanDamage => Best(r => (r.Damage.Mean, MeanError(r.Damage.StdDev, r.Iterations)));

    /// <summary>La plus forte probabilité de détruire l'unité.</summary>
    public IReadOnlyList<int> BestWipe => Best(r => (r.ProbabilityWipe, ProbabilityError(r.ProbabilityWipe, r.Iterations)));

    /// <summary>La plus forte probabilité de tuer au moins <paramref name="models"/> figurines.</summary>
    public IReadOnlyList<int> BestKillsAtLeast(int models) =>
        Best(r => (r.Kills.ProbabilityAtLeast(models), ProbabilityError(r.Kills.ProbabilityAtLeast(models), r.Iterations)));

    /// <summary>La plus forte probabilité de retirer au moins <paramref name="wounds"/> PV.</summary>
    public IReadOnlyList<int> BestDamageAtLeast(int wounds) =>
        Best(r => (r.Damage.ProbabilityAtLeast(wounds), ProbabilityError(r.Damage.ProbabilityAtLeast(wounds), r.Iterations)));

    /// <summary>Deux valeurs à égalité : écart dans le bruit de la simulation (au plus <see cref="SignificanceThreshold"/> erreurs types).</summary>
    public static bool AreTied(double value, double error, double other, double otherError) =>
        Math.Abs(value - other) <= SignificanceThreshold * Math.Sqrt(error * error + otherError * otherError);

    private static bool AreTied(EfficiencyScore score, EfficiencyScore other) =>
        AreTied(score.Mean, score.StandardError, other.Mean, other.StandardError);

    private IReadOnlyList<int> Best(Func<SimulationResult, (double Value, double Error)> indicator)
    {
        var values = Entries.Select(indicator).ToList();
        var best = values.MaxBy(v => v.Value);
        return Enumerable.Range(0, values.Count)
            .Where(i => AreTied(best.Value, best.Error, values[i].Value, values[i].Error))
            .ToList();
    }

    private static double MeanError(double stdDev, long iterations) => stdDev / Math.Sqrt(iterations);

    private static double ProbabilityError(double p, long iterations) => Math.Sqrt(p * (1 - p) / iterations);
}
