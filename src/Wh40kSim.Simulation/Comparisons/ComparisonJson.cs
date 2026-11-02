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
        EnsureComplete(comparison);
        return comparison;
    }

    /// <summary>
    /// Refuse une comparaison lue dont une partie obligatoire est écrite <c>null</c> (défenseur, entrées, attaquant…) :
    /// comme si elle manquait. Sert aussi au serveur, qui lit la comparaison dans le corps d'une demande.
    /// </summary>
    /// <exception cref="JsonException">Si une partie obligatoire est absente.</exception>
    public static void EnsureComplete(Comparison comparison)
    {
        if (comparison.Defender is null || comparison.Entries is null
            || comparison.Entries.Any(e => e?.Attacker is null || e.Context is null || e.Alternatives is null || e.Alternatives.Any(a => a?.Profiles is null || a.Profiles.Any(p => p is null))))
        {
            throw new JsonException("Le fichier de comparaison doit contenir un défenseur et une liste d'entrées, chacune avec son attaquant.");
        }
    }
}
