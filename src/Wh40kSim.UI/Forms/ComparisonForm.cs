using Wh40kSim.Core.Model;
using Wh40kSim.Simulation.Comparisons;

namespace Wh40kSim.UI.Forms;

/// <summary>Version modifiable d'une <see cref="ComparisonEntry"/> : un attaquant et sa propre situation.</summary>
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
    }

    public AttackerForm Attacker { get; set; }

    /// <summary>Situation propre à cet attaquant (le tir en overwatch, commun, est sur la comparaison).</summary>
    public ContextForm Context { get; set; }

    /// <exception cref="FormatException">Si une expression de dés saisie est invalide.</exception>
    public ComparisonEntry ToComparisonEntry() => new()
    {
        Attacker = Attacker.ToAttackerUnit(),
        Context = Context.ToAttackContext() with { SnapShooting = false },
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
