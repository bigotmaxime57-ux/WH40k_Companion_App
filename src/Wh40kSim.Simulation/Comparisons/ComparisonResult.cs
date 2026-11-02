using Wh40kSim.Core.Model;

namespace Wh40kSim.Simulation.Comparisons;

/// <summary>
/// Place d'une entrée dans le classement : les finalistes par score d'efficacité moyen, puis les éliminés.
/// </summary>
/// <param name="Rank">Rang, de 1 au nombre d'entrées (pas de rang partagé : les égalités sont signalées à part).</param>
/// <param name="Index">Indice de l'entrée dans l'ordre de saisie (<see cref="ComparisonResult.Entries"/>).</param>
/// <param name="Result">Résultat de sa dernière ronde (moins de simulations pour une entrée éliminée).</param>
/// <param name="EliminatedInRound">Ronde où l'entrée a été éliminée, à partir de 1 ; null pour un finaliste.</param>
/// <param name="TiedWithPrevious">
/// L'écart de score avec le finaliste classé juste devant n'est pas significatif (dans le bruit de la simulation) ;
/// toujours faux pour le premier et pour les éliminés.
/// </param>
/// <param name="Variant">Variante classée : son attaquant d'origine et les profils choisis.</param>
public sealed record RankedEntry(int Rank, int Index, SimulationResult Result, int? EliminatedInRound, bool TiedWithPrevious, ComparisonVariant Variant)
{
    public string Name => Result.Scenario.Attacker.Name;

    public bool IsFinalist => EliminatedInRound is null;
}

/// <summary>
/// Meilleurs profils d'un attaquant qui a des profils de rechange : ceux de sa variante la mieux classée.
/// </summary>
/// <param name="Entry">Numéro de l'attaquant dans la comparaison.</param>
/// <param name="AttackerName">Nom de l'attaquant (sans les profils).</param>
/// <param name="Best">Sa variante la mieux classée.</param>
public sealed record BestProfiles(int Entry, string AttackerName, RankedEntry Best)
{
    public IReadOnlyList<ProfileChoice> Choices => Best.Variant.Choices;
}

/// <summary>
/// Résultat d'une comparaison en rondes éliminatoires : le dernier résultat de chaque attaquant, classement (finalistes
/// par score d'efficacité, puis éliminés), top 3 et meilleure(s) entrée(s) pour chaque indicateur, parmi les finalistes
/// (tous simulés autant de fois). Deux valeurs sont à égalité quand leur écart ne dépasse pas
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
        Entries = data.Entries.Select(e => new SimulationResult(e.Simulation)).ToList();
        Comparison = data.Comparison;
        Variants = Comparison.Variants();
        _finalists = Enumerable.Range(0, Entries.Count).Where(i => data.Entries[i].EliminatedInRound is null).ToList();

        // Finalistes d'abord (round = nombre de rondes + 1), puis éliminés des dernières rondes aux premières ; par score dans chaque groupe
        var ordered = Entries
            .Select((result, index) => (result, index, round: data.Entries[index].EliminatedInRound ?? int.MaxValue))
            .OrderByDescending(e => e.round)
            .ThenByDescending(e => e.result.EfficiencyScore.Mean)
            .ThenBy(e => e.index)
            .ToList();
        Ranking = ordered
            .Select((e, rank) => new RankedEntry(
                rank + 1, e.index, e.result, data.Entries[e.index].EliminatedInRound,
                rank > 0 && e.round == int.MaxValue && AreTied(ordered[rank - 1].result.EfficiencyScore, e.result.EfficiencyScore),
                Variants[e.index]))
            .ToList();
        BestProfilesByAttacker = Comparison.Entries
            .Select((entry, index) => (entry, index))
            .Where(e => e.entry.Alternatives.Count > 0)
            .Select(e => new BestProfiles(e.index, e.entry.Attacker.Name, Ranking.First(r => r.Variant.Entry == e.index)))
            .ToList();
    }

    /// <summary>Variante de chaque entrée (dans le même ordre que <see cref="Entries"/>) : attaquant d'origine, profils choisis.</summary>
    public IReadOnlyList<ComparisonVariant> Variants { get; }

    /// <summary>
    /// Pour chaque attaquant qui a des profils de rechange, dans l'ordre de saisie : les profils de sa variante la mieux
    /// classée (le meilleur profil pour chaque arme).
    /// </summary>
    public IReadOnlyList<BestProfiles> BestProfilesByAttacker { get; }

    private readonly IReadOnlyList<int> _finalists;

    /// <summary>Compteurs bruts d'où ce résultat est calculé.</summary>
    public ComparisonData Data { get; }

    /// <summary>Définition comparée (défenseur, overwatch, attaquants avec leur situation et leurs profils de rechange).</summary>
    public Comparison Comparison { get; }

    public DefenderUnit Defender => Comparison.Defender;

    /// <summary>Graine commune à toutes les entrées.</summary>
    public int Seed => Entries[0].Seed;

    /// <summary>Nombre de simulations de chaque finaliste (celui de la finale).</summary>
    public long Iterations => Data.Rounds[^1];

    /// <summary>Simulations de chaque ronde, de la première à la finale.</summary>
    public IReadOnlyList<int> Rounds => Data.Rounds;

    /// <summary>Durée totale du calcul, toutes rondes comprises.</summary>
    public TimeSpan Elapsed => Data.Elapsed;

    /// <summary>Dernier résultat de chaque variante, dans l'ordre de <see cref="Comparison.Variants"/> (moins de simulations pour une variante éliminée).</summary>
    public IReadOnlyList<SimulationResult> Entries { get; }

    /// <summary>Indices des finalistes (ordre de saisie) : seules entrées comparées entre elles pour les indicateurs.</summary>
    public IReadOnlyList<int> Finalists => _finalists;

    /// <summary>
    /// Toutes les entrées, de la meilleure à la moins bonne : finalistes par score d'efficacité moyen (ordre de saisie si
    /// égalité exacte), puis éliminés, des dernières rondes aux premières, par score.
    /// </summary>
    public IReadOnlyList<RankedEntry> Ranking { get; }

    /// <summary>Les gagnants : les <see cref="PodiumSize"/> premiers du classement (toutes les entrées s'il y en a moins), tous finalistes.</summary>
    public IReadOnlyList<RankedEntry> Podium => Ranking.Take(PodiumSize).ToList();

    /// <summary>
    /// Le premier non retenu (4e) est à égalité statistique avec le dernier gagnant (3e) : le top 3 est alors incertain à
    /// sa limite, ce qu'il faut signaler.
    /// </summary>
    public bool PodiumBoundaryTied => Ranking.Count > PodiumSize && Ranking[PodiumSize].TiedWithPrevious;

    /// <summary>Meilleur(s) score(s) d'efficacité moyen(s) parmi les finalistes : indices des entrées (ordre de saisie), ex æquo compris.</summary>
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
        var values = _finalists.Select(i => (Index: i, Indicator: indicator(Entries[i]))).ToList();
        var best = values.MaxBy(v => v.Indicator.Value).Indicator;
        return values
            .Where(v => AreTied(best.Value, best.Error, v.Indicator.Value, v.Indicator.Error))
            .Select(v => v.Index)
            .ToList();
    }

    private static double MeanError(double stdDev, long iterations) => stdDev / Math.Sqrt(iterations);

    private static double ProbabilityError(double p, long iterations) => Math.Sqrt(p * (1 - p) / iterations);
}
