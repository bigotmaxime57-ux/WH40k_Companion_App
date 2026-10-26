using Wh40kSim.Core.Model;

namespace Wh40kSim.UI.Forms;

/// <summary>Version modifiable d'un <see cref="ModelGroup"/> (groupe d'allocation).</summary>
public sealed class ModelGroupForm
{
    public ModelGroupForm()
        : this(new ModelGroup())
    {
    }

    public ModelGroupForm(ModelGroup group)
    {
        Name = group.Name;
        IsCharacter = group.IsCharacter;
        Count = group.Count;
        Toughness = group.Toughness;
        Wounds = group.Wounds;
        Save = group.Save;
        InvulnerableSave = group.InvulnerableSave;
        FeelNoPain = group.FeelNoPain;
        DamageReduction = group.DamageReduction;
    }

    public string Name { get; set; }

    public bool IsCharacter { get; set; }

    public int Count { get; set; }

    public int Toughness { get; set; }

    public int Wounds { get; set; }

    /// <summary>3 signifie 3+, 7 = aucune sauvegarde.</summary>
    public int Save { get; set; }

    /// <summary>null = aucune.</summary>
    public int? InvulnerableSave { get; set; }

    /// <summary>null = aucun.</summary>
    public int? FeelNoPain { get; set; }

    public int DamageReduction { get; set; }

    public ModelGroup ToModelGroup() => new()
    {
        Name = Name,
        IsCharacter = IsCharacter,
        Count = Count,
        Toughness = Toughness,
        Wounds = Wounds,
        Save = Save,
        InvulnerableSave = InvulnerableSave,
        FeelNoPain = FeelNoPain,
        DamageReduction = DamageReduction,
    };
}
