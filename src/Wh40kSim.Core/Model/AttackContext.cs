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
    /// Nom du groupe PERSONNAGE visé par les armes Precision (null = le premier PERSONNAGE encore en jeu,
    /// dans l'ordre d'allocation).
    /// </summary>
    public string? PrecisionTarget { get; init; }
}
