using System.Text.Json.Serialization;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Simulation.Comparisons;

/// <summary>Un attaquant comparé et sa propre situation (a chargé, immobile, demi-portée, cible de Precision).</summary>
public sealed record ComparisonEntry
{
    public required AttackerUnit Attacker { get; init; }

    /// <summary>Situation propre à cet attaquant ; le tir en overwatch, commun, est porté par la comparaison.</summary>
    public AttackContext Context { get; init; } = new();
}

/// <summary>
/// Comparaison de plusieurs attaquants contre un même défenseur : qui fait le mieux ? Chaque attaquant forme une
/// entrée, désignée par son nom (unique), avec sa propre situation ; le tir en overwatch est commun à tous (décision
/// de l'utilisateur). Immuable ; JSON : <see cref="ComparisonJson"/>.
/// </summary>
public sealed record Comparison
{
    /// <summary>Nombre minimal d'attaquants (pas de maximum).</summary>
    public const int MinAttackers = 2;

    public required DefenderUnit Defender { get; init; }

    /// <summary>Tir en overwatch (snap shooting), pour tous les attaquants.</summary>
    public bool SnapShooting { get; init; }

    /// <summary>Attaquants comparés et leur situation, dans l'ordre de saisie.</summary>
    public required IReadOnlyList<ComparisonEntry> Entries { get; init; }

    /// <summary>Attaquants comparés, dans l'ordre de saisie.</summary>
    [JsonIgnore]
    public IReadOnlyList<AttackerUnit> Attackers => Entries.Select(e => e.Attacker).ToList();

    /// <summary>Comparaison d'attaquants qui ont tous la situation par défaut.</summary>
    public static Comparison Of(DefenderUnit defender, IEnumerable<AttackerUnit> attackers, bool snapShooting = false) => new()
    {
        Defender = defender,
        SnapShooting = snapShooting,
        Entries = attackers.Select(a => new ComparisonEntry { Attacker = a }).ToList(),
    };

    /// <summary>Un scénario par attaquant, dans l'ordre de saisie : sa situation, avec le tir en overwatch commun.</summary>
    public IReadOnlyList<Scenario> ToScenarios() =>
        Entries.Select(e => new Scenario { Attacker = e.Attacker, Defender = Defender, Context = e.Context with { SnapShooting = SnapShooting } }).ToList();

    /// <summary>
    /// Incohérences (vide si la comparaison peut être lancée) : nombre d'attaquants, noms vides ou en double, overwatch
    /// donné dans la situation d'une entrée (il est commun), erreurs du scénario de chaque entrée, précédées de son nom
    /// (une seule entrée invalide bloque le lancement).
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>(NameErrors(Attackers.Select(a => a.Name).ToList()));
        var scenarios = ToScenarios();
        for (int i = 0; i < scenarios.Count; i++)
        {
            if (Entries[i].Context.SnapShooting)
            {
                errors.Add($"{Label(i)} : le tir en overwatch est commun à toute la comparaison, pas propre à un attaquant.");
            }
            errors.AddRange(scenarios[i].Validate().Select(e => $"{Label(i)} : {e}"));
        }
        return errors;
    }

    /// <summary>Points à signaler qui n'empêchent pas de lancer : avertissements du scénario de chaque entrée, avec son nom.</summary>
    public IReadOnlyList<string> Warnings()
    {
        var scenarios = ToScenarios();
        return scenarios.SelectMany((scenario, i) => scenario.Warnings().Select(w => $"{Label(i)} : {w}")).ToList();
    }

    /// <exception cref="ArgumentException">Si la comparaison est invalide (<see cref="Validate"/>).</exception>
    public void EnsureValid()
    {
        var errors = Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException("Comparaison invalide :" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => "  - " + e)));
        }
    }

    /// <summary>
    /// Nombre d'entrées et noms (vides, en double sans tenir compte des majuscules ni des espaces autour) : règles
    /// communes à la définition et aux données d'une comparaison.
    /// </summary>
    internal static IEnumerable<string> NameErrors(IReadOnlyList<string> names)
    {
        if (names.Count < MinAttackers)
        {
            yield return $"Une comparaison demande au moins {MinAttackers} attaquants ({names.Count} actuellement).";
        }
        for (int i = 0; i < names.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(names[i]))
            {
                yield return $"Attaquant n° {i + 1} : le nom est obligatoire (il désigne l'entrée).";
            }
        }
        foreach (var duplicate in names
                     .Where(n => !string.IsNullOrWhiteSpace(n))
                     .GroupBy(n => n.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1))
        {
            yield return $"Plusieurs attaquants s'appellent '{duplicate.Key}' : chaque nom doit être unique.";
        }
    }

    private string Label(int index) =>
        string.IsNullOrWhiteSpace(Entries[index].Attacker.Name) ? $"Attaquant n° {index + 1}" : Entries[index].Attacker.Name;
}
