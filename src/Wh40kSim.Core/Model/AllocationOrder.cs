namespace Wh40kSim.Core.Model;

/// <summary>
/// Règle 05.03 : dans l'ordre d'allocation, les groupes PERSONNAGE viennent toujours après tous les autres groupes
/// (les gardes du corps). Fonctions sur une liste de « est un PERSONNAGE », dans l'ordre d'allocation, pour servir aussi
/// bien aux <see cref="ModelGroup"/> du Core qu'aux formulaires de l'interface.
/// </summary>
public static class AllocationOrder
{
    /// <summary>
    /// Première infraction : un PERSONNAGE (indice <c>Character</c>) placé avant un garde du corps (indice
    /// <c>Bodyguard</c>), ou null si l'ordre respecte la règle.
    /// </summary>
    public static (int Character, int Bodyguard)? FindViolation(IReadOnlyList<bool> isCharacter)
    {
        int firstCharacter = -1;
        for (int g = 0; g < isCharacter.Count; g++)
        {
            if (isCharacter[g] && firstCharacter < 0)
            {
                firstCharacter = g;
            }
            else if (!isCharacter[g] && firstCharacter >= 0)
            {
                return (firstCharacter, g);
            }
        }
        return null;
    }

    public static bool IsValid(IReadOnlyList<bool> isCharacter) => FindViolation(isCharacter) is null;

    /// <summary>
    /// Remet les PERSONNAGES après les gardes du corps, sans changer l'ordre au sein de chaque catégorie.
    /// </summary>
    public static List<T> Arrange<T>(IEnumerable<T> groups, Func<T, bool> isCharacter)
    {
        var list = groups.ToList();
        return [.. list.Where(g => !isCharacter(g)), .. list.Where(isCharacter)];
    }
}
