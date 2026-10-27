using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;

namespace Wh40kSim.UI.Forms;

/// <summary>
/// Version modifiable d'un <see cref="WeaponGroup"/> : le profil d'arme et le nombre de figurines qui l'utilisent,
/// saisis ensemble dans l'interface.
/// </summary>
public sealed class WeaponForm
{
    public WeaponForm()
        : this(new WeaponGroup())
    {
    }

    public WeaponForm(WeaponGroup group)
    {
        var weapon = group.Weapon;
        ModelCount = group.ModelCount;
        Name = weapon.Name;
        Type = weapon.Type;
        Attacks = weapon.Attacks.ToString();
        Skill = weapon.Skill;
        Strength = weapon.Strength;
        Ap = weapon.Ap;
        Damage = weapon.Damage.ToString();
        Abilities = new WeaponAbilitiesForm(weapon.Abilities);
    }

    public int ModelCount { get; set; }

    public string Name { get; set; }

    public WeaponType Type { get; set; }

    /// <summary>Expression de dés telle que saisie, ex. "2" ou "D6+1".</summary>
    public string Attacks { get; set; }

    /// <summary>CT ou CC : 3 signifie 3+.</summary>
    public int Skill { get; set; }

    public int Strength { get; set; }

    public int Ap { get; set; }

    /// <summary>Expression de dés telle que saisie, ex. "1" ou "D3".</summary>
    public string Damage { get; set; }

    public WeaponAbilitiesForm Abilities { get; set; }

    /// <exception cref="FormatException">Si <see cref="Attacks"/> ou <see cref="Damage"/> est invalide.</exception>
    public WeaponGroup ToWeaponGroup() => new()
    {
        ModelCount = ModelCount,
        Weapon = new WeaponProfile
        {
            Name = Name,
            Type = Type,
            Attacks = DiceExpression.Parse(Attacks),
            Skill = Skill,
            Strength = Strength,
            Ap = Ap,
            Damage = DiceExpression.Parse(Damage),
            Abilities = Abilities.ToWeaponAbilities(),
        },
    };

    /// <summary>Copie indépendante, telle que saisie (même si une expression de dés est invalide).</summary>
    public WeaponForm Clone()
    {
        var copy = (WeaponForm)MemberwiseClone();
        copy.Abilities = Abilities.Clone();
        return copy;
    }
}
