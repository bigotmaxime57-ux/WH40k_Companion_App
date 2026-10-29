using Wh40kSim.Simulation;

namespace Wh40kSim.UI.Services;

/// <summary>
/// Réglages et dernier résultat d'un mode de simulation (Quick sim ou Full sim) : chaque mode garde les siens, le
/// scénario reste commun (<see cref="ScenarioSession"/>).
/// </summary>
public sealed class SimulationRun
{
    private SimulationResult? _result;

    /// <summary>
    /// Résultat de la dernière simulation de ce mode, effacé quand un autre scénario est chargé. Un nouveau résultat
    /// rouvre le premier onglet (<see cref="ResultsTab"/>).
    /// </summary>
    public SimulationResult? Result
    {
        get => _result;
        set
        {
            if (!ReferenceEquals(_result, value))
            {
                _result = value;
                ResultsTab = 0;
            }
        }
    }

    /// <summary>
    /// Onglet des résultats affiché en dernier pour ce résultat (Full sim), retrouvé en revenant aux résultats ; remis
    /// au premier (Distributions) à chaque nouveau résultat.
    /// </summary>
    public int ResultsTab { get; set; }

    public int Iterations { get; set; } = 1_000_000;

    public int? Seed { get; set; }
}
