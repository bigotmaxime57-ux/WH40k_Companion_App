using Wh40kSim.Core.Model;
using Wh40kSim.Simulation;
using Wh40kSim.Simulation.Comparisons;

namespace Wh40kSim.UI.Services;

/// <summary>
/// Lance une simulation sans bloquer l'interface. L'implémentation dépend de l'hôte :
/// calcul local (desktop) ou appel à l'API du serveur (client web).
/// </summary>
public interface ISimulationService
{
    /// <summary>Nombre maximal de simulations par lancement (null = aucun plafond, cas du desktop).</summary>
    int? MaxIterations => null;

    /// <param name="progress">Progression entre 0 et 1 (voir <see cref="Simulator.Run"/>).</param>
    /// <param name="queuePosition">
    /// Position dans la file d'attente du serveur tant que le calcul n'a pas commencé (1 = le prochain) ; jamais appelé
    /// en desktop.
    /// </param>
    /// <exception cref="OperationCanceledException">Si <paramref name="cancellationToken"/> est annulé.</exception>
    /// <exception cref="SimulationServiceException">Si le calcul est refusé ou échoue (message à afficher).</exception>
    Task<SimulationResult> RunAsync(
        Scenario scenario,
        int iterations,
        int? seed = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        IProgress<int>? queuePosition = null);

    /// <summary>
    /// L'hôte sait lancer une comparaison (<see cref="RunComparisonAsync"/>) : le desktop oui, le client web pas avant la
    /// phase 5, étape 6 (la tuile du comparateur y est masquée).
    /// </summary>
    bool SupportsComparison => false;

    /// <summary>Nombre maximal de variantes par comparaison (null = aucun plafond, cas du desktop).</summary>
    int? MaxComparisonVariants => null;

    /// <summary>Lance une comparaison en rondes éliminatoires (voir <see cref="Comparator.Run"/>).</summary>
    /// <param name="progress">Ronde en cours et son avancement.</param>
    /// <param name="queuePosition">Position dans la file d'attente du serveur (voir <see cref="RunAsync"/>) ; jamais appelé en desktop.</param>
    /// <exception cref="OperationCanceledException">Si <paramref name="cancellationToken"/> est annulé.</exception>
    /// <exception cref="SimulationServiceException">Si le calcul est refusé ou échoue (message à afficher).</exception>
    /// <exception cref="NotSupportedException">Si l'hôte ne sait pas comparer (<see cref="SupportsComparison"/>).</exception>
    Task<ComparisonResult> RunComparisonAsync(
        Comparison comparison,
        int iterations,
        int? seed = null,
        IProgress<RoundProgress>? progress = null,
        CancellationToken cancellationToken = default,
        IProgress<int>? queuePosition = null) =>
        throw new NotSupportedException("Comparaison indisponible sur cet hôte.");
}

/// <summary>
/// Calcul refusé ou en échec (serveur injoignable, demande refusée, trop de lancements…) : un titre et le détail, à
/// afficher tels quels.
/// </summary>
public sealed class SimulationServiceException(string title, IReadOnlyList<string> errors)
    : Exception(errors.Count > 0 ? $"{title} : {string.Join(" ; ", errors)}" : title)
{
    public string Title { get; } = title;

    public IReadOnlyList<string> Errors { get; } = errors;
}
