namespace Wh40kSim.Simulation;

/// <summary>
/// Réglage des threads de calcul de <see cref="Simulator.Run"/> : l'hôte peut limiter leur nombre (laisser des cœurs à
/// l'interface et au système) et fournir son ordonnanceur (ex. threads en priorité basse). Sans effet sur les résultats.
/// </summary>
/// <param name="MaxThreads">Nombre maximal de threads de calcul (null = autant que de cœurs).</param>
/// <param name="Scheduler">Ordonnanceur des tâches de calcul (null = pool de threads de .NET).</param>
public sealed record SimulationThreads(int? MaxThreads = null, TaskScheduler? Scheduler = null);
