using Wh40kSim.Simulation;

namespace Wh40kSim.UI.Services;

/// <summary>
/// Réglages et dernier résultat d'un mode de simulation (Quick sim ou Full sim) : chaque mode garde les siens, le
/// scénario reste commun (<see cref="ScenarioSession"/>).
/// </summary>
public sealed class SimulationRun
{
    /// <summary>Résultat de la dernière simulation de ce mode, effacé quand un autre scénario est chargé.</summary>
    public SimulationResult? Result { get; set; }

    public int Iterations { get; set; } = 1_000_000;

    public int? Seed { get; set; }
}
