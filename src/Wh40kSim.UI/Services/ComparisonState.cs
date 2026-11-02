using Wh40kSim.Simulation.Comparisons;
using Wh40kSim.UI.Forms;

namespace Wh40kSim.UI.Services;

/// <summary>
/// État du comparateur dans la session : sa comparaison (défenseur et situation propres, décision de l'utilisateur),
/// son fichier, ses modifications, ses réglages et son dernier résultat. Indépendant du scénario du Quick sim et du
/// Full sim ; conservé en changeant d'écran.
/// </summary>
public sealed class ComparisonState
{
    /// <summary>Comparaison en cours d'édition ; null tant que le comparateur n'a pas été ouvert (elle part alors du scénario courant).</summary>
    public ComparisonForm? Form { get; set; }

    /// <summary>Nom du fichier de comparaison ouvert ou enregistré, null pour une nouvelle comparaison.</summary>
    public string? FileName { get; set; }

    /// <summary>La comparaison a été modifiée depuis le dernier « Nouveau », « Ouvrir » ou « Enregistrer ».</summary>
    public bool IsDirty { get; set; }

    /// <summary>Le défenseur a été modifié à la main depuis son dernier chargement ou enregistrement.</summary>
    public bool DefenderDirty { get; set; }

    /// <summary>Attaquants modifiés à la main depuis leur dernier chargement ou enregistrement (confirmation avant de les remplacer).</summary>
    public HashSet<AttackerForm> DirtyAttackers { get; } = [];

    /// <summary>Nombre de simulations de la finale.</summary>
    public int Iterations { get; set; } = 1_000_000;

    public int? Seed { get; set; }

    /// <summary>Résultat de la dernière comparaison, effacé quand une autre comparaison est chargée.</summary>
    public ComparisonResult? Result { get; set; }

    /// <summary>
    /// Le dernier résultat a été calculé sur une autre comparaison que celle saisie (modifiée depuis, ou illisible) ;
    /// faux sans résultat.
    /// </summary>
    public bool IsStale
    {
        get
        {
            if (Result is null || Form is null)
            {
                return false;
            }
            try
            {
                return ComparisonJson.Serialize(Form.ToComparison()) != ComparisonJson.Serialize(Result.Comparison);
            }
            catch (FormatException)
            {
                return true;
            }
        }
    }
}
