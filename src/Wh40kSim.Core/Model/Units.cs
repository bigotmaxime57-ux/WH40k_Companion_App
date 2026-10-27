using System.Text.Json.Serialization;

namespace Wh40kSim.Core.Model;

/// <summary>Unité attaquante : ses armes sont résolues dans l'ordre de la liste.</summary>
public sealed record AttackerUnit
{
    public string Name { get; init; } = "Attaquant";

    public IReadOnlyList<WeaponGroup> Weapons { get; init; } = [];

    /// <summary>Au moins une arme est concernée par la demi-portée (voir <see cref="WeaponProfile.UsesHalfRange"/>).</summary>
    [JsonIgnore]
    public bool UsesHalfRange => Weapons.Any(w => w.Weapon.UsesHalfRange);

    /// <summary>Au moins une arme est concernée par l'immobilité (voir <see cref="WeaponProfile.UsesRemainedStationary"/>).</summary>
    [JsonIgnore]
    public bool UsesRemainedStationary => Weapons.Any(w => w.Weapon.UsesRemainedStationary);

    /// <summary>Au moins une arme est concernée par la charge (voir <see cref="WeaponProfile.UsesCharged"/>).</summary>
    [JsonIgnore]
    public bool UsesCharged => Weapons.Any(w => w.Weapon.UsesCharged);
}

/// <summary>
/// Groupe d'allocation (règles 05.03, 11e édition) :
/// un groupe par figurine PERSONNAGE, et un groupe pour toutes les autres figurines ayant les mêmes PV, Sv et InSv.
/// Les groupes encaissent dans l'ordre choisi par le défenseur, les PERSONNAGES toujours après les autres.
/// </summary>
public sealed record ModelGroup
{
    public string Name { get; init; } = "Figurines";

    public int Count { get; init; } = 1;

    /// <summary>Figurine PERSONNAGE (leader) : groupe d'une seule figurine, alloué après les gardes du corps.</summary>
    public bool IsCharacter { get; init; }

    /// <summary>Endurance des figurines de ce groupe.</summary>
    public int Toughness { get; init; } = 4;

    /// <summary>Points de vie par figurine.</summary>
    public int Wounds { get; init; } = 1;

    /// <summary>Sauvegarde d'armure : 3 signifie 3+, 7 = aucune sauvegarde.</summary>
    public int Save { get; init; } = 7;

    /// <summary>Sauvegarde invulnérable (null = aucune).</summary>
    public int? InvulnerableSave { get; init; }

    /// <summary>Insensible à la douleur X+ (null = aucun).</summary>
    public int? FeelNoPain { get; init; }

    /// <summary>Réduction des dégâts de chaque attaque (minimum 1 dégât).</summary>
    public int DamageReduction { get; init; }

    public string Describe()
    {
        string save = Save >= 7 ? "aucune svg" : $"Sv {Save}+";
        if (InvulnerableSave is int invuln) save += $" / {invuln}++";
        if (FeelNoPain is int fnp) save += $", FNP {fnp}+";
        if (DamageReduction > 0) save += $", -{DamageReduction} dégât";
        string character = IsCharacter ? " (PERSONNAGE)" : "";
        return $"{Name}{character} : {Count} fig. × {Wounds} PV, E{Toughness}, {save}";
    }
}

/// <summary>
/// Unité ciblée. <see cref="Groups"/> est dans l'ordre d'allocation (le premier encaisse en premier).
/// Les caractéristiques des figurines (Endurance, PV, sauvegardes) sont portées par les groupes ;
/// l'Endurance utilisée pour blesser est calculée par le moteur (règle 05.02.01).
/// </summary>
public sealed record DefenderUnit
{
    public string Name { get; init; } = "Défenseur";

    /// <summary>Règle 11e édition : le couvert dégrade la CT de l'attaquant de 1 (armes de tir uniquement).</summary>
    public bool InCover { get; init; }

    /// <summary>Modificateur appliqué aux jets de touche contre cette unité (ex. -1 pour Stealth).</summary>
    public int HitModifier { get; init; }

    /// <summary>Modificateur appliqué aux jets de blessure contre cette unité.</summary>
    public int WoundModifier { get; init; }

    // Boosts du défenseur sur les caractéristiques des attaques qui le visent : exprimés du point de vue de l'attaquant
    // (négatif = moins bon pour lui), sans plafond, cumulés avec ceux de l'attaquant (voir AttackContext).

    /// <summary>CT / CC des attaquants (-1 : 3+ devient 4+), cumulée avec celle de l'attaquant et le couvert.</summary>
    public int SkillModifier { get; init; }

    /// <summary>PA des attaques reçues (-1 : PA-2 devient PA-1) ; la PA ne dépasse jamais 0.</summary>
    public int ApModifier { get; init; }

    /// <summary>
    /// Dégâts des attaques reçues (-1 : un dégât de moins), appliqués comme la réduction de dégâts des groupes : après
    /// Melta, minimum 1, pas sur les blessures mortelles.
    /// </summary>
    public int DamageModifier { get; init; }

    public IReadOnlyList<ModelGroup> Groups { get; init; } = [];

    [JsonIgnore]
    public int TotalModels => Groups.Sum(g => g.Count);

    [JsonIgnore]
    public int TotalWounds => Groups.Sum(g => g.Count * g.Wounds);

    /// <summary>
    /// Profil des figurines virtuelles qui encaissent l'excédent une fois l'unité détruite : le premier groupe
    /// non-PERSONNAGE dans l'ordre d'allocation (un garde du corps : le leader, tué en dernier, est unique et souvent
    /// plus résistant), à défaut le premier groupe. Null si l'unité n'a aucun groupe.
    /// </summary>
    [JsonIgnore]
    public ModelGroup? ExcessProfile => Groups.FirstOrDefault(g => !g.IsCharacter) ?? Groups.FirstOrDefault();
}
