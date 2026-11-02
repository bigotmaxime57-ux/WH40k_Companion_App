namespace Wh40kSim.Simulation;

/// <summary>
/// Nombre de simulations de chaque ronde éliminatoire, la dernière (la finale) avec le nombre choisi par le joueur.
/// Seuils fixés d'après un banc d'essai (phase 5, étape 2) : les rondes sont rentables dès 4 entrées, trois rondes plus
/// rapides que deux dès 10.
/// </summary>
public static class RoundPlan
{
    /// <summary>Nombre d'entrées à partir duquel on fait 2 rondes (en dessous, toutes sont dans le top 3).</summary>
    public const int TwoRoundsFrom = 4;

    /// <summary>Nombre d'entrées à partir duquel on fait 3 rondes.</summary>
    public const int ThreeRoundsFrom = 10;

    /// <summary>Une ronde préliminaire plus courte que cela serait trop imprécise : elle est sautée.</summary>
    public const int MinRoundIterations = 10_000;

    /// <summary>
    /// Simulations de chaque ronde, de la première à la finale (<paramref name="finalIterations"/>) : M/100, M/10, M,
    /// selon le nombre d'entrées, sans les rondes préliminaires de moins de <see cref="MinRoundIterations"/>.
    /// </summary>
    public static IReadOnlyList<int> For(int entries, int finalIterations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(entries, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(finalIterations, 1);
        int rounds = entries >= ThreeRoundsFrom ? 3 : entries >= TwoRoundsFrom ? 2 : 1;
        var plan = new List<int>();
        for (int divisor = rounds == 3 ? 100 : rounds == 2 ? 10 : 1; divisor > 1; divisor /= 10)
        {
            if (finalIterations / divisor >= MinRoundIterations)
            {
                plan.Add(finalIterations / divisor);
            }
        }
        plan.Add(finalIterations);
        return plan;
    }
}
