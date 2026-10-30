using Wh40kSim.Core.Engine;
using Wh40kSim.Core.Model;
using Wh40kSim.Simulation.Statistics;

namespace Wh40kSim.Simulation;

/// <summary>
/// Résultat de N simulations : distributions complètes des figurines tuées et des PV retirés, pertes par groupe.
/// Tout ce qui s'affiche se lit ici, sans calcul côté console ou interface. Immuable. Toutes les statistiques sont
/// calculées depuis les compteurs bruts (<see cref="Data"/>), envoyés tels quels du serveur au navigateur.
/// </summary>
public sealed class SimulationResult
{
    private readonly long[] _killedPerGroup; // total des figurines tuées par groupe, toutes simulations confondues
    private readonly long[] _damagePerGroup; // total des PV retirés par groupe, toutes simulations confondues

    /// <exception cref="ArgumentException">Si les données sont incohérentes avec leur scénario (<see cref="SimulationData.Validate"/>).</exception>
    public SimulationResult(SimulationData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var errors = data.Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException("Données de simulation incohérentes :" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        Data = data;
        var scenario = data.Scenario;
        Scenario = scenario;
        Seed = data.Seed;
        Elapsed = data.Elapsed;
        Kills = new DiscreteDistribution(data.Kills);
        Damage = new DiscreteDistribution(data.Damage);
        ExtendedKills = new DiscreteDistribution(data.ExtendedKills);
        ExtendedDamage = new DiscreteDistribution(data.ExtendedDamage);
        RawDamage = new DiscreteDistribution(data.RawDamage);
        Waste = new DiscreteDistribution(data.Waste);
        MeanWasteWhenUnitSurvives = data.UnitSurvives > 0 ? (double)data.WasteWhenUnitSurvives / data.UnitSurvives : null;
        _killedPerGroup = data.Groups.Select(g => g.KilledTotal).ToArray();
        _damagePerGroup = data.Groups.Select(g => g.DamageTotal).ToArray();
        long iterations = Iterations;

        Groups = scenario.Defender.Groups
            .Select((group, g) =>
            {
                var groupData = data.Groups[g];
                return new GroupLosses(
                    g, group, new DiscreteDistribution(groupData.Kills), new DiscreteDistribution(groupData.Damage),
                    (double)groupData.PrecisionKilled / iterations, (double)groupData.PrecisionDamage / iterations,
                    groupData.PrecisionWeapons is { } precision ? PrecisionWeapons(groupData, precision, iterations) : null);
            })
            .ToList();

        double totalKills = _killedPerGroup.Sum(), totalDamage = _damagePerGroup.Sum();
        Pools = AttackPool.Build(scenario.FiringWeapons)
            .Select((pool, p) =>
            {
                var poolData = data.Pools[p];
                var damage = new DiscreteDistribution(poolData.Damage);
                var kills = new DiscreteDistribution(poolData.Kills);
                double damageSum = damage.Mean * iterations, killSum = kills.Mean * iterations;
                return new WeaponPoolLosses(
                    p, pool.Members, damage, kills, (double)poolData.RawDamage / iterations,
                    (double)poolData.FiredOnDestroyedUnit / iterations,
                    totalDamage > 0 ? damageSum / totalDamage : 0, totalKills > 0 ? killSum / totalKills : 0,
                    WasteShares(poolData.WasteByTotal, data.Waste), Conversion(poolData.Conversion));
            })
            .ToList();

        // Gardes du corps : seulement s'il y en a et qu'ils protègent au moins un PERSONNAGE
        var defenderGroups = scenario.Defender.Groups;
        bool bodyguards = defenderGroups.Any(g => g.IsCharacter) && defenderGroups.Any(g => !g.IsCharacter);
        BodyguardKills = bodyguards ? new DiscreteDistribution(data.BodyguardKills) : null;
        BodyguardDamage = bodyguards ? new DiscreteDistribution(data.BodyguardDamage) : null;
    }

    /// <summary>Compteurs bruts d'où ce résultat est calculé : ce qui s'envoie (<see cref="SimulationJson"/>).</summary>
    public SimulationData Data { get; }

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

    /// <summary>
    /// Gaspillage par activation : PV perdus dans une simulation (dégâts bruts - PV retirés, marge comprise), de 0 au
    /// maximum observé. Sa moyenne est <see cref="MeanWastedDamage"/>.
    /// </summary>
    public DiscreteDistribution Waste { get; }

    /// <summary>
    /// Gaspillage moyen dans les simulations où l'unité survit : PV perdus qui auraient pu tuer une figurine de plus
    /// (quand l'unité est détruite, le gaspillage n'a rien coûté). Null si l'unité est toujours détruite.
    /// </summary>
    public double? MeanWasteWhenUnitSurvives { get; }

    /// <summary>Pertes par pool d'attaques, dans l'ordre de résolution (rapport par arme).</summary>
    public IReadOnlyList<WeaponPoolLosses> Pools { get; }

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

    /// <summary>
    /// PV retirés aux gardes du corps (tous les groupes non-PERSONNAGES ensemble, de 0 à la somme de leurs PV). Null
    /// dans les mêmes cas que <see cref="BodyguardKills"/>.
    /// </summary>
    public DiscreteDistribution? BodyguardDamage { get; }

    /// <summary>Probabilité que tous les gardes du corps soient tués : les PERSONNAGES sont alors exposés.</summary>
    public double? ProbabilityBodyguardsWiped => BodyguardKills?.ProbabilityAtLeast(BodyguardKills.MaxValue);

    /// <summary>
    /// PERSONNAGE visé par Precision (voir <see cref="Scenario.PrecisionTargetGroup"/>) : la cible choisie, sinon le
    /// premier PERSONNAGE ; null sans arme Precision ou sans PERSONNAGE.
    /// </summary>
    public GroupLosses? PrecisionTarget => Scenario.PrecisionTargetGroup is int g ? Groups[g] : null;

    private static PrecisionWeaponLosses PrecisionWeapons(GroupData group, PrecisionWeaponData precision, long iterations)
    {
        double meanDamage = (double)group.DamageTotal / iterations;
        double meanPrecision = (double)precision.Damage / iterations;
        var shares = new double[group.Damage.Length];
        for (int k = 1; k < shares.Length; k++)
        {
            long simulations = group.Damage[k];
            shares[k] = simulations > 0 ? (double)precision.DamageByTotal[k] / (k * simulations) : 0;
        }
        return new PrecisionWeaponLosses(
            (double)precision.KilledByPrecisionAlone / iterations,
            (double)precision.KilledWithHelp / iterations,
            meanPrecision,
            meanDamage - meanPrecision,
            (double)precision.KilledWhileBodyguardsAlive / iterations,
            (double)precision.FallsShort / iterations,
            precision.FallsShort > 0 ? (double)precision.WoundsLeftWhenShort / precision.FallsShort : 0,
            shares);
    }

    /// <summary>
    /// Part moyenne d'un pool dans le gaspillage total k de la simulation, pour chaque k (0 si k = 0 ou jamais atteint).
    /// </summary>
    private static double[] WasteShares(long[] poolWasteByTotal, long[] waste)
    {
        var shares = new double[waste.Length];
        for (int k = 1; k < shares.Length; k++)
        {
            long poolWaste = k < poolWasteByTotal.Length ? poolWasteByTotal[k] : 0;
            shares[k] = waste[k] > 0 ? (double)poolWaste / (k * waste[k]) : 0;
        }
        return shares;
    }

    /// <summary>Conversion moyenne d'un pool sur les simulations où il a tiré ; null s'il n'a jamais tiré.</summary>
    private static WeaponConversion? Conversion(ConversionCounts counts)
    {
        if (counts.Activations == 0)
        {
            return null;
        }
        double Mean(long sum) => (double)sum / counts.Activations;
        return new WeaponConversion(
            Mean(counts.Attacks), Mean(counts.BlastAttacks), Mean(counts.RapidFireAttacks), Mean(counts.Hits),
            Mean(counts.SustainedHits), Mean(counts.LethalHits), Mean(counts.Wounds), Mean(counts.DevastatingWounds),
            Mean(counts.FailedSaves));
    }
}
