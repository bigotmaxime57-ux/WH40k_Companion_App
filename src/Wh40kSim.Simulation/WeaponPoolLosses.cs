using Wh40kSim.Core.Model;
using Wh40kSim.Simulation.Statistics;

namespace Wh40kSim.Simulation;

/// <summary>
/// Pertes infligées par un pool d'attaques (armes aux attaques identiques, lancées ensemble : leurs dés sont
/// indiscernables) sur N simulations. Séquence réelle seulement (pas la marge) ; une figurine compte pour l'attaque qui
/// l'achève.
/// </summary>
/// <param name="Index">Position du pool dans l'ordre de résolution.</param>
/// <param name="Weapons">Armes du pool, dans l'ordre de saisie (le nom du pool vient de leurs noms).</param>
/// <param name="Damage">PV retirés par le pool (0 aux PV de l'unité).</param>
/// <param name="Kills">Figurines achevées par le pool (0 à l'effectif de l'unité).</param>
/// <param name="MeanRawDamage">
/// Dégâts bruts du pool en moyenne, jusqu'à la destruction de l'unité seulement : ce qui suit n'est pas du gaspillage
/// mais un tir sur une unité déjà détruite (<see cref="ProbabilityUnitAlreadyDestroyed"/>).
/// </param>
/// <param name="ProbabilityUnitAlreadyDestroyed">L'unité est déjà détruite quand le pool commence à tirer.</param>
/// <param name="DamageShare">Part des PV retirés à l'unité qui viennent de ce pool (0 à 1).</param>
/// <param name="KillShare">Part des figurines tuées qui l'ont été par ce pool (0 à 1).</param>
/// <param name="WasteShareByTotal">
/// Pour chaque gaspillage total k d'une simulation (indice, voir <see cref="SimulationResult.Waste"/>), part moyenne de
/// ce pool dans ces k PV perdus, marge comprise (0 à 1 ; la somme sur les pools vaut 1 pour tout k atteint, k > 0).
/// </param>
public sealed record WeaponPoolLosses(
    int Index,
    IReadOnlyList<WeaponGroup> Weapons,
    DiscreteDistribution Damage,
    DiscreteDistribution Kills,
    double MeanRawDamage,
    double ProbabilityUnitAlreadyDestroyed,
    double DamageShare,
    double KillShare,
    IReadOnlyList<double> WasteShareByTotal)
{
    /// <summary>Figurines achevées par le pool, en moyenne par simulation.</summary>
    public double MeanKills => Kills.Mean;

    /// <summary>Gaspillage moyen du pool : dégâts bruts perdus parce qu'une attaque dépasse les PV de sa figurine.</summary>
    public double MeanWastedDamage => MeanRawDamage - Damage.Mean;

    /// <summary>Part des dégâts bruts du pool gaspillée (0 à 1 ; 0 si aucun dégât brut).</summary>
    public double WastedShare => MeanRawDamage > 0 ? MeanWastedDamage / MeanRawDamage : 0;
}
