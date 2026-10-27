using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Wh40kSim.Core.Dice;

namespace Wh40kSim.Core.Model;

/// <summary>
/// Mots-clés d'arme gérés par le moteur.
/// Les règles qui dépendent des mots-clés du défenseur (vrai Anti-X…) viendront en phase 5 ;
/// en attendant, Anti-X peut s'approximer avec <see cref="CriticalWoundOn"/>.
/// </summary>
public sealed record WeaponAbilities
{
    /// <summary>Touche automatiquement (aucun jet de touche, donc aucune touche critique).</summary>
    public bool Torrent { get; init; }

    /// <summary>Une touche critique blesse automatiquement.</summary>
    public bool LethalHits { get; init; }

    /// <summary>Touches supplémentaires générées par une touche critique (0 = pas de Sustained Hits).</summary>
    public DiceExpression SustainedHits { get; init; }

    /// <summary>Une blessure critique n'autorise aucune sauvegarde.</summary>
    public bool DevastatingWounds { get; init; }

    /// <summary>Relance les jets de blessure ratés.</summary>
    public bool TwinLinked { get; init; }

    /// <summary>+1 attaque par tranche de 5 figurines dans l'unité cible.</summary>
    public bool Blast { get; init; }

    /// <summary>Rapid Fire X : +X attaques à demi-portée (0 = absent).</summary>
    public int RapidFire { get; init; }

    /// <summary>Melta X : +X dégâts à demi-portée (0 = absent).</summary>
    public int Melta { get; init; }

    /// <summary>+1 pour toucher si l'unité est restée immobile.</summary>
    public bool Heavy { get; init; }

    /// <summary>+1 pour blesser si l'unité a chargé ce tour.</summary>
    public bool Lance { get; init; }

    /// <summary>Le couvert de la cible ne s'applique pas.</summary>
    public bool IgnoresCover { get; init; }

    /// <summary>
    /// Precision (24.28) : un groupe PERSONNAGE devient le groupe d'allocation courant jusqu'à la fin du pool
    /// ou sa destruction (cible visible supposée).
    /// </summary>
    public bool Precision { get; init; }

    /// <summary>Résultat non modifié à partir duquel une touche est critique (6 par défaut).</summary>
    public int CriticalHitOn { get; init; } = 6;

    /// <summary>Résultat non modifié à partir duquel une blessure est critique (6 par défaut, 4 pour un Anti-X 4+).</summary>
    public int CriticalWoundOn { get; init; } = 6;

    /// <summary>Description lisible, ex. "Lethal Hits, Rapid Fire 1".</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (Torrent) parts.Add("Torrent");
        if (LethalHits) parts.Add("Lethal Hits");
        if (!SustainedHits.IsZero) parts.Add($"Sustained Hits {SustainedHits}");
        if (DevastatingWounds) parts.Add("Devastating Wounds");
        if (TwinLinked) parts.Add("Twin-linked");
        if (Blast) parts.Add("Blast");
        if (RapidFire > 0) parts.Add($"Rapid Fire {RapidFire}");
        if (Melta > 0) parts.Add($"Melta {Melta}");
        if (Heavy) parts.Add("Heavy");
        if (Lance) parts.Add("Lance");
        if (IgnoresCover) parts.Add("Ignores Cover");
        if (Precision) parts.Add("Precision");
        if (CriticalHitOn != 6) parts.Add($"Touche critique sur {CriticalHitOn}+");
        if (CriticalWoundOn != 6) parts.Add($"Blessure critique sur {CriticalWoundOn}+");
        return string.Join(", ", parts);
    }

    // Un mot-clé = un nom (un ou plusieurs mots) suivi éventuellement d'une valeur ("1", "d3", "2d6+1", "5+")
    private static readonly Regex TokenPattern = new(
        @"^(?<name>[a-z]+(?: [a-z]+)*?)(?: (?<value>\d*d?\d+(?:\+\d+)?)\+?)?$",
        RegexOptions.Compiled);

    /// <summary>
    /// Lit une liste de mots-clés séparés par des virgules, en anglais ou en français.
    /// Exemple : "lethal hits, sustained hits d3, twin-linked, rapid fire 1, melta 2, anti 4+".
    /// </summary>
    public static WeaponAbilities Parse(string? text)
    {
        var result = new WeaponAbilities();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        foreach (string raw in text.Split(new[] { ',', ';' },StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = TokenPattern.Match(Normalize(raw));
            if (!match.Success)
            {
                throw new FormatException($"Mot-clé d'arme non reconnu : '{raw}'.");
            }

            string name = match.Groups["name"].Value;
            string value = match.Groups["value"].Value;

            result = name switch
            {
                "torrent" => result with { Torrent = true },
                "lethal hits" or "lethal" or "touches fatales" => result with { LethalHits = true },
                "sustained hits" or "sustained" or "touches soutenues" =>
                    result with { SustainedHits = value.Length == 0 ? DiceExpression.Constant(1) : DiceExpression.Parse(value) },
                "devastating wounds" or "devastating" or "dev" or "blessures devastatrices" => result with { DevastatingWounds = true },
                "twin linked" or "twin" or "jumele" or "jumelee" or "jumelees" => result with { TwinLinked = true },
                "blast" or "explosion" or "explosive" => result with { Blast = true },
                "rapid fire" or "rapid" or "tir rapide" => result with { RapidFire = RequireInt(value, raw, 1, 20) },
                "melta" or "fusion" => result with { Melta = RequireInt(value, raw, 1, 20) },
                "heavy" or "lourd" or "lourde" => result with { Heavy = true },
                "lance" => result with { Lance = true },
                "ignores cover" or "ignore cover" or "ignore couvert" or "ignore le couvert" => result with { IgnoresCover = true },
                "precision" => result with { Precision = true },
                "crit hit" or "critical hit" or "crit touche" => result with { CriticalHitOn = RequireInt(value, raw, 2, 6) },
                "crit wound" or "critical wound" or "anti" or "crit blessure" => result with { CriticalWoundOn = RequireInt(value, raw, 2, 6) },
                // "Anti-Infantry 4+" : en attendant les mots-clés du défenseur (phase 5), on suppose que la cible est concernée
                _ when name.StartsWith("anti ", StringComparison.Ordinal) => result with { CriticalWoundOn = RequireInt(value, raw, 2, 6) },
                _ => throw new FormatException($"Mot-clé d'arme non reconnu : '{raw}'."),
            };
        }

        return result;
    }

    private static int RequireInt(string value, string raw, int min, int max)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number < min || number > max)
        {
            throw new FormatException($"Valeur attendue entre {min} et {max} pour '{raw}'.");
        }
        return number;
    }

    // minuscules, sans accents, tirets/underscores -> espaces, espaces multiples réduits
    private static string Normalize(string text)
    {
        string decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            builder.Append(c is '-' or '_' ? ' ' : c);
        }
        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }
}
