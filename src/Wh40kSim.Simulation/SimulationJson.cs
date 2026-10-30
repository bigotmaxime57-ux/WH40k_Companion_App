using System.Text.Json;
using Wh40kSim.Core.Serialization;

namespace Wh40kSim.Simulation;

/// <summary>
/// Lecture / écriture JSON des données d'une simulation (<see cref="SimulationData"/>), contrat du résultat entre le
/// serveur et le navigateur. Mêmes réglages stricts que les scénarios (<see cref="ScenarioJson.Options"/>) : propriété
/// inconnue ou manquante refusée, scénario au même format que les fichiers.
/// </summary>
public static class SimulationJson
{
    public static string Serialize(SimulationData data) => JsonSerializer.Serialize(data, ScenarioJson.Options);

    /// <exception cref="JsonException">JSON invalide, ou données incohérentes avec leur scénario (message détaillé).</exception>
    public static SimulationData Deserialize(string json)
    {
        var data = JsonSerializer.Deserialize<SimulationData>(json, ScenarioJson.Options)
                   ?? throw new JsonException("Les données de simulation sont vides.");
        var errors = data.Validate();
        if (errors.Count > 0)
        {
            throw new JsonException("Données de simulation incohérentes : " + string.Join(" ; ", errors));
        }
        return data;
    }
}
