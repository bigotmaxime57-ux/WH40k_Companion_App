using Wh40kSim.Core.Model;
using Wh40kSim.Simulation.Comparisons;

namespace Wh40kSim.UI.Forms;

/// <summary>
/// Version modifiable d'une <see cref="ComparisonEntry"/> : un attaquant, sa propre situation et les profils de rechange
/// de ses armes.
/// </summary>
public sealed class ComparisonEntryForm
{
    public ComparisonEntryForm(AttackerForm attacker, ContextForm? context = null)
    {
        Attacker = attacker;
        Context = context ?? new ContextForm();
    }

    public ComparisonEntryForm(ComparisonEntry entry)
        : this(new AttackerForm(entry.Attacker), new ContextForm(entry.Context))
    {
        foreach (var alternatives in entry.Alternatives.Where(a => a.Weapon >= 0 && a.Weapon < Attacker.Weapons.Count))
        {
            var weapon = Attacker.Weapons[alternatives.Weapon];
            Alternatives[weapon] = alternatives.Profiles
                .Select(p => new WeaponForm(new WeaponGroup { ModelCount = weapon.ModelCount, Weapon = p }))
                .ToList();
        }
    }

    public AttackerForm Attacker { get; set; }

    /// <summary>Situation propre à cet attaquant (le tir en overwatch, commun, est sur la comparaison).</summary>
    public ContextForm Context { get; set; }

    /// <summary>
    /// Profils de rechange, par arme de l'attaquant (la clé est l'arme de <see cref="AttackerForm.Weapons"/>, qui reste
    /// le premier profil) ; une arme supprimée de l'attaquant est ignorée.
    /// </summary>
    public Dictionary<WeaponForm, List<WeaponForm>> Alternatives { get; } = [];

    /// <summary>Nombre de variantes (combinaisons de profils) : 1 sans profil de rechange.</summary>
    public int VariantCount => Attacker.Weapons.Aggregate(1, (count, weapon) => count * (1 + ProfilesOf(weapon).Count));

    /// <summary>Profils de rechange d'une arme (vide s'il n'y en a pas).</summary>
    public IReadOnlyList<WeaponForm> ProfilesOf(WeaponForm weapon) => Alternatives.GetValueOrDefault(weapon) ?? [];

    /// <summary>Copie indépendante : attaquant, situation et profils de rechange.</summary>
    /// <exception cref="FormatException">Si une expression de dés saisie est invalide.</exception>
    public ComparisonEntryForm Clone() => new(ToComparisonEntry());

    /// <exception cref="FormatException">Si une expression de dés saisie est invalide.</exception>
    public ComparisonEntry ToComparisonEntry() => new()
    {
        Attacker = Attacker.ToAttackerUnit(),
        Context = Context.ToAttackContext() with { SnapShooting = false },
        Alternatives = Attacker.Weapons
            .Select((weapon, index) => (index, profiles: ProfilesOf(weapon)))
            .Where(w => w.profiles.Count > 0)
            .Select(w => new WeaponAlternatives { Weapon = w.index, Profiles = w.profiles.Select(p => p.ToWeaponGroup().Weapon).ToList() })
            .ToList(),
    };
}

/// <summary>
/// Version modifiable d'une <see cref="Comparison"/> : défenseur commun, tir en overwatch commun, attaquants comparés
/// avec leur situation.
/// </summary>
public sealed class ComparisonForm
{
    /// <summary>Formulaire vierge : défenseur par défaut du Core, aucun attaquant.</summary>
    public ComparisonForm()
        : this(new Comparison { Defender = new DefenderUnit(), Entries = [] })
    {
    }

    public ComparisonForm(Comparison comparison)
    {
        Defender = new DefenderForm(comparison.Defender);
        SnapShooting = comparison.SnapShooting;
        Entries = comparison.Entries.Select(e => new ComparisonEntryForm(e)).ToList();
    }

    public DefenderForm Defender { get; set; }

    /// <summary>Tir en overwatch, pour tous les attaquants.</summary>
    public bool SnapShooting { get; set; }

    /// <summary>Attaquants comparés et leur situation, dans l'ordre de saisie.</summary>
    public List<ComparisonEntryForm> Entries { get; set; }

    /// <summary>Attaquants comparés, dans l'ordre de saisie.</summary>
    public IEnumerable<AttackerForm> Attackers => Entries.Select(e => e.Attacker);

    /// <exception cref="FormatException">Si une expression de dés saisie est invalide.</exception>
    public Comparison ToComparison() => new()
    {
        Defender = Defender.ToDefenderUnit(),
        SnapShooting = SnapShooting,
        Entries = Entries.Select(e => e.ToComparisonEntry()).ToList(),
    };

    /// <summary>
    /// Nom libre pour un nouvel attaquant : <paramref name="name"/> s'il n'est pas pris (sans tenir compte des majuscules
    /// ni des espaces autour), sinon « nom (2) », « nom (3) »… ; <paramref name="except"/> (l'attaquant renommé) ne compte pas.
    /// </summary>
    public string UniqueName(string name, AttackerForm? except = null)
    {
        string trimmed = name.Trim();
        var taken = Attackers.Where(a => !ReferenceEquals(a, except)).Select(a => a.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(trimmed))
        {
            return trimmed;
        }
        for (int suffix = 2; ; suffix++)
        {
            string candidate = $"{trimmed} ({suffix})";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
