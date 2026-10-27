namespace Wh40kSim.Core.Model;

/// <summary>Politique de relance. L'ordre des valeurs compte : une valeur plus haute relance davantage.</summary>
public enum RerollPolicy
{
    None = 0,

    /// <summary>Relance les 1.</summary>
    Ones = 1,

    /// <summary>Relance les échecs.</summary>
    Failed = 2,

    /// <summary>Relance tout ce qui n'est pas critique (« pêche aux 6 » pour Lethal/Sustained/Devastating).</summary>
    NonCritical = 3,
}

/// <summary>
/// Situation de l'attaque et bonus apportés par des sources externes (aura, stratagème…).
/// <para>Deux sortes de bonus, à ne pas confondre (02.02.01) :</para>
/// <list type="bullet">
/// <item><b>Modificateurs aux jets</b> (<see cref="HitModifier"/>, <see cref="WoundModifier"/>) : ajoutés au résultat
/// du dé, cumulés avec ceux du défenseur et plafonnés à ±1.</item>
/// <item><b>Modifications de caractéristiques</b> (<see cref="SkillModifier"/>, <see cref="AttacksModifier"/>,
/// <see cref="StrengthModifier"/>, <see cref="ApModifier"/>, <see cref="DamageModifier"/>) : changent le profil de
/// toutes les armes avant les jets, sans plafond, dans les bornes de chaque caractéristique.</item>
/// </list>
/// Convention : +1 = mieux pour l'attaquant (CT 3+ → 2+, PA-1 → PA-2). Une aptitude du défenseur qui dégrade une
/// caractéristique des attaquants (« PA dégradée de 1 ») s'exprime ici, en négatif : le résultat est le même.
/// </summary>
public sealed record AttackContext
{
    public bool HalfRange { get; init; }

    public bool RemainedStationary { get; init; }

    public bool Charged { get; init; }

    /// <summary>Modificateur aux jets de touche de l'attaquant (cumulé avec celui du défenseur, plafonné à ±1).</summary>
    public int HitModifier { get; init; }

    /// <summary>Modificateur aux jets de blessure de l'attaquant (cumulé avec celui du défenseur, plafonné à ±1).</summary>
    public int WoundModifier { get; init; }

    /// <summary>Amélioration de la CT / CC (+1 : 3+ devient 2+), cumulée avec le couvert ; résultat borné entre 2+ et 6+.</summary>
    public int SkillModifier { get; init; }

    /// <summary>Attaques en plus par figurine (A au moins 1).</summary>
    public int AttacksModifier { get; init; }

    /// <summary>Force en plus (F au moins 1).</summary>
    public int StrengthModifier { get; init; }

    /// <summary>Amélioration de la PA (+1 : PA-1 devient PA-2) ; la PA ne dépasse jamais 0.</summary>
    public int ApModifier { get; init; }

    /// <summary>Dégâts en plus par attaque (D au moins 1), avant Melta et la réduction de dégâts du défenseur.</summary>
    public int DamageModifier { get; init; }

    public RerollPolicy HitReroll { get; init; }

    public RerollPolicy WoundReroll { get; init; }

    /// <summary>
    /// Nom du groupe PERSONNAGE visé par les armes Precision (null = le premier PERSONNAGE encore en jeu,
    /// dans l'ordre d'allocation).
    /// </summary>
    public string? PrecisionTarget { get; init; }
}
