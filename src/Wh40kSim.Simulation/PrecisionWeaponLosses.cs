namespace Wh40kSim.Simulation;

/// <summary>
/// Travail des armes Precision (pools dont le profil a le mot-clé) sur un groupe, en général un PERSONNAGE visé, sur
/// N simulations. Les autres armes sont celles sans Precision. Pertes réelles seulement (pas de marge).
/// </summary>
/// <param name="ProbabilityKilledByPrecisionAlone">Groupe entièrement tué, tous ses PV perdus venant d'armes Precision.</param>
/// <param name="ProbabilityKilledWithHelp">Groupe entièrement tué, au moins un PV venant d'une autre arme.</param>
/// <param name="MeanPrecisionDamage">PV retirés au groupe par les armes Precision, en moyenne par simulation.</param>
/// <param name="MeanOtherDamage">PV retirés au groupe par les autres armes, en moyenne par simulation.</param>
/// <param name="ProbabilityKilledWhileBodyguardsAlive">
/// Groupe entièrement tué alors qu'au moins un garde du corps (groupe non-PERSONNAGE) est encore en vie à la fin :
/// kill impossible sans Precision.
/// </param>
/// <param name="ProbabilityPrecisionFallsShort">Les armes Precision ne tuent pas le groupe à elles seules.</param>
/// <param name="MeanWoundsLeftWhenShort">
/// Dans ces cas-là, PV que les armes Precision laissent au groupe (ses PV moins ceux qu'elles ont retirés), en
/// moyenne : ce qu'il faudrait de plus pour finir le travail. 0 si elles le tuent toujours seules.
/// </param>
/// <param name="PrecisionShareByDamage">
/// Pour chaque total k de PV retirés au groupe (indice k), part moyenne de ces k PV venant des armes Precision (0 à 1),
/// sur les simulations qui donnent k ; 0 pour k = 0 ou pour un total jamais atteint.
/// </param>
public sealed record PrecisionWeaponLosses(
    double ProbabilityKilledByPrecisionAlone,
    double ProbabilityKilledWithHelp,
    double MeanPrecisionDamage,
    double MeanOtherDamage,
    double ProbabilityKilledWhileBodyguardsAlive,
    double ProbabilityPrecisionFallsShort,
    double MeanWoundsLeftWhenShort,
    IReadOnlyList<double> PrecisionShareByDamage);
