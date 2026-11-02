using Wh40kSim.Core.Model;

namespace Wh40kSim.Simulation;

/// <summary>Avancement des rondes éliminatoires.</summary>
/// <param name="Round">Ronde en cours, à partir de 1.</param>
/// <param name="RoundCount">Nombre de rondes prévues (la dernière est la finale).</param>
/// <param name="Fraction">Avancement de la ronde en cours, entre 0 et 1.</param>
public readonly record struct RoundProgress(int Round, int RoundCount, double Fraction);

/// <summary>Issue d'un candidat : son dernier résultat, et la ronde où il a été éliminé (null s'il est finaliste).</summary>
public sealed record RoundOutcome(SimulationResult Result, int? EliminatedInRound);

/// <summary>
/// Rondes éliminatoires façon Raidbots, sur le score d'efficacité : chaque ronde simule les candidats encore en lice,
/// puis écarte ceux qui sont nettement sous le dernier du top 3. Générique : la comparaison (phase 5) et l'optimiseur
/// de l'ordre des armes (phase 6) n'en diffèrent que par leurs candidats.
/// </summary>
public static class EliminationRounds
{
    /// <summary>Nombre de places à pourvoir : le top 3 n'est jamais éliminé.</summary>
    public const int Kept = 3;

    /// <summary>
    /// Prudence de l'élimination : écart au 3e, en erreurs types de la différence, au-delà duquel un candidat est écarté
    /// (environ 1 risque sur 1 000 d'écarter un vrai membre du top 3).
    /// </summary>
    public const double EliminationThreshold = 3;

    /// <summary>
    /// Fait les rondes du <paramref name="plan"/> (simulations de chaque ronde, la dernière étant la finale), avec la même
    /// graine partout : les premières simulations d'une ronde longue sont exactement celles d'une ronde courte, et un
    /// finaliste est identique à une simulation seule de son scénario à graine égale.
    /// </summary>
    /// <returns>L'issue de chaque candidat, dans l'ordre des candidats.</returns>
    /// <exception cref="OperationCanceledException">Si le calcul est annulé (aucun résultat partiel).</exception>
    public static IReadOnlyList<RoundOutcome> Run(
        IReadOnlyList<Scenario> candidates,
        IReadOnlyList<int> plan,
        int seed,
        IProgress<RoundProgress>? progress = null,
        CancellationToken cancellationToken = default,
        SimulationThreads? threads = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(plan);
        if (candidates.Count == 0 || plan.Count == 0)
        {
            throw new ArgumentException("Il faut au moins un candidat et une ronde.");
        }

        var outcomes = new RoundOutcome?[candidates.Count];
        var alive = Enumerable.Range(0, candidates.Count).ToList();
        for (int round = 0; round < plan.Count; round++)
        {
            var results = new SimulationResult[alive.Count];
            for (int i = 0; i < alive.Count; i++)
            {
                var candidateProgress = progress is null ? null : new CandidateProgress(progress, round + 1, plan.Count, i, alive.Count);
                results[i] = Simulator.Run(candidates[alive[i]], plan[round], seed, candidateProgress, cancellationToken, threads);
            }

            bool final = round == plan.Count - 1;
            var survivors = final ? Enumerable.Range(0, alive.Count).ToList() : Survivors(results);
            for (int i = 0; i < alive.Count; i++)
            {
                outcomes[alive[i]] = new RoundOutcome(results[i], survivors.Contains(i) ? null : round + 1);
            }
            alive = survivors.Select(i => alive[i]).ToList();
        }
        return outcomes.Select(o => o!).ToList();
    }

    /// <summary>
    /// Indices des candidats qui restent en lice : les <see cref="Kept"/> meilleurs scores, et tout candidat qui n'est pas
    /// à plus de <see cref="EliminationThreshold"/> erreurs types sous le dernier d'entre eux.
    /// </summary>
    public static IReadOnlyList<int> Survivors(IReadOnlyList<SimulationResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var ordered = Enumerable.Range(0, results.Count).OrderByDescending(i => results[i].EfficiencyScore.Mean).ThenBy(i => i).ToList();
        if (ordered.Count <= Kept)
        {
            return ordered.Order().ToList();
        }
        var last = results[ordered[Kept - 1]].EfficiencyScore;
        return ordered
            .Where((i, rank) => rank < Kept || !ClearlyBelow(results[i].EfficiencyScore, last))
            .Order()
            .ToList();
    }

    private static bool ClearlyBelow(EfficiencyScore score, EfficiencyScore reference) =>
        reference.Mean - score.Mean > EliminationThreshold * Math.Sqrt(
            reference.StandardError * reference.StandardError + score.StandardError * score.StandardError);

    /// <summary>Ramène la progression d'un candidat (0 à 1) à sa part de la ronde.</summary>
    private sealed class CandidateProgress(IProgress<RoundProgress> total, int round, int roundCount, int index, int count) : IProgress<double>
    {
        public void Report(double value) => total.Report(new RoundProgress(round, roundCount, (index + value) / count));
    }
}
