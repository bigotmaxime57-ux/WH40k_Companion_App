using Wh40kSim.Core.Dice;

namespace Wh40kSim.Core.Model;

public enum WeaponType
{
    Ranged,
    Melee,
}

/// <summary>Profil d'arme tel qu'il figure sur une fiche technique.</summary>
public sealed record WeaponProfile
{
    public string Name { get; init; } = "Arme";

    public WeaponType Type { get; init; } = WeaponType.Ranged;

    public DiceExpression Attacks { get; init; } = 1;

    /// <summary>CT ou CC : 3 signifie 3+.</summary>
    public int Skill { get; init; } = 4;

    public int Strength { get; init; } = 4;

    /// <summary>PA telle qu'écrite sur la fiche : 0, -1, -2… (une valeur positive est interprétée comme négative).</summary>
    public int Ap { get; init; }

    public DiceExpression Damage { get; init; } = 1;

    public WeaponAbilities Abilities { get; init; } = new();

    public string Describe()
    {
        string skill = Abilities.Torrent ? "N/A" : $"{Skill}+";
        string skillLabel = Type == WeaponType.Ranged ? "CT" : "CC";
        string abilities = Abilities.Describe();
        return $"{Name} : A{Attacks} {skillLabel}{skill} F{Strength} PA{-Math.Abs(Ap)} D{Damage}"
               + (abilities.Length > 0 ? $" [{abilities}]" : "");
    }
}

/// <summary>Un profil d'arme et le nombre de figurines de l'unité qui l'utilisent.</summary>
public sealed record WeaponGroup
{
    public int ModelCount { get; init; } = 1;

    public WeaponProfile Weapon { get; init; } = new();
}
