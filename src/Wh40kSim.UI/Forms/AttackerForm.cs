using Wh40kSim.Core.Model;

namespace Wh40kSim.UI.Forms;

/// <summary>Version modifiable d'une <see cref="AttackerUnit"/>.</summary>
public sealed class AttackerForm
{
    public AttackerForm()
        : this(new AttackerUnit())
    {
    }

    public AttackerForm(AttackerUnit unit)
    {
        Name = unit.Name;
        Boosts = new AttackerBoostsForm(unit.Boosts);
        Weapons = unit.Weapons.Select(w => new WeaponForm(w)).ToList();
    }

    public string Name { get; set; }

    /// <summary>Boosts appliqués à toutes les armes de l'unité.</summary>
    public AttackerBoostsForm Boosts { get; set; }

    /// <summary>Profils d'armes, résolus dans l'ordre de la liste.</summary>
    public List<WeaponForm> Weapons { get; set; }

    public AttackerUnit ToAttackerUnit() => new()
    {
        Name = Name,
        Boosts = Boosts.ToAttackerBoosts(),
        Weapons = Weapons.Select(w => w.ToWeaponGroup()).ToList(),
    };
}
