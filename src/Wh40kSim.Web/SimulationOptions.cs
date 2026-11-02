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

    /// <summary>Comparaison : nombre maximal de variantes (attaquants et combinaisons de profils) par lancement.</summary>
    public int MaxComparisonVariants { get; set; } = 50;

    /// <summary>
    /// Comparaison : nombre maximal de finalistes ; au-delà, seuls les meilleurs scores vont en finale (la finale coûte
    /// le plus : chaque finaliste y fait toutes ses simulations).
    /// </summary>
    public int MaxComparisonFinalists { get; set; } = 5;

    /// <summary>
    /// Fichier du journal des lancements (une ligne par simulation ou comparaison demandée) ; null ou vide : pas de
    /// journal. Sur Fly : sur le volume monté en /data (voir fly.toml).
    /// </summary>
    public string? LaunchLogPath { get; set; }
}
