using Wh40kSim.Core.Model;
using Wh40kSim.Simulation.Statistics;

namespace Wh40kSim.Simulation;

/// <summary>
/// Pertes d'un groupe d'allocation sur N simulations : distributions des figurines tuées (0 à l'effectif) et des PV
/// retirés (0 aux PV du groupe), et pertes « grâce à Precision » (attaque allouée au groupe visé par Precision alors
/// que, sans Precision, elle serait allée sur un autre groupe ; figurine attribuée à l'attaque qui l'achève).
/// Pertes réelles seulement : l'excédent (marge) ne concerne pas les groupes.
/// </summary>
/// <param name="Index">Position du groupe dans l'ordre d'allocation du défenseur.</param>
/// <param name="MeanPrecisionKills">Figurines du groupe achevées grâce à Precision, en moyenne par simulation.</param>
/// <param name="MeanPrecisionDamage">PV retirés au groupe grâce à Precision, en moyenne par simulation.</param>
/// <param name="PrecisionWeapons">
/// Travail des armes Precision sur un groupe PERSONNAGE ; null si l'attaquant n'en a pas, ou pour un garde du corps.
/// </param>
public sealed record GroupLosses(
    int Index,
    ModelGroup Group,
    DiscreteDistribution Kills,
    DiscreteDistribution Damage,
    double MeanPrecisionKills,
    double MeanPrecisionDamage,
    PrecisionWeaponLosses? PrecisionWeapons = null)
{
    /// <summary>Probabilité que tout le groupe soit tué (pour un groupe PERSONNAGE : tuer le PERSONNAGE).</summary>
    public double ProbabilityWiped => Kills.ProbabilityAtLeast(Group.Count);

    /// <summary>Part des figurines tuées dans ce groupe qui l'ont été grâce à Precision (0 si aucune perte).</summary>
    public double PrecisionKillShare => Kills.Mean > 0 ? MeanPrecisionKills / Kills.Mean : 0;

    /// <summary>Part des PV retirés à ce groupe qui l'ont été grâce à Precision (0 si aucune perte).</summary>
    public double PrecisionDamageShare => Damage.Mean > 0 ? MeanPrecisionDamage / Damage.Mean : 0;
}
