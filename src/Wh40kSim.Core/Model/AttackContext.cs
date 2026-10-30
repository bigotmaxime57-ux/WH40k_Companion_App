namespace Wh40kSim.Core.Model;

/// <summary>
/// Situation de l'attaque : conditions qui changent quelque chose pour certaines armes. Les boosts (auras,
/// stratagèmes…) sont portés par l'attaquant et le défenseur (<see cref="AttackerBoosts"/>, <see cref="DefenderBoosts"/>).
/// </summary>
public sealed record AttackContext
{
    /// <summary>À demi-portée (Rapid Fire, Melta).</summary>
    public bool HalfRange { get; init; }

    /// <summary>Restée immobile (Heavy, armes de tir).</summary>
    public bool RemainedStationary { get; init; }

    /// <summary>A chargé ce tour (Lance).</summary>
    public bool Charged { get; init; }

    /// <summary>
    /// Tir en snap shooting (15.09), utilisé par l'Overwatch (15.08) : seules les armes de tir tirent ; chaque attaque ne
    /// touche que sur un 6 non modifié, quels que soient la CT et les modificateurs, sans relance, et ces touches ne
    /// sont pas critiques (Torrent touche toujours automatiquement). Demi-portée seule utile : « immobile » et
    /// « a chargé » sont sans effet.
    /// </summary>
    public bool SnapShooting { get; init; }

    /// <summary>
    /// Nom du groupe PERSONNAGE visé par les armes Precision (null = le premier PERSONNAGE encore en jeu,
    /// dans l'ordre d'allocation).
    /// </summary>
    public string? PrecisionTarget { get; init; }
}
