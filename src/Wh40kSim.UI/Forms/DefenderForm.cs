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
        Boosts = new DefenderBoostsForm(unit.Boosts);
        Groups = unit.Groups.Select(g => new ModelGroupForm(g)).ToList();
    }

    public string Name { get; set; }

    /// <summary>Couvert et boosts contre les attaques qui visent cette unité.</summary>
    public DefenderBoostsForm Boosts { get; set; }

    /// <summary>Groupes dans l'ordre d'allocation : le premier encaisse en premier.</summary>
    public List<ModelGroupForm> Groups { get; set; }

    public DefenderUnit ToDefenderUnit() => new()
    {
        Name = Name,
        Boosts = Boosts.ToDefenderBoosts(),
        Groups = Groups.Select(g => g.ToModelGroup()).ToList(),
    };
}
