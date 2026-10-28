using Wh40kSim.Core.Model;
using Wh40kSim.Simulation.Statistics;

namespace Wh40kSim.Simulation;

/// <summary>
/// Résultat de N simulations : distributions complètes des figurines tuées et des PV retirés, pertes par groupe.
/// Tout ce qui s'affiche se lit ici, sans calcul côté console ou interface. Immuable.
/// </summary>
public sealed class SimulationResult
{
    private readonly long[] _killedPerGroup; // total des figurines tuées par groupe, toutes simulations confondues
    private readonly long[] _damagePerGroup; // total des PV retirés par groupe, toutes simulations confondues

    internal SimulationResult(
        Scenario scenario,
        int seed,
        TimeSpan elapsed,
        DiscreteDistribution kills,
        DiscreteDistribution damage,
        long[] killedPerGroup,
        long[] damagePerGroup,
        DiscreteDistribution extendedKills,
        DiscreteDistribution extendedDamage,
        DiscreteDistribution rawDamage,
        IReadOnlyList<GroupLosses> groups,
        DiscreteDistribution bodyguardKills)
    {
        Groups = groups;
        // Gardes du corps : seulement s'il y en a et qu'ils protègent au moins un PERSONNAGE
        var defenderGroups = scenario.Defender.Groups;
        BodyguardKills = defenderGroups.Any(g => g.IsCharacter) && defenderGroups.Any(g => !g.IsCharacter) ? bodyguardKills : null;
        Scenario = scenario;
        Seed = seed;
        Elapsed = elapsed;
        Kills = kills;
        Damage = damage;
        _killedPerGroup = killedPerGroup;
        _damagePerGroup = damagePerGroup;
        ExtendedKills = extendedKills;
        ExtendedDamage = extendedDamage;
        RawDamage = rawDamage;
    }

    public Scenario Scenario { get; }

    /// <summary>Graine utilisée (tirée au hasard si aucune n'a été fournie) : la redonner reproduit ce résultat.</summary>
    public int Seed { get; }

    public long Iterations => Kills.Total;

    public TimeSpan Elapsed { get; }

    /// <summary>Distribution du nombre de figurines tuées (0 à <see cref="TotalModels"/>).</summary>
    public DiscreteDistribution Kills { get; }

    /// <summary>Distribution des PV effectivement retirés (0 à <see cref="TotalWounds"/>).</summary>
    public DiscreteDistribution Damage { get; }

    public int TotalModels => Kills.MaxValue;

    public int TotalWounds => Damage.MaxValue;

    /// <summary>Probabilité de détruire toute l'unité.</summary>
    public double ProbabilityWipe => Kills.ProbabilityAtLeast(TotalModels);

    /// <summary>Nombre moyen de figurines tuées dans le groupe <paramref name="group"/> (ordre d'allocation du défenseur).</summary>
    public double MeanKilledInGroup(int group) => (double)_killedPerGroup[group] / Iterations;

    /// <summary>Nombre moyen de PV retirés au groupe <paramref name="group"/>, figurine entamée comprise.</summary>
    public double MeanDamageInGroup(int group) => (double)_damagePerGroup[group] / Iterations;

    /// <summary>
    /// Points détruits en moyenne, au prorata des PV retirés à chaque groupe (une figurine entamée compte pour sa part :
    /// la moitié des PV d'un char vaut la moitié de ses points). Null si un groupe n'a pas de points. La marge ne
    /// rapporte rien : on ne détruit pas plus que la valeur de l'unité.
    /// </summary>
    public double? MeanPointsDestroyed
    {
        get
        {
            if (Scenario.Defender.TotalPoints is null)
            {
                return null;
            }

            double total = 0;
            for (int g = 0; g < Scenario.Defender.Groups.Count; g++)
            {
                var group = Scenario.Defender.Groups[g];
                total += group.Points!.Value * MeanDamageInGroup(g) / (group.Count * group.Wounds);
            }
            return total;
        }
    }

    /// <summary>
    /// Rendement : points détruits en moyenne par point de l'attaquant (0,5 = la moitié de sa propre valeur).
    /// Null si les points de l'attaquant ou du défenseur manquent, ou si l'attaquant ne coûte rien.
    /// </summary>
    public double? Efficiency =>
        MeanPointsDestroyed is double destroyed && Scenario.Attacker.Points is > 0 and var points ? destroyed / points : null;

    /// <summary>
    /// Figurines tuées en prolongeant la séquence après la destruction de l'unité, contre des figurines virtuelles
    /// du profil <see cref="ExcessProfile"/> : identique à <see cref="Kills"/> sous <see cref="TotalModels"/>,
    /// et répartit au-delà les cas où l'unité est détruite (la marge).
    /// </summary>
    public DiscreteDistribution ExtendedKills { get; }

    /// <summary>PV retirés en prolongeant la séquence (voir <see cref="ExtendedKills"/>).</summary>
    public DiscreteDistribution ExtendedDamage { get; }

    /// <summary>Marge moyenne quand l'unité est détruite : figurines virtuelles tuées en plus (0 si jamais détruite).</summary>
    public double KillMarginWhenWiped => ExtendedKills.MeanExcessAbove(TotalModels);

    /// <summary>Marge moyenne quand l'unité est détruite : PV retirés en plus aux figurines virtuelles (0 si jamais détruite).</summary>
    public double DamageMarginWhenWiped => ExtendedDamage.MeanExcessAbove(TotalWounds);

    /// <summary>
    /// Dégâts bruts : dégâts de chaque attaque non sauvegardée comptés en entier (après réduction de dégâts et Feel No
    /// Pain), sans plafond par figurine ni arrêt à la destruction de l'unité. Toujours au moins égaux aux PV retirés
    /// de <see cref="ExtendedDamage"/>.
    /// </summary>
    public DiscreteDistribution RawDamage { get; }

    /// <summary>
    /// Gaspillage moyen : dégâts bruts perdus parce qu'une attaque dépasse les PV restants de sa figurine. Ce qui dépasse
    /// la destruction de l'unité n'en fait pas partie : c'est la marge (<see cref="DamageMarginWhenWiped"/>).
    /// </summary>
    public double MeanWastedDamage => RawDamage.Mean - ExtendedDamage.Mean;

    /// <summary>Part des dégâts bruts gaspillée (entre 0 et 1 ; 0 si aucun dégât brut).</summary>
    public double WastedShare => RawDamage.Mean > 0 ? MeanWastedDamage / RawDamage.Mean : 0;

    /// <summary>Profil des figurines virtuelles qui encaissent l'excédent (voir <see cref="DefenderUnit.ExcessProfile"/>).</summary>
    public ModelGroup ExcessProfile => Scenario.Defender.ExcessProfile!;

    /// <summary>Pertes de chaque groupe d'allocation, dans l'ordre d'allocation du défenseur (pertes réelles).</summary>
    public IReadOnlyList<GroupLosses> Groups { get; }

    /// <summary>
    /// Figurines de garde du corps tuées (tous les groupes non-PERSONNAGES ensemble, de 0 à leur effectif total). Null
    /// s'il n'y a pas à la fois des gardes du corps et au moins un PERSONNAGE : il n'y a alors personne à protéger.
    /// </summary>
    public DiscreteDistribution? BodyguardKills { get; }

    /// <summary>Probabilité que tous les gardes du corps soient tués : les PERSONNAGES sont alors exposés.</summary>
    public double? ProbabilityBodyguardsWiped => BodyguardKills?.ProbabilityAtLeast(BodyguardKills.MaxValue);

    /// <summary>
    /// Groupes PERSONNAGES que Precision peut viser (voir <see cref="Scenario.PrecisionTargets"/>) : vide sans arme
    /// Precision ou sans PERSONNAGE.
    /// </summary>
    public IReadOnlyList<GroupLosses> PrecisionTargets => Scenario.PrecisionTargets.Select(g => Groups[g]).ToList();
}
