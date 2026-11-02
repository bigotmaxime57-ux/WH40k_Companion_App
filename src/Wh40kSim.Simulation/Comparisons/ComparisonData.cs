using System.Text.Json;
using Wh40kSim.Core.Serialization;

namespace Wh40kSim.Simulation.Comparisons;

/// <summary>
/// Données brutes d'une comparaison : les compteurs de chaque entrée (<see cref="SimulationData"/>, dans l'ordre de
/// saisie), d'où <see cref="ComparisonResult"/> recalcule tout. Sérialisable : contrat entre le serveur et le navigateur
/// (phase 5, étape 6).
/// </summary>
public sealed record ComparisonData
{
    public required IReadOnlyList<SimulationData> Entries { get; init; }

    /// <summary>
    /// Incohérences (vide si les données sont utilisables) : nombre d'entrées et noms, données de chaque entrée, même
    /// défenseur, même situation, même graine et même nombre de simulations partout.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        if (Entries is null || Entries.Any(e => e?.Scenario?.Attacker is null))
        {
            return ["Les entrées de la comparaison manquent."];
        }

        var errors = new List<string>(Comparison.NameErrors(Entries.Select(e => e.Scenario.Attacker.Name).ToList()));
        for (int i = 0; i < Entries.Count; i++)
        {
            errors.AddRange(Entries[i].Validate().Select(e => $"Entrée '{Entries[i].Scenario.Attacker.Name}' : {e}"));
        }
        if (errors.Count > 0 || Entries.Count == 0)
        {
            return errors;
        }

        var first = Entries[0];
        string defender = ScenarioJson.Serialize(first.Scenario.Defender);
        string context = JsonSerializer.Serialize(first.Scenario.Context, ScenarioJson.Options);
        foreach (var entry in Entries.Skip(1))
        {
            string label = $"Entrée '{entry.Scenario.Attacker.Name}'";
            if (ScenarioJson.Serialize(entry.Scenario.Defender) != defender) errors.Add($"{label} : défenseur différent des autres entrées.");
            if (JsonSerializer.Serialize(entry.Scenario.Context, ScenarioJson.Options) != context) errors.Add($"{label} : situation différente des autres entrées.");
            if (entry.Seed != first.Seed) errors.Add($"{label} : graine différente des autres entrées.");
            if (entry.Iterations != first.Iterations) errors.Add($"{label} : nombre de simulations différent des autres entrées.");
        }
        return errors;
    }
}
