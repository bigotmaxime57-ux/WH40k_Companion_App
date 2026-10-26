using System.Globalization;
using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;

namespace Wh40kSim.ConsoleApp;

/// <summary>Petites fonctions de saisie : valeur par défaut entre crochets, nouvelle question tant que la saisie est invalide.</summary>
internal static class ConsoleInput
{
    public static string ReadString(string label, string defaultValue) =>
        Ask(label, defaultValue, s => s);

    /// <summary>Entier ; accepte "3+" pour 3.</summary>
    public static int ReadInt(string label, int defaultValue, int min = int.MinValue, int max = int.MaxValue) =>
        Ask(label, defaultValue.ToString(CultureInfo.InvariantCulture), s => ParseInt(s, min, max));

    /// <summary>Seuil optionnel (invulnérable, FNP…) : vide = aucun.</summary>
    public static int? ReadOptionalThreshold(string label) =>
        Ask<int?>(label + " (vide si aucun)", "", s => s.Length == 0 ? null : ParseInt(s, 2, 6));

    /// <summary>Nom de fichier sans dossier (vide accepté).</summary>
    public static string ReadFileName(string label) =>
        Ask(label, "", s => s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || s is "." or ".."
            ? throw new FormatException("Nom de fichier invalide : un nom seul, sans dossier ni caractères spéciaux (\\ / : * ? \" < > |).")
            : s);

    public static bool ReadBool(string label, bool defaultValue) =>
        Ask(label + " (o/n)", defaultValue ? "o" : "n", s => s.ToLowerInvariant() switch
        {
            "o" or "oui" or "y" or "yes" => true,
            "n" or "non" or "no" => false,
            _ => throw new FormatException("Répondre o ou n."),
        });

    public static DiceExpression ReadDice(string label, DiceExpression defaultValue) =>
        Ask(label, defaultValue.ToString(), DiceExpression.Parse);

    public static WeaponAbilities ReadAbilities(string label) =>
        Ask(label, "", WeaponAbilities.Parse);

    public static RerollPolicy ReadReroll(string label) =>
        Ask(label + " (aucune / 1 / ratés / noncrit)", "aucune", s => s.ToLowerInvariant() switch
        {
            "aucune" or "non" or "n" or "0" => RerollPolicy.None,
            "1" or "1s" or "uns" => RerollPolicy.Ones,
            "rates" or "ratés" or "echecs" or "échecs" or "toutes" => RerollPolicy.Failed,
            "noncrit" or "non crit" or "crit" => RerollPolicy.NonCritical,
            _ => throw new FormatException("Valeurs possibles : aucune, 1, ratés, noncrit."),
        });

    public static void WriteTitle(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"=== {title} ===");
        Console.ResetColor();
    }

    public static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("  " + message);
        Console.ResetColor();
    }

    private static T Ask<T>(string label, string defaultText, Func<string, T> parse)
    {
        while (true)
        {
            Console.Write(defaultText.Length == 0 ? $"{label} : " : $"{label} [{defaultText}] : ");
            string? input = Console.ReadLine() ?? throw new EndOfStreamException("Saisie interrompue.");
            input = input.Trim();
            if (input.Length == 0)
            {
                input = defaultText;
            }

            try
            {
                return parse(input);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                WriteError(ex.Message);
            }
        }
    }

    private static int ParseInt(string text, int min, int max)
    {
        if (!int.TryParse(text.TrimEnd('+'), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
        {
            throw new FormatException($"Nombre entier attendu (saisi : '{text}').");
        }
        if (value < min || value > max)
        {
            throw new FormatException($"Valeur attendue entre {min} et {max}.");
        }
        return value;
    }
}
