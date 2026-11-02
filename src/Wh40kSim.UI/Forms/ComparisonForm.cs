using Wh40kSim.Core.Model;
using Wh40kSim.Simulation.Comparisons;

namespace Wh40kSim.UI.Forms;

/// <summary>Version modifiable d'une <see cref="Comparison"/> : défenseur et situation communs, attaquants comparés.</summary>
public sealed class ComparisonForm
{
    /// <summary>Formulaire vierge : défenseur et situation par défaut du Core, aucun attaquant.</summary>
    public ComparisonForm()
        : this(new Comparison { Defender = new DefenderUnit(), Attackers = [] })
    {
    }

    public ComparisonForm(Comparison comparison)
    {
        Defender = new DefenderForm(comparison.Defender);
        Context = new ContextForm(comparison.Context);
        Attackers = comparison.Attackers.Select(a => new AttackerForm(a)).ToList();
    }

    public DefenderForm Defender { get; set; }

    public ContextForm Context { get; set; }

    /// <summary>Attaquants comparés, dans l'ordre de saisie.</summary>
    public List<AttackerForm> Attackers { get; set; }

    /// <exception cref="FormatException">Si une expression de dés saisie est invalide.</exception>
    public Comparison ToComparison() => new()
    {
        Defender = Defender.ToDefenderUnit(),
        Context = Context.ToAttackContext(),
        Attackers = Attackers.Select(a => a.ToAttackerUnit()).ToList(),
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
