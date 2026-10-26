namespace Wh40kSim.Core.Engine;

public static class WoundTable
{
    /// <summary>Résultat nécessaire pour blesser (Force contre Endurance).</summary>
    public static int RequiredRoll(int strength, int toughness)
    {
        if (strength >= 2 * toughness) return 2;
        if (strength > toughness) return 3;
        if (strength == toughness) return 4;
        if (strength * 2 <= toughness) return 6;
        return 5;
    }
}
