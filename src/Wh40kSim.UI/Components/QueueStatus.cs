namespace Wh40kSim.UI.Components;

/// <summary>Attente d'un lancement dans la file du serveur : position (1 = le prochain), suivie par la fenêtre d'attente.</summary>
public sealed class QueueStatus
{
    private int _position;

    /// <summary>Calculs avant celui-ci (celui en cours compris).</summary>
    public int Position
    {
        get => _position;
        set
        {
            _position = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Le calcul a commencé (ou le lancement est fini) : plus de fenêtre d'attente.</summary>
    public bool Started { get; set; }

    /// <summary>La fenêtre est en train de s'ouvrir.</summary>
    public bool Opening { get; set; }

    public event Action? Changed;
}
