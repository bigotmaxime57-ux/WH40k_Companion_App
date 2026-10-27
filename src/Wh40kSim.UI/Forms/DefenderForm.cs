using Wh40kSim.Core.Model;

namespace Wh40kSim.UI.Forms;

/// <summary>Version modifiable d'une <see cref="DefenderUnit"/>.</summary>
public sealed class DefenderForm
{
    public DefenderForm()
        : this(new DefenderUnit())
    {
    }

    public DefenderForm(DefenderUnit unit)
    {
        Name = unit.Name;
        InCover = unit.InCover;
        HitModifier = unit.HitModifier;
        WoundModifier = unit.WoundModifier;
        SkillModifier = unit.SkillModifier;
        ApModifier = unit.ApModifier;
        Groups = unit.Groups.Select(g => new ModelGroupForm(g)).ToList();
    }

    public string Name { get; set; }

    public bool InCover { get; set; }

    public int HitModifier { get; set; }

    public int WoundModifier { get; set; }

    /// <summary>Modification de la CT / CC des attaquants, de leur point de vue (-1 = 3+ devient 4+).</summary>
    public int SkillModifier { get; set; }

    /// <summary>Modification de la PA des attaques reçues, du point de vue de l'attaquant (-1 = PA-2 devient PA-1).</summary>
    public int ApModifier { get; set; }

    /// <summary>Groupes dans l'ordre d'allocation : le premier encaisse en premier.</summary>
    public List<ModelGroupForm> Groups { get; set; }

    public DefenderUnit ToDefenderUnit() => new()
    {
        Name = Name,
        InCover = InCover,
        HitModifier = HitModifier,
        WoundModifier = WoundModifier,
        SkillModifier = SkillModifier,
        ApModifier = ApModifier,
        Groups = Groups.Select(g => g.ToModelGroup()).ToList(),
    };
}
