using Wh40kSim.Simulation;
using Wh40kSim.UI.Forms;

namespace Wh40kSim.UI.Services;

/// <summary>
/// Scénario courant de l'application et ce qui l'accompagne (fichier, modifications, dernier résultat, réglages de la
/// simulation) : il survit au changement d'écran et sera partagé par le Quick sim et le Full sim. Service « scoped » :
/// une instance par fenêtre en desktop, par onglet en web.
/// </summary>
public sealed class ScenarioSession
{
    private bool _isRunning;

    /// <summary>Formulaire en cours d'édition.</summary>
    public ScenarioForm Form { get; set; } = NewForm();

    /// <summary>Nom du fichier ouvert ou enregistré, null pour un nouveau scénario.</summary>
    public string? FileName { get; set; }

    /// <summary>Le formulaire a été modifié depuis le dernier « Nouveau », « Ouvrir » ou « Enregistrer ».</summary>
    public bool IsDirty { get; set; }

    /// <summary>Résultat de la dernière simulation, effacé quand un autre scénario est chargé.</summary>
    public SimulationResult? Result { get; set; }

    public int Iterations { get; set; } = 1_000_000;

    public int? Seed { get; set; }

    /// <summary>Une simulation est en cours : on ne quitte pas l'écran tant qu'elle n'est pas finie ou annulée.</summary>
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning != value)
            {
                _isRunning = value;
                RunningChanged?.Invoke();
            }
        }
    }

    /// <summary>Prévient la barre de titre qu'une simulation commence ou se termine.</summary>
    public event Action? RunningChanged;

    /// <summary>Nouveau scénario : valeurs par défaut du Core, avec une arme et un groupe pour commencer.</summary>
    public static ScenarioForm NewForm()
    {
        var form = new ScenarioForm();
        form.Attacker.Weapons.Add(new WeaponForm());
        form.Defender.Groups.Add(new ModelGroupForm());
        return form;
    }
}
