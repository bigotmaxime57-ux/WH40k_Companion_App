using System.Globalization;
using System.Text.RegularExpressions;

namespace Wh40kSim.Core.Dice;

/// <summary>
/// Valeur éventuellement aléatoire, telle qu'écrite sur une fiche : "3", "D6", "2D3", "D6+2", "D3-1".
/// <para><see cref="DiceCount"/> = 0 signifie une valeur fixe égale à <see cref="Modifier"/>.</para>
/// </summary>
public readonly record struct DiceExpression(int DiceCount, int Sides, int Modifier)
{
    // [n]D<faces> optionnel, puis [+|-]<nombre> optionnel
    private static readonly Regex Pattern = new(
        @"^\s*(?:(?<count>\d*)\s*[dD]\s*(?<sides>\d+))?\s*(?:(?<sign>[+-])?\s*(?<mod>\d+))?\s*$",
        RegexOptions.Compiled);

    public static DiceExpression Constant(int value) => new(0, 0, value);

    public bool IsConstant => DiceCount == 0;

    public bool IsZero => DiceCount == 0 && Modifier == 0;

    /// <summary>Valeur moyenne (ex. D6 = 3,5 ; D3+1 = 3).</summary>
    public double Average => DiceCount * (Sides + 1) / 2.0 + Modifier;

    /// <summary>Lance l'expression. Le résultat ne descend jamais sous 0.</summary>
    public int Roll(IDiceRoller dice)
    {
        int total = Modifier;
        for (int i = 0; i < DiceCount; i++)
        {
            total += dice.Roll(Sides);
        }
        return Math.Max(0, total);
    }

    public static DiceExpression Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var match = Pattern.Match(text);
        bool hasDice = match.Success && match.Groups["sides"].Success;
        bool hasNumber = match.Success && match.Groups["mod"].Success;

        // Rejette "" et "D6 2" (nombre après un dé sans signe)
        if (!match.Success || (!hasDice && !hasNumber) || (hasDice && hasNumber && !match.Groups["sign"].Success))
        {
            throw new FormatException($"Expression de dés invalide : '{text}' (exemples valides : 2, D6, 2D3, D6+1).");
        }

        int count = 0, sides = 0;
        if (hasDice)
        {
            sides = int.Parse(match.Groups["sides"].Value, CultureInfo.InvariantCulture);
            string countText = match.Groups["count"].Value;
            count = countText.Length == 0 ? 1 : int.Parse(countText, CultureInfo.InvariantCulture);
            if (sides < 2 || count < 1)
            {
                throw new FormatException($"Expression de dés invalide : '{text}'.");
            }
        }

        int modifier = 0;
        if (hasNumber)
        {
            modifier = int.Parse(match.Groups["mod"].Value, CultureInfo.InvariantCulture);
            if (match.Groups["sign"].Value == "-")
            {
                modifier = -modifier;
            }
        }

        return new DiceExpression(count, sides, modifier);
    }

    public static bool TryParse(string? text, out DiceExpression result)
    {
        result = default;
        if (text is null)
        {
            return false;
        }

        try
        {
            result = Parse(text);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static implicit operator DiceExpression(int value) => Constant(value);

    public override string ToString()
    {
        if (IsConstant)
        {
            return Modifier.ToString(CultureInfo.InvariantCulture);
        }

        string dice = (DiceCount == 1 ? "" : DiceCount.ToString(CultureInfo.InvariantCulture))
                      + "D" + Sides.ToString(CultureInfo.InvariantCulture);
        return Modifier switch
        {
            0 => dice,
            > 0 => $"{dice}+{Modifier}",
            _ => $"{dice}{Modifier}",
        };
    }
}
