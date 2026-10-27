using Wh40kSim.Core.Model;

namespace Wh40kSim.UI.Forms;

/// <summary>Version modifiable des <see cref="AttackerBoosts"/> (valeurs du point de vue de l'attaquant).</summary>
public sealed class AttackerBoostsForm
{
    public AttackerBoostsForm()
        : this(new AttackerBoosts())
    {
    }

    public AttackerBoostsForm(AttackerBoosts boosts)
    {
        Skill = boosts.Skill;
        HitRoll = boosts.HitRoll;
        HitReroll = boosts.HitReroll;
        Strength = boosts.Strength;
        WoundRoll = boosts.WoundRoll;
        WoundReroll = boosts.WoundReroll;
        Attacks = boosts.Attacks;
        Ap = boosts.Ap;
        Damage = boosts.Damage;
    }

    /// <summary>Modification de la CT / CC (+1 = 3+ devient 2+), sans plafond.</summary>
    public int Skill { get; set; }

    /// <summary>Modificateur au jet de touche (plafonné à ±1 au total par le moteur).</summary>
    public int HitRoll { get; set; }

    public RerollPolicy HitReroll { get; set; }

    public int Strength { get; set; }

    /// <summary>Modificateur au jet de blessure (plafonné à ±1 au total par le moteur).</summary>
    public int WoundRoll { get; set; }

    public RerollPolicy WoundReroll { get; set; }

    public int Attacks { get; set; }

    /// <summary>Modification de la PA (+1 = PA-1 devient PA-2).</summary>
    public int Ap { get; set; }

    public int Damage { get; set; }

    /// <summary>Nombre de boosts différents de leur valeur neutre.</summary>
    public int ActiveCount =>
        new[] { Skill, HitRoll, Strength, WoundRoll, Attacks, Ap, Damage }.Count(v => v != 0)
        + (HitReroll != RerollPolicy.None ? 1 : 0)
        + (WoundReroll != RerollPolicy.None ? 1 : 0);

    public AttackerBoosts ToAttackerBoosts() => new()
    {
        Skill = Skill,
        HitRoll = HitRoll,
        HitReroll = HitReroll,
        Strength = Strength,
        WoundRoll = WoundRoll,
        WoundReroll = WoundReroll,
        Attacks = Attacks,
        Ap = Ap,
        Damage = Damage,
    };
}

/// <summary>Version modifiable des <see cref="DefenderBoosts"/> (valeurs du point de vue de l'attaquant).</summary>
public sealed class DefenderBoostsForm
{
    public DefenderBoostsForm()
        : this(new DefenderBoosts())
    {
    }

    public DefenderBoostsForm(DefenderBoosts boosts)
    {
        InCover = boosts.InCover;
        Skill = boosts.Skill;
        HitRoll = boosts.HitRoll;
        WoundRoll = boosts.WoundRoll;
        Ap = boosts.Ap;
        Damage = boosts.Damage;
    }

    public bool InCover { get; set; }

    /// <summary>CT / CC des attaquants (-1 = 3+ devient 4+).</summary>
    public int Skill { get; set; }

    public int HitRoll { get; set; }

    public int WoundRoll { get; set; }

    /// <summary>PA des attaques reçues (-1 = PA-2 devient PA-1).</summary>
    public int Ap { get; set; }

    /// <summary>Dégâts des attaques reçues (-1 = un dégât de moins).</summary>
    public int Damage { get; set; }

    /// <summary>Nombre de boosts différents de leur valeur neutre (couvert compris).</summary>
    public int ActiveCount =>
        new[] { Skill, HitRoll, WoundRoll, Ap, Damage }.Count(v => v != 0) + (InCover ? 1 : 0);

    public DefenderBoosts ToDefenderBoosts() => new()
    {
        InCover = InCover,
        Skill = Skill,
        HitRoll = HitRoll,
        WoundRoll = WoundRoll,
        Ap = Ap,
        Damage = Damage,
    };
}
