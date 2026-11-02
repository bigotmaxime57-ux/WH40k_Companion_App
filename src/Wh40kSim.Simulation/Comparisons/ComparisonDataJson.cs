using System.Text.Json;
using Wh40kSim.Core.Serialization;

namespace Wh40kSim.Simulation.Comparisons;

/// <summary>
/// Lecture / écriture JSON des données d'une comparaison (<see cref="ComparisonData"/>), contrat du résultat entre le
/// serveur et le navigateur. Mêmes réglages stricts que les scénarios ; écrit sur une seule ligne (un seul champ
/// <c>data:</c> en Server-Sent Events), comme <see cref="SimulationJson"/>.
/// </summary>
public static class ComparisonDataJson
{
    private static readonly JsonSerializerOptions Options = new(ScenarioJson.Options) { WriteIndented = false };

    public static string Serialize(ComparisonData data) => JsonSerializer.Serialize(data, Options);

    /// <exception cref="JsonException">JSON invalide, ou données incohérentes (message détaillé).</exception>
    public static ComparisonData Deserialize(string json)
    {
        var data = JsonSerializer.Deserialize<ComparisonData>(json, Options)
                   ?? throw new JsonException("Les données de comparaison sont vides.");
        var errors = data.Validate();
        if (errors.Count > 0)
        {
            throw new JsonException("Données de comparaison incohérentes : " + string.Join(" ; ", errors));
        }
        return data;
    }
}
