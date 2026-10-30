namespace Wh40kSim.UI.Components;

/// <summary>
/// Progression de la simulation en cours (0 à 1), suivie par la barre (<see cref="SimulationProgressBar"/>) : seule la
/// barre se redessine à chaque changement, pas la page entière.
/// </summary>
public sealed class ProgressState
{
    public double Value { get; private set; }

    public event Action? Changed;

    public void Report(double value)
    {
        Value = value;
        Changed?.Invoke();
    }
}
