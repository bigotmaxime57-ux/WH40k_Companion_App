using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;

namespace Wh40kSim.UI.Forms;

/// <summary>
/// Version modifiable de <see cref="WeaponAbilities"/>. Les règles à valeur (Sustained Hits X, Rapid Fire X, Melta X)
/// ont une case à cocher et une valeur séparées : décocher puis recocher la case retrouve la valeur saisie.
/// </summary>
public sealed class WeaponAbilitiesForm
{
    private const string DefaultSustainedHits = "1";
    private const int DefaultRapidFire = 1;
    private const int DefaultMelta = 2;

    public WeaponAbilitiesForm()
        : this(new WeaponAbilities())
    {
    }

    public WeaponAbilitiesForm(WeaponAbilities abilities)
    {
        Torrent = abilities.Torrent;
        LethalHits = abilities.LethalHits;
        HasSustainedHits = !abilities.SustainedHits.IsZero;
        SustainedHits = abilities.SustainedHits.IsZero ? DefaultSustainedHits : abilities.SustainedHits.ToString();
        DevastatingWounds = abilities.DevastatingWounds;
        TwinLinked = abilities.TwinLinked;
        Blast = abilities.Blast;
        HasRapidFire = abilities.RapidFire > 0;
        RapidFire = abilities.RapidFire > 0 ? abilities.RapidFire : DefaultRapidFire;
        HasMelta = abilities.Melta > 0;
        Melta = abilities.Melta > 0 ? abilities.Melta : DefaultMelta;
        Heavy = abilities.Heavy;
        Lance = abilities.Lance;
        IgnoresCover = abilities.IgnoresCover;
        Precision = abilities.Precision;
        CriticalHitOn = abilities.CriticalHitOn;
        CriticalWoundOn = abilities.CriticalWoundOn;
    }

    public bool Torrent { get; set; }

    public bool LethalHits { get; set; }

    public bool HasSustainedHits { get; set; }

    /// <summary>Touches supplémentaires par touche critique, telles que saisies (ex. "1" ou "D3").</summary>
    public string SustainedHits { get; set; }

    public bool DevastatingWounds { get; set; }

    public bool TwinLinked { get; set; }

    public bool Blast { get; set; }

    public bool HasRapidFire { get; set; }

    public int RapidFire { get; set; }

    public bool HasMelta { get; set; }

    public int Melta { get; set; }

    public bool Heavy { get; set; }

    public bool Lance { get; set; }

    public bool IgnoresCover { get; set; }

    public bool Precision { get; set; }

    public int CriticalHitOn { get; set; }

    public int CriticalWoundOn { get; set; }

    /// <exception cref="FormatException">Si Sustained Hits est coché avec une valeur invalide.</exception>
    public WeaponAbilities ToWeaponAbilities() => new()
    {
        Torrent = Torrent,
        LethalHits = LethalHits,
        SustainedHits = HasSustainedHits ? DiceExpression.Parse(SustainedHits) : default,
        DevastatingWounds = DevastatingWounds,
        TwinLinked = TwinLinked,
        Blast = Blast,
        RapidFire = HasRapidFire ? RapidFire : 0,
        Melta = HasMelta ? Melta : 0,
        Heavy = Heavy,
        Lance = Lance,
        IgnoresCover = IgnoresCover,
        Precision = Precision,
        CriticalHitOn = CriticalHitOn,
        CriticalWoundOn = CriticalWoundOn,
    };
}
