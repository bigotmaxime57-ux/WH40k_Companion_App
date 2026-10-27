using Wh40kSim.Core.Model;

namespace Wh40kSim.UI.Forms;

/// <summary>Version modifiable d'un <see cref="AttackContext"/> (situation de l'attaque).</summary>
public sealed class ContextForm
{
    public ContextForm()
        : this(new AttackContext())
    {
    }

    public ContextForm(AttackContext context)
    {
        HalfRange = context.HalfRange;
        RemainedStationary = context.RemainedStationary;
        Charged = context.Charged;
        HitModifier = context.HitModifier;
        WoundModifier = context.WoundModifier;
        SkillModifier = context.SkillModifier;
        AttacksModifier = context.AttacksModifier;
        StrengthModifier = context.StrengthModifier;
        ApModifier = context.ApModifier;
        DamageModifier = context.DamageModifier;
        HitReroll = context.HitReroll;
        WoundReroll = context.WoundReroll;
        PrecisionTarget = context.PrecisionTarget;
    }

    public bool HalfRange { get; set; }

    public bool RemainedStationary { get; set; }

    public bool Charged { get; set; }

    /// <summary>Modificateur au jet de touche (plafonné à ±1 au total par le moteur).</summary>
    public int HitModifier { get; set; }

    /// <summary>Modificateur au jet de blessure (plafonné à ±1 au total par le moteur).</summary>
    public int WoundModifier { get; set; }

    /// <summary>Modification de la CT / CC (+1 = 3+ devient 2+), sans plafond.</summary>
    public int SkillModifier { get; set; }

    public int AttacksModifier { get; set; }

    public int StrengthModifier { get; set; }

    /// <summary>Modification de la PA (+1 = PA-1 devient PA-2).</summary>
    public int ApModifier { get; set; }

    public int DamageModifier { get; set; }

    public RerollPolicy HitReroll { get; set; }

    public RerollPolicy WoundReroll { get; set; }

    /// <summary>Nom du PERSONNAGE visé par Precision (null = le premier encore en jeu).</summary>
    public string? PrecisionTarget { get; set; }

    public AttackContext ToAttackContext() => new()
    {
        HalfRange = HalfRange,
        RemainedStationary = RemainedStationary,
        Charged = Charged,
        HitModifier = HitModifier,
        WoundModifier = WoundModifier,
        SkillModifier = SkillModifier,
        AttacksModifier = AttacksModifier,
        StrengthModifier = StrengthModifier,
        ApModifier = ApModifier,
        DamageModifier = DamageModifier,
        HitReroll = HitReroll,
        WoundReroll = WoundReroll,
        PrecisionTarget = PrecisionTarget,
    };
}
