using System.Text.Json.Serialization;
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

    /// <summary>
    /// « Pêche aux 6 » à la touche : avec la relance complète, relance aussi les touches ordinaires pour chercher une
    /// touche critique. Sans effet si <see cref="CanFishForCriticalHits"/> est faux (choix gardé, ignoré).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool FishForCriticalHits { get; init; }

    /// <summary>
    /// « Pêche aux 6 » à la blessure : avec la relance complète, relance aussi les blessures ordinaires pour chercher
    /// une blessure critique. Sans effet si <see cref="CanFishForCriticalWounds"/> est faux (choix gardé, ignoré).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool FishForCriticalWounds { get; init; }

    /// <summary>
    /// La pêche aux 6 à la touche a un sens : un effet sur touche critique (Lethal Hits, Sustained Hits), la relance
    /// complète des touches, et un jet de touche relançable (pas Torrent, pas en snap shooting).
    /// </summary>
    public bool CanFishForCriticalHits(AttackerBoosts boosts, AttackContext context) =>
        (Abilities.LethalHits || !Abilities.SustainedHits.IsZero)
        && !Abilities.Torrent && !context.SnapShooting && boosts.HitReroll == RerollPolicy.Failed;

    /// <summary>
    /// La pêche aux 6 à la blessure a un sens : Devastating Wounds, et la relance complète des blessures (boosts de
    /// l'attaquant, ou Twin-linked qui permet de relancer le jet de blessure).
    /// </summary>
    public bool CanFishForCriticalWounds(AttackerBoosts boosts) =>
        Abilities.DevastatingWounds && (boosts.WoundReroll == RerollPolicy.Failed || Abilities.TwinLinked);

    /// <summary>La situation « à demi-portée » change quelque chose pour cette arme (Rapid Fire, Melta).</summary>
    [JsonIgnore]
    public bool UsesHalfRange => Abilities.RapidFire > 0 || Abilities.Melta > 0;

    /// <summary>La situation « restée immobile » change quelque chose pour cette arme (Heavy, armes de tir uniquement).</summary>
    [JsonIgnore]
    public bool UsesRemainedStationary => Abilities.Heavy && Type == WeaponType.Ranged;

    /// <summary>La situation « a chargé » change quelque chose pour cette arme (Lance).</summary>
    [JsonIgnore]
    public bool UsesCharged => Abilities.Lance;

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
