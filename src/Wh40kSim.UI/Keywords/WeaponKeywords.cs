using Wh40kSim.UI.Forms;

namespace Wh40kSim.UI.Keywords;

/// <summary>
/// Catalogue des mots-clés d'arme gérés par le moteur, rangés par catégorie, branchés sur <see cref="WeaponAbilitiesForm"/>.
/// Les mots-clés à valeur gardent leur valeur quand on les désactive (comme les cases à cocher du formulaire).
/// </summary>
public static class WeaponKeywords
{
    public const string Attacks = "Attacks";
    public const string Hit = "Hit";
    public const string Wound = "Wound";
    public const string Damage = "Damage";
    public const string Targeting = "Targeting";

    /// <summary>Catégories, dans l'ordre d'affichage (celui de la séquence d'attaque).</summary>
    public static IReadOnlyList<string> Categories { get; } = [Attacks, Hit, Wound, Damage, Targeting];

    /// <summary>Seuil critique désactivé : critique sur 6 seulement (valeur par défaut du Core).</summary>
    private const int DefaultCritical = 6;

    /// <summary>Seuil proposé quand on active « critique sur X+ ».</summary>
    private const int ActivatedCritical = 5;

    public static IReadOnlyList<KeywordDefinition<WeaponAbilitiesForm>> All { get; } =
    [
        Flag("Blast", Attacks, a => a.Blast, (a, v) => a.Blast = v),
        new()
        {
            Id = "RapidFire", Category = Attacks, ValueKind = KeywordValueKind.Number, Min = 1, Max = 20,
            IsActive = a => a.HasRapidFire, SetActive = (a, v) => a.HasRapidFire = v,
            GetNumber = a => a.RapidFire, SetNumber = (a, v) => a.RapidFire = v,
        },
        Flag("Torrent", Hit, a => a.Torrent, (a, v) => a.Torrent = v),
        Flag("Heavy", Hit, a => a.Heavy, (a, v) => a.Heavy = v),
        Flag("LethalHits", Hit, a => a.LethalHits, (a, v) => a.LethalHits = v),
        new()
        {
            Id = "SustainedHits", Category = Hit, ValueKind = KeywordValueKind.Dice,
            IsActive = a => a.HasSustainedHits, SetActive = (a, v) => a.HasSustainedHits = v,
            GetText = a => a.SustainedHits, SetText = (a, v) => a.SustainedHits = v,
        },
        Critical("CriticalHitOn", Hit, a => a.CriticalHitOn, (a, v) => a.CriticalHitOn = v),
        Flag("TwinLinked", Wound, a => a.TwinLinked, (a, v) => a.TwinLinked = v),
        Flag("Lance", Wound, a => a.Lance, (a, v) => a.Lance = v),
        Flag("DevastatingWounds", Wound, a => a.DevastatingWounds, (a, v) => a.DevastatingWounds = v),
        Critical("CriticalWoundOn", Wound, a => a.CriticalWoundOn, (a, v) => a.CriticalWoundOn = v),
        new()
        {
            Id = "Melta", Category = Damage, ValueKind = KeywordValueKind.Number, Min = 1, Max = 20,
            IsActive = a => a.HasMelta, SetActive = (a, v) => a.HasMelta = v,
            GetNumber = a => a.Melta, SetNumber = (a, v) => a.Melta = v,
        },
        Flag("IgnoresCover", Targeting, a => a.IgnoresCover, (a, v) => a.IgnoresCover = v),
        Flag("Precision", Targeting, a => a.Precision, (a, v) => a.Precision = v),
    ];

    private static KeywordDefinition<WeaponAbilitiesForm> Flag(
        string id, string category, Func<WeaponAbilitiesForm, bool> get, Action<WeaponAbilitiesForm, bool> set) =>
        new() { Id = id, Category = category, IsActive = get, SetActive = set };

    /// <summary>« Critique sur X+ » : actif dès que le seuil n'est plus 6 ; le désactiver remet 6.</summary>
    private static KeywordDefinition<WeaponAbilitiesForm> Critical(
        string id, string category, Func<WeaponAbilitiesForm, int> get, Action<WeaponAbilitiesForm, int> set) => new()
    {
        Id = id,
        Category = category,
        ValueKind = KeywordValueKind.Threshold,
        Min = 2,
        Max = DefaultCritical - 1,
        IsActive = a => get(a) != DefaultCritical,
        SetActive = (a, v) => set(a, v ? ActivatedCritical : DefaultCritical),
        GetNumber = get,
        SetNumber = set,
    };
}
