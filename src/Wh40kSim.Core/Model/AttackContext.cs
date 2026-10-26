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

/// <summary>Situation de l'attaque et bonus apportés par des sources externes (aura, stratagème…).</summary>
public sealed record AttackContext
{
    public bool HalfRange { get; init; }

    public bool RemainedStationary { get; init; }

    public bool Charged { get; init; }

    /// <summary>Modificateur aux jets de touche de l'attaquant (cumulé avec celui du défenseur, plafonné à ±1).</summary>
    public int HitModifier { get; init; }

    /// <summary>Modificateur aux jets de blessure de l'attaquant (cumulé avec celui du défenseur, plafonné à ±1).</summary>
    public int WoundModifier { get; init; }

    public RerollPolicy HitReroll { get; init; }

    public RerollPolicy WoundReroll { get; init; }

    /// <summary>
    /// Nom du groupe PERSONNAGE visé par les armes Precision (null = le premier PERSONNAGE encore en jeu,
    /// dans l'ordre d'allocation).
    /// </summary>
    public string? PrecisionTarget { get; init; }
}
