using System.Text.Json.Serialization;
using Wh40kSim.Core.Engine;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Simulation;

/// <summary>
/// Données brutes d'une simulation : les compteurs du simulateur (entiers seulement), d'où <see cref="SimulationResult"/>
/// recalcule toutes les statistiques. Sérialisable (<see cref="SimulationJson"/>) : c'est le contrat du résultat entre
/// le serveur et le navigateur. Un histogramme <c>h</c> compte les simulations : <c>h[k]</c> = nombre de simulations où
/// la valeur vaut exactement k ; le nombre de simulations est la somme de chacun.
/// </summary>
public sealed record SimulationData
{
    public required Scenario Scenario { get; init; }

    /// <summary>Graine utilisée : la redonner reproduit ce résultat.</summary>
    public required int Seed { get; init; }

    public required TimeSpan Elapsed { get; init; }

    /// <summary>Figurines tuées (0 à l'effectif de l'unité).</summary>
    public required long[] Kills { get; init; }

    /// <summary>PV retirés (0 aux PV de l'unité).</summary>
    public required long[] Damage { get; init; }

    /// <summary>Figurines tuées en prolongeant la séquence après la destruction de l'unité (marge).</summary>
    public required long[] ExtendedKills { get; init; }

    /// <summary>PV retirés en prolongeant la séquence (marge).</summary>
    public required long[] ExtendedDamage { get; init; }

    /// <summary>Dégâts bruts (sans plafond par figurine ni arrêt à la destruction).</summary>
    public required long[] RawDamage { get; init; }

    /// <summary>Gaspillage par simulation (dégâts bruts - PV retirés, marge comprise).</summary>
    public required long[] Waste { get; init; }

    /// <summary>Gaspillage cumulé sur les simulations où l'unité survit.</summary>
    public required long WasteWhenUnitSurvives { get; init; }

    /// <summary>Nombre de simulations où l'unité survit.</summary>
    public required long UnitSurvives { get; init; }

    /// <summary>Figurines de garde du corps tuées (groupes non-PERSONNAGES ensemble).</summary>
    public required long[] BodyguardKills { get; init; }

    /// <summary>PV retirés aux gardes du corps.</summary>
    public required long[] BodyguardDamage { get; init; }

    /// <summary>Un élément par groupe d'allocation, dans l'ordre du défenseur.</summary>
    public required IReadOnlyList<GroupData> Groups { get; init; }

    /// <summary>Un élément par pool d'attaques, dans l'ordre de résolution.</summary>
    public required IReadOnlyList<PoolData> Pools { get; init; }

    /// <summary>Nombre de simulations.</summary>
    [JsonIgnore]
    public long Iterations => Kills.Sum();

    /// <summary>
    /// Incohérences avec le scénario (vide si les données sont utilisables) : tailles des histogrammes, nombre de groupes
    /// et de pools, simulations comptées, valeurs négatives.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Scenario is null)
        {
            return ["Le scénario manque."];
        }
        var scenarioErrors = Scenario.Validate();
        if (scenarioErrors.Count > 0)
        {
            return [.. scenarioErrors.Select(e => "Scénario : " + e)];
        }

        var defender = Scenario.Defender;
        long iterations = Kills?.Sum() ?? 0;
        if (iterations <= 0)
        {
            errors.Add("Aucune simulation comptée.");
        }

        void Histogram(long[]? counts, string name, int? exactLength = null, int minLength = 1)
        {
            if (counts is null)
            {
                errors.Add($"{name} : histogramme absent.");
                return;
            }
            if (exactLength is int length && counts.Length != length)
            {
                errors.Add($"{name} : {counts.Length} valeurs au lieu de {length}.");
            }
            else if (counts.Length < minLength)
            {
                errors.Add($"{name} : {counts.Length} valeurs, au moins {minLength} attendues.");
            }
            if (counts.Any(c => c < 0))
            {
                errors.Add($"{name} : nombre négatif.");
            }
            else if (counts.Sum() != iterations)
            {
                errors.Add($"{name} : {counts.Sum()} simulations au lieu de {iterations}.");
            }
        }

        var bodyguards = defender.Groups.Where(g => !g.IsCharacter).ToList();
        Histogram(Kills, "Figurines tuées", defender.TotalModels + 1);
        Histogram(Damage, "PV retirés", defender.TotalWounds + 1);
        Histogram(ExtendedKills, "Figurines tuées (marge)", minLength: defender.TotalModels + 1);
        Histogram(ExtendedDamage, "PV retirés (marge)", minLength: defender.TotalWounds + 1);
        Histogram(RawDamage, "Dégâts bruts", minLength: defender.TotalWounds + 1);
        Histogram(Waste, "Gaspillage");
        Histogram(BodyguardKills, "Gardes du corps tués", bodyguards.Sum(g => g.Count) + 1);
        Histogram(BodyguardDamage, "PV retirés aux gardes du corps", bodyguards.Sum(g => g.Count * g.Wounds) + 1);
        if (UnitSurvives < 0 || UnitSurvives > iterations || WasteWhenUnitSurvives < 0)
        {
            errors.Add("Simulations où l'unité survit : valeurs impossibles.");
        }

        if (Groups is null || Groups.Count != defender.Groups.Count)
        {
            errors.Add($"Groupes : {Groups?.Count ?? 0} au lieu de {defender.Groups.Count}.");
        }
        else
        {
            for (int g = 0; g < Groups.Count; g++)
            {
                var group = defender.Groups[g];
                var data = Groups[g];
                string label = $"Groupe '{group.Name}'";
                Histogram(data.Kills, $"{label}, figurines tuées", group.Count + 1);
                Histogram(data.Damage, $"{label}, PV retirés", group.Count * group.Wounds + 1);
                bool tracked = Scenario.UsesPrecision && group.IsCharacter;
                if (tracked != data.PrecisionWeapons is not null)
                {
                    errors.Add($"{label} : suivi des armes Precision {(tracked ? "absent" : "en trop")}.");
                }
                else if (data.PrecisionWeapons is { } precision && precision.DamageByTotal?.Length != group.Count * group.Wounds + 1)
                {
                    errors.Add($"{label} : PV des armes Precision par total de taille incorrecte.");
                }
            }
        }

        int poolCount = AttackPool.Build(Scenario.FiringWeapons).Count;
        if (Pools is null || Pools.Count != poolCount)
        {
            errors.Add($"Pools : {Pools?.Count ?? 0} au lieu de {poolCount}.");
        }
        else
        {
            for (int p = 0; p < Pools.Count; p++)
            {
                var pool = Pools[p];
                string label = $"Pool {p + 1}";
                Histogram(pool.Damage, $"{label}, PV retirés", defender.TotalWounds + 1);
                Histogram(pool.Kills, $"{label}, figurines achevées", defender.TotalModels + 1);
                if (pool.WasteByTotal is null || pool.WasteByTotal.Length == 0 || pool.WasteByTotal.Length > (Waste?.Length ?? 0))
                {
                    errors.Add($"{label} : gaspillage par total de taille incorrecte.");
                }
                if (pool.Conversion is null || pool.Conversion.Activations > iterations || pool.FiredOnDestroyedUnit > iterations)
                {
                    errors.Add($"{label} : compteurs impossibles.");
                }
            }
        }

        return errors;
    }
}

/// <summary>Compteurs bruts d'un groupe d'allocation.</summary>
public sealed record GroupData
{
    /// <summary>Figurines tuées dans le groupe, cumulées sur toutes les simulations.</summary>
    public required long KilledTotal { get; init; }

    /// <summary>PV retirés au groupe, cumulés.</summary>
    public required long DamageTotal { get; init; }

    /// <summary>Histogramme des figurines tuées dans le groupe.</summary>
    public required long[] Kills { get; init; }

    /// <summary>Histogramme des PV retirés au groupe.</summary>
    public required long[] Damage { get; init; }

    /// <summary>Figurines tuées « grâce à Precision », cumulées.</summary>
    public required long PrecisionKilled { get; init; }

    /// <summary>PV retirés « grâce à Precision », cumulés.</summary>
    public required long PrecisionDamage { get; init; }

    /// <summary>Travail des armes Precision (groupe PERSONNAGE, attaquant avec une arme Precision), sinon null.</summary>
    public PrecisionWeaponData? PrecisionWeapons { get; init; }
}

/// <summary>Compteurs bruts des armes Precision sur un groupe PERSONNAGE.</summary>
public sealed record PrecisionWeaponData
{
    /// <summary>PV retirés par les armes Precision, cumulés.</summary>
    public required long Damage { get; init; }

    /// <summary>PV retirés par les armes Precision, cumulés par total de PV retirés au groupe.</summary>
    public required long[] DamageByTotal { get; init; }

    public required long KilledByPrecisionAlone { get; init; }

    public required long KilledWithHelp { get; init; }

    public required long KilledWhileBodyguardsAlive { get; init; }

    /// <summary>Simulations où les armes Precision ne le tuent pas seules.</summary>
    public required long FallsShort { get; init; }

    /// <summary>PV qu'elles lui laissent alors, cumulés.</summary>
    public required long WoundsLeftWhenShort { get; init; }
}

/// <summary>Compteurs bruts d'un pool d'attaques (séquence réelle).</summary>
public sealed record PoolData
{
    /// <summary>Histogramme des PV retirés par le pool.</summary>
    public required long[] Damage { get; init; }

    /// <summary>Histogramme des figurines achevées par le pool.</summary>
    public required long[] Kills { get; init; }

    /// <summary>Dégâts bruts du pool, cumulés.</summary>
    public required long RawDamage { get; init; }

    /// <summary>Simulations où l'unité était déjà détruite quand le pool a tiré.</summary>
    public required long FiredOnDestroyedUnit { get; init; }

    /// <summary>Gaspillage du pool, cumulé par gaspillage total de la simulation.</summary>
    public required long[] WasteByTotal { get; init; }

    public required ConversionCounts Conversion { get; init; }
}

/// <summary>Conversion des attaques d'un pool, cumulée sur les simulations où il tire sur une unité en jeu.</summary>
public sealed record ConversionCounts
{
    /// <summary>Nombre de ces simulations.</summary>
    public required long Activations { get; init; }

    public required long Attacks { get; init; }

    public required long BlastAttacks { get; init; }

    public required long RapidFireAttacks { get; init; }

    public required long Hits { get; init; }

    public required long SustainedHits { get; init; }

    public required long LethalHits { get; init; }

    public required long Wounds { get; init; }

    public required long DevastatingWounds { get; init; }

    public required long FailedSaves { get; init; }
}
