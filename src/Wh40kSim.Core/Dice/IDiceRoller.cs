namespace Wh40kSim.Core.Dice;

/// <summary>
/// Source de jets de dés. Abstraite pour pouvoir injecter des dés « scriptés » dans les tests
/// et ainsi vérifier pas à pas la séquence d'attaque.
/// </summary>
public interface IDiceRoller
{
    /// <summary>Lance un dé à <paramref name="sides"/> faces et renvoie une valeur entre 1 et <paramref name="sides"/>.</summary>
    int Roll(int sides);
}

public static class DiceRollerExtensions
{
    public static int D6(this IDiceRoller dice) => dice.Roll(6);
}
