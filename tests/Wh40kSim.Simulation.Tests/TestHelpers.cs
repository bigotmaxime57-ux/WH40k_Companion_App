using Wh40kSim.Core.Model;

namespace Wh40kSim.Simulation.Tests;

/// <summary>
/// Enregistre les valeurs de progression dès leur arrivée. <see cref="Progress{T}"/> ne convient pas aux tests :
/// il transmet les valeurs plus tard, via le contexte de synchronisation.
/// </summary>
internal sealed class RecordingProgress : IProgress<double>
{
    private readonly List<double> _values = [];

    public IReadOnlyList<double> Values
    {
        get
        {
            lock (_values)
            {
                return _values.ToList();
            }
        }
    }

    public void Report(double value)
    {
        lock (_values)
        {
            _values.Add(value);
        }
    }
}

internal static class Build
{
    public static Scenario Scenario(WeaponProfile weapon, DefenderUnit defender, int models = 1) => new()
    {
        Attacker = new AttackerUnit { Weapons = [new WeaponGroup { ModelCount = models, Weapon = weapon }] },
        Defender = defender,
    };

    public static DefenderUnit Horde(int toughness = 4, int save = 7, int models = 100) => new()
    {
        Groups = [new ModelGroup { Name = "Horde", Count = models, Toughness = toughness, Wounds = 1, Save = save }],
    };

    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Wh40kSim.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Racine de la solution introuvable.");
    }
}
