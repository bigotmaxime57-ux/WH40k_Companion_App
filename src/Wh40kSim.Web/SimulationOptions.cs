namespace Wh40kSim.Web;

/// <summary>
/// Limites du serveur, section « Simulation » de la configuration (appsettings.json, ou variables d'environnement
/// comme <c>Simulation__MaxIterations</c>). Les valeurs par défaut sont celles décidées avec l'utilisateur.
/// </summary>
public sealed class SimulationOptions
{
    public const string Section = "Simulation";

    /// <summary>Nombre maximal de simulations par lancement.</summary>
    public int MaxIterations { get; set; } = 1_000_000;

    /// <summary>Calculs menés en même temps ; les autres demandes attendent leur tour, dans l'ordre d'arrivée.</summary>
    public int MaxConcurrentSimulations { get; set; } = 1;

    /// <summary>Lancements acceptés par minute et par adresse IP.</summary>
    public int LaunchesPerMinute { get; set; } = 10;

    /// <summary>Threads de calcul par simulation (null = tous les cœurs).</summary>
    public int? ThreadsPerSimulation { get; set; }
}
