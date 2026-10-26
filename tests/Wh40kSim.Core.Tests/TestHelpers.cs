using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Tests;

/// <summary>Dés dont les résultats sont imposés, dans l'ordre où le moteur les lance.</summary>
internal sealed class ScriptedDiceRoller : IDiceRoller
{
    private readonly int[] _rolls;
    private int _index;

    public ScriptedDiceRoller(params int[] rolls) => _rolls = rolls;

    public int Remaining => _rolls.Length - _index;

    public int Roll(int sides)
    {
        if (_index >= _rolls.Length)
        {
            throw new InvalidOperationException("Le moteur a demandé plus de dés que prévu par le test.");
        }
        return _rolls[_index++];
    }
}

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
    public static Scenario Scenario(WeaponProfile weapon, DefenderUnit defender, int models = 1, AttackContext? context = null) => new()
    {
        Attacker = new AttackerUnit { Weapons = [new WeaponGroup { ModelCount = models, Weapon = weapon }] },
        Defender = defender,
        Context = context ?? new AttackContext(),
    };

    public static DefenderUnit Horde(int toughness = 4, int save = 7, int models = 100) => new()
    {
        Groups = [new ModelGroup { Name = "Horde", Count = models, Toughness = toughness, Wounds = 1, Save = save }],
    };
}
