namespace Wh40kSim.Core.Model;

/// <summary>Politique de relance. L'ordre des valeurs compte : une valeur plus haute relance davantage.</summary>
public enum RerollPolicy
{
    None = 0,

    /// <summary>Relance les 1.</summary>
    Ones = 1,

    /// <summary>
    /// Relance complète : on peut relancer n'importe quel jet. Relance les échecs, ou tout ce qui n'est pas critique
    /// pour une arme en « pêche aux 6 » (<see cref="WeaponProfile.FishForCriticalHits"/>,
    /// <see cref="WeaponProfile.FishForCriticalWounds"/>).
    /// </summary>
    Failed = 2,
}

/// <summary>
/// Boosts de l'attaquant (auras, stratagèmes, aptitudes…), pour toutes ses armes ; cumulés avec ceux du défenseur
/// (<see cref="DefenderBoosts"/>). Convention : +1 = mieux pour l'attaquant. Deux sortes, à ne pas confondre (02.02.01) :
/// <list type="bullet">
/// <item><b>modifications de caractéristiques</b> (<see cref="Skill"/>, <see cref="Strength"/>, <see cref="Attacks"/>,
/// <see cref="Ap"/>, <see cref="Damage"/>) : changent le profil avant les jets, sans plafond, dans les bornes de chaque
/// caractéristique ;</item>
/// <item><b>modificateurs aux jets</b> (<see cref="HitRoll"/>, <see cref="WoundRoll"/>) : ajoutés au résultat du dé,
/// cumulés avec ceux du défenseur et plafonnés à ±1, après les relances.</item>
/// </list>
/// </summary>
public sealed record AttackerBoosts
{
    /// <summary>CT / CC (+1 : 3+ devient 2+), cumulée avec le défenseur et le couvert ; résultat borné entre 2+ et 6+.</summary>
    public int Skill { get; init; }

    /// <summary>Modificateur au jet de touche (plafonné à ±1 au total).</summary>
    public int HitRoll { get; init; }

    public RerollPolicy HitReroll { get; init; }

    /// <summary>Force en plus (F au moins 1).</summary>
    public int Strength { get; init; }

    /// <summary>Modificateur au jet de blessure (plafonné à ±1 au total).</summary>
    public int WoundRoll { get; init; }

    public RerollPolicy WoundReroll { get; init; }

    /// <summary>Attaques en plus par figurine (A au moins 1).</summary>
    public int Attacks { get; init; }

    /// <summary>PA (+1 : PA-1 devient PA-2), cumulée avec le défenseur ; la PA ne dépasse jamais 0.</summary>
    public int Ap { get; init; }

    /// <summary>Dégâts en plus par attaque (D au moins 1), avant Melta et les boosts / la réduction de dégâts du défenseur.</summary>
    public int Damage { get; init; }
}

/// <summary>
/// Boosts du défenseur contre les attaques qui le visent ; cumulés avec ceux de l'attaquant (<see cref="AttackerBoosts"/>).
/// Exprimés du point de vue de l'attaquant : -1 = moins bon pour lui (CT 3+ → 4+, PA-2 → PA-1, un dégât de moins).
/// </summary>
public sealed record DefenderBoosts
{
    /// <summary>Règle 11e édition : le couvert dégrade de 1 la CT des armes de tir (sauf Ignores Cover).</summary>
    public bool InCover { get; init; }

    /// <summary>CT / CC des attaquants (-1 : 3+ devient 4+), cumulée avec l'attaquant et le couvert.</summary>
    public int Skill { get; init; }

    /// <summary>Modificateur aux jets de touche des attaquants (ex. -1 pour Stealth ; plafonné à ±1 au total).</summary>
    public int HitRoll { get; init; }

    /// <summary>Modificateur aux jets de blessure des attaquants (plafonné à ±1 au total).</summary>
    public int WoundRoll { get; init; }

    /// <summary>PA des attaques reçues (-1 : PA-2 devient PA-1) ; la PA ne dépasse jamais 0.</summary>
    public int Ap { get; init; }

    /// <summary>
    /// Dégâts des attaques reçues (-1 : un dégât de moins), appliqués comme la réduction de dégâts des groupes : après
    /// Melta, minimum 1, pas sur les blessures mortelles.
    /// </summary>
    public int Damage { get; init; }
}
