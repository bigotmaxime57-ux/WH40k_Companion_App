using System.Text.Json;
using Wh40kSim.Core.Serialization;

namespace Wh40kSim.Simulation.Comparisons;

/// <summary>
/// Lecture / écriture JSON d'une comparaison : <c>{ "defender": …, "snapShooting": …, "entries": [ { "attacker": …,
/// "context": … } ] }</c>, défenseur, attaquants et situations au format des fichiers de scénario, overwatch et situations
/// facultatifs. Mêmes réglages stricts que les scénarios
/// (<see cref="ScenarioJson.Options"/>) : propriété inconnue ou manquante refusée. La lecture ne valide pas la
/// comparaison (<see cref="Comparison.Validate"/>), comme pour un scénario.
/// </summary>
public static class ComparisonJson
{
    public static string Serialize(Comparison comparison) => JsonSerializer.Serialize(comparison, ScenarioJson.Options);

    /// <exception cref="JsonException">JSON invalide, défenseur ou attaquants absents, propriété inconnue.</exception>
    public static Comparison Deserialize(string json)
    {
        var comparison = JsonSerializer.Deserialize<Comparison>(json, ScenarioJson.Options)
                         ?? throw new JsonException("Le fichier de comparaison est vide.");
        // Présents mais écrits null : refusés comme s'ils manquaient
        if (comparison.Defender is null || comparison.Entries is null || comparison.Entries.Any(e => e?.Attacker is null || e.Context is null))
        {
            throw new JsonException("Le fichier de comparaison doit contenir un défenseur et une liste d'entrées, chacune avec son attaquant.");
        }
        return comparison;
    }
}
