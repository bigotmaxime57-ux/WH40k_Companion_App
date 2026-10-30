namespace Wh40kSim.UI.Keywords;

/// <summary>Nature de la valeur d'un mot-clé.</summary>
public enum KeywordValueKind
{
    /// <summary>Pas de valeur (Torrent, Lethal Hits…).</summary>
    None,

    /// <summary>Entier entre Min et Max (Rapid Fire X, Melta X).</summary>
    Number,

    /// <summary>Expression de dés lue par le Core (Sustained Hits D3).</summary>
    Dice,

    /// <summary>Seuil X+ entre Min et Max (touche critique sur 5+).</summary>
    Threshold,
}

/// <summary>
/// Description d'un mot-clé pour le sélecteur : où il se range, comment l'activer et lire / modifier sa valeur sur une
/// cible (ici un formulaire d'arme ; en phase 8, d'autres cibles et une portée). Le nom et le rappel de règle sont dans
/// Strings.resx : <c>Keyword_{Id}</c> et <c>Keyword_{Id}_Help</c> ; la catégorie : <c>KeywordCategory_{Category}</c>.
/// Aucune règle de jeu ici : seulement l'accès aux champs du formulaire.
/// </summary>
public sealed class KeywordDefinition<TTarget>
{
    public required string Id { get; init; }

    public required string Category { get; init; }

    public KeywordValueKind ValueKind { get; init; }

    public required Func<TTarget, bool> IsActive { get; init; }

    public required Action<TTarget, bool> SetActive { get; init; }

    /// <summary>Valeur entière (<see cref="KeywordValueKind.Number"/>, <see cref="KeywordValueKind.Threshold"/>).</summary>
    public Func<TTarget, int>? GetNumber { get; init; }

    public Action<TTarget, int>? SetNumber { get; init; }

    public int Min { get; init; }

    public int Max { get; init; }

    /// <summary>Valeur texte (<see cref="KeywordValueKind.Dice"/>).</summary>
    public Func<TTarget, string>? GetText { get; init; }

    public Action<TTarget, string>? SetText { get; init; }

    public string NameKey => $"Keyword_{Id}";

    public string HelpKey => $"Keyword_{Id}_Help";

    public string CategoryKey => $"KeywordCategory_{Category}";

    /// <summary>Valeur affichée après le nom sur l'étiquette (« D3 », « 2 », « 5+ »), null sans valeur.</summary>
    public string? ValueText(TTarget target) => ValueKind switch
    {
        KeywordValueKind.Number => GetNumber!(target).ToString(),
        KeywordValueKind.Dice => GetText!(target),
        KeywordValueKind.Threshold => $"{GetNumber!(target)}+",
        _ => null,
    };
}
