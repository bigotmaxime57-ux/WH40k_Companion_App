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
        SnapShooting = context.SnapShooting;
        PrecisionTarget = context.PrecisionTarget;
    }

    public bool HalfRange { get; set; }

    public bool RemainedStationary { get; set; }

    public bool Charged { get; set; }

    /// <summary>Tir en overwatch (snap shooting dans le Core).</summary>
    public bool SnapShooting { get; set; }

    /// <summary>Nom du PERSONNAGE visé par Precision (null = le premier encore en jeu).</summary>
    public string? PrecisionTarget { get; set; }

    public AttackContext ToAttackContext() => new()
    {
        HalfRange = HalfRange,
        RemainedStationary = RemainedStationary,
        Charged = Charged,
        SnapShooting = SnapShooting,
        PrecisionTarget = PrecisionTarget,
    };
}
