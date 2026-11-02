namespace Wh40kSim.UI.Components;

/// <summary>Noms de fichier proposés à l'enregistrement.</summary>
public static class FileNames
{
    /// <summary>
    /// Caractères retirés des noms de fichier proposés : ceux que Windows interdit, quel que soit le système (Linux et le
    /// navigateur en acceptent davantage, mais les fichiers doivent pouvoir être ouverts partout).
    /// </summary>
    private static readonly char[] ForbiddenChars =
        ['<', '>', ':', '"', '/', '\\', '|', '?', '*', .. Enumerable.Range(0, 32).Select(c => (char)c)];

    /// <summary>Nom proposé pour une unité : son nom sans les caractères interdits, à défaut un nom générique.</summary>
    public static string ForUnit(string unitName, string fallback)
    {
        string name = string.Concat(unitName.Split(ForbiddenChars)).Trim();
        return (name.Length > 0 ? name : fallback) + ".json";
    }
}
