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
        Groups = unit.Groups.Select(g => new ModelGroupForm(g)).ToList();
    }

    public string Name { get; set; }

    public bool InCover { get; set; }

    public int HitModifier { get; set; }

    public int WoundModifier { get; set; }

    /// <summary>Groupes dans l'ordre d'allocation : le premier encaisse en premier.</summary>
    public List<ModelGroupForm> Groups { get; set; }

    public DefenderUnit ToDefenderUnit() => new()
    {
        Name = Name,
        InCover = InCover,
        HitModifier = HitModifier,
        WoundModifier = WoundModifier,
        Groups = Groups.Select(g => g.ToModelGroup()).ToList(),
    };
}
