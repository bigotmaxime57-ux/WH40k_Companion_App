using System.Text.Json.Serialization;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Simulation.Comparisons;

/// <summary>
/// Profils de rechange d'une arme de l'attaquant (comparateur seulement) : le profil saisi dans l'attaquant reste le
/// premier choix, ceux-ci s'y ajoutent ; tous gardent le nombre de figurines de l'arme.
/// </summary>
public sealed record WeaponAlternatives
{
    /// <summary>Numéro de l'arme dans la liste de l'attaquant (à partir de 0).</summary>
    public required int Weapon { get; init; }

    public required IReadOnlyList<WeaponProfile> Profiles { get; init; }
}

/// <summary>
/// Un attaquant comparé, sa propre situation (a chargé, immobile, demi-portée, cible de Precision) et les profils de
/// rechange de ses armes.
/// </summary>
public sealed record ComparisonEntry
{
    public required AttackerUnit Attacker { get; init; }

    /// <summary>Situation propre à cet attaquant ; le tir en overwatch, commun, est porté par la comparaison.</summary>
    public AttackContext Context { get; init; } = new();

    /// <summary>Profils de rechange de certaines armes (aucun : une seule variante, l'attaquant tel quel).</summary>
    public IReadOnlyList<WeaponAlternatives> Alternatives { get; init; } = [];

    /// <summary>
    /// Toutes les combinaisons de profils, chacune un attaquant ordinaire : la première est l'attaquant tel quel. Nom de
    /// chaque variante : celui de l'attaquant, suivi des profils choisis pour les armes à plusieurs profils
    /// (« Hellblasters · Plasma (surchargé) »).
    /// </summary>
    /// <remarks>Numéros d'arme hors de la liste ignorés : la validation les signale.</remarks>
    public IReadOnlyList<(AttackerUnit Attacker, IReadOnlyList<ProfileChoice> Choices)> Variants()
    {
        var options = Alternatives
            .Where(a => a.Weapon >= 0 && a.Weapon < Attacker.Weapons.Count && a.Profiles.Count > 0)
            .GroupBy(a => a.Weapon)
            .Select(g => (Weapon: g.Key, Profiles: (IReadOnlyList<WeaponProfile>)[Attacker.Weapons[g.Key].Weapon, .. g.SelectMany(a => a.Profiles)]))
            .OrderBy(o => o.Weapon)
            .ToList();

        IEnumerable<IReadOnlyList<int>> combinations = [[]];
        foreach (var option in options)
        {
            combinations = combinations.SelectMany(c => Enumerable.Range(0, option.Profiles.Count).Select(p => (IReadOnlyList<int>)[.. c, p])).ToList();
        }

        return combinations.Select(combination =>
        {
            var weapons = Attacker.Weapons.ToList();
            var choices = new List<ProfileChoice>();
            for (int o = 0; o < options.Count; o++)
            {
                var (weapon, profiles) = options[o];
                var profile = profiles[combination[o]];
                choices.Add(new ProfileChoice(weapon, Attacker.Weapons[weapon].Weapon.Name, profile.Name));
                weapons[weapon] = weapons[weapon] with { Weapon = profile };
            }
            string name = options.Count == 0 ? Attacker.Name : $"{Attacker.Name} · {string.Join(", ", choices.Select(c => c.Profile))}";
            return (Attacker with { Name = name, Weapons = weapons }, (IReadOnlyList<ProfileChoice>)choices);
        }).ToList();
    }
}

/// <summary>Profil retenu pour une arme à plusieurs profils : numéro de l'arme, nom de son premier profil, profil choisi.</summary>
public sealed record ProfileChoice(int Weapon, string WeaponName, string Profile);

/// <summary>
/// Une variante à simuler : son entrée (numéro dans <see cref="Comparison.Entries"/>), son scénario et les profils choisis.
/// </summary>
public sealed record ComparisonVariant(int Entry, Scenario Scenario, IReadOnlyList<ProfileChoice> Choices)
{
    public string Name => Scenario.Attacker.Name;
}

/// <summary>
/// Comparaison de plusieurs attaquants contre un même défenseur : qui fait le mieux ? Chaque attaquant forme une
/// entrée, désignée par son nom (unique), avec sa propre situation et d'éventuels profils de rechange (chaque
/// combinaison de profils est une variante à part entière) ; le tir en overwatch est commun à tous (décision de
/// l'utilisateur). Immuable ; JSON : <see cref="ComparisonJson"/>.
/// </summary>
public sealed record Comparison
{
    /// <summary>Nombre minimal de variantes à comparer (pas de maximum) : deux attaquants, ou un seul avec des profils de rechange.</summary>
    public const int MinAttackers = 2;

    public required DefenderUnit Defender { get; init; }

    /// <summary>Tir en overwatch (snap shooting), pour tous les attaquants.</summary>
    public bool SnapShooting { get; init; }

    /// <summary>Attaquants comparés, leur situation et leurs profils de rechange, dans l'ordre de saisie.</summary>
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

    /// <summary>
    /// Toutes les variantes, dans l'ordre de saisie des attaquants puis des combinaisons : chacune avec sa situation et le
    /// tir en overwatch commun. Ce sont elles qui s'affrontent.
    /// </summary>
    public IReadOnlyList<ComparisonVariant> Variants() =>
        Entries.SelectMany((entry, e) => entry.Variants().Select(v => new ComparisonVariant(
            e,
            new Scenario { Attacker = v.Attacker, Defender = Defender, Context = entry.Context with { SnapShooting = SnapShooting } },
            v.Choices))).ToList();

    /// <summary>Un scénario par variante (voir <see cref="Variants"/>).</summary>
    public IReadOnlyList<Scenario> ToScenarios() => Variants().Select(v => v.Scenario).ToList();

    /// <summary>
    /// Incohérences (vide si la comparaison peut être lancée) : noms d'attaquant vides ou en double, profils de rechange
    /// (arme inexistante, aucun profil, noms en double), overwatch donné dans la situation d'une entrée (il est commun),
    /// au moins deux variantes de noms distincts, erreurs du scénario de chaque variante, précédées de son nom (une seule
    /// variante invalide bloque le lancement).
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>(NameErrors(Attackers.Select(a => a.Name).ToList(), countCheck: false));
        for (int i = 0; i < Entries.Count; i++)
        {
            var entry = Entries[i];
            if (entry.Context.SnapShooting)
            {
                errors.Add($"{Label(i)} : le tir en overwatch est commun à toute la comparaison, pas propre à un attaquant.");
            }
            foreach (var alternatives in entry.Alternatives)
            {
                if (alternatives.Weapon < 0 || alternatives.Weapon >= entry.Attacker.Weapons.Count)
                {
                    errors.Add($"{Label(i)} : profils de rechange pour une arme qui n'existe pas (n° {alternatives.Weapon + 1}).");
                    continue;
                }
                string weapon = entry.Attacker.Weapons[alternatives.Weapon].Weapon.Name;
                if (alternatives.Profiles.Count == 0)
                {
                    errors.Add($"{Label(i)} : l'arme '{weapon}' annonce des profils de rechange mais n'en a aucun.");
                }
                var names = alternatives.Profiles.Select(p => p.Name).Prepend(weapon);
                foreach (var duplicate in names.GroupBy(n => n.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                {
                    errors.Add($"{Label(i)} : plusieurs profils de l'arme '{weapon}' s'appellent '{duplicate.Key}' (le nom désigne le profil dans les résultats).");
                }
            }
            if (entry.Alternatives.GroupBy(a => a.Weapon).Any(g => g.Count() > 1))
            {
                errors.Add($"{Label(i)} : les profils de rechange d'une arme sont donnés en plusieurs fois.");
            }
        }

        var variants = Variants();
        errors.AddRange(NameErrors(variants.Select(v => v.Name).ToList(), countCheck: true, emptyCheck: false)
            .Where(e => !errors.Contains(e)));
        foreach (var variant in variants)
        {
            errors.AddRange(variant.Scenario.Validate().Select(e => $"{(string.IsNullOrWhiteSpace(variant.Name) ? Label(variant.Entry) : variant.Name)} : {e}"));
        }
        return errors.Distinct().ToList();
    }

    /// <summary>Points à signaler qui n'empêchent pas de lancer : avertissements du scénario de chaque variante, avec son nom.</summary>
    public IReadOnlyList<string> Warnings() =>
        Variants().SelectMany(v => v.Scenario.Warnings().Select(w => $"{v.Name} : {w}")).Distinct().ToList();

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
    /// Noms (vides, en double sans tenir compte des majuscules ni des espaces autour) et, si demandé, nombre minimal
    /// d'entrées : règles communes à la définition et aux données d'une comparaison.
    /// </summary>
    internal static IEnumerable<string> NameErrors(IReadOnlyList<string> names, bool countCheck = true, bool emptyCheck = true)
    {
        if (countCheck && names.Count < MinAttackers)
        {
            yield return $"Une comparaison demande au moins {MinAttackers} attaquants, ou un attaquant avec des profils de rechange ({names.Count} variante(s) actuellement).";
        }
        for (int i = 0; i < names.Count; i++)
        {
            if (emptyCheck && string.IsNullOrWhiteSpace(names[i]))
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
