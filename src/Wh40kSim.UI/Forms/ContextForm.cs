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
        HitReroll = context.HitReroll;
        WoundReroll = context.WoundReroll;
        PrecisionTarget = context.PrecisionTarget;
    }

    public bool HalfRange { get; set; }

    public bool RemainedStationary { get; set; }

    public bool Charged { get; set; }

    public int HitModifier { get; set; }

    public int WoundModifier { get; set; }

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
        HitReroll = HitReroll,
        WoundReroll = WoundReroll,
        PrecisionTarget = PrecisionTarget,
    };
}
