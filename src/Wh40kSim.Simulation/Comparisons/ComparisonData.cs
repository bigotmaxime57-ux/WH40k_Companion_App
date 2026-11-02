using Wh40kSim.Core.Serialization;

namespace Wh40kSim.Simulation.Comparisons;

/// <summary>
/// Données brutes d'une comparaison : plan des rondes, durée totale et, pour chaque entrée (ordre de saisie), les
/// compteurs de sa dernière ronde et la ronde où elle a été éliminée ; <see cref="ComparisonResult"/> en recalcule tout.
/// Sérialisable : contrat entre le serveur et le navigateur (phase 5, étape 6).
/// </summary>
public sealed record ComparisonData
{
    /// <summary>Simulations de chaque ronde, de la première à la finale (<see cref="RoundPlan"/>).</summary>
    public required IReadOnlyList<int> Rounds { get; init; }

    /// <summary>Durée totale du calcul, toutes rondes comprises.</summary>
    public required TimeSpan Elapsed { get; init; }

    public required IReadOnlyList<ComparisonEntryData> Entries { get; init; }

    /// <summary>
    /// Incohérences (vide si les données sont utilisables) : plan des rondes, nombre d'entrées et noms, données de chaque
    /// entrée, ronde d'élimination et nombre de simulations accordés au plan, même défenseur, même overwatch et même
    /// graine partout.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        if (Rounds is null || Rounds.Count == 0 || Rounds.Any(r => r < 1) || Rounds.Zip(Rounds.Skip(1)).Any(p => p.Second <= p.First))
        {
            return ["Le plan des rondes doit compter au moins une ronde, de plus en plus longues."];
        }
        if (Entries is null || Entries.Any(e => e?.Simulation?.Scenario?.Attacker is null))
        {
            return ["Les entrées de la comparaison manquent."];
        }

        var errors = new List<string>(Comparison.NameErrors(Entries.Select(e => e.Simulation.Scenario.Attacker.Name).ToList()));
        foreach (var entry in Entries)
        {
            string label = $"Entrée '{entry.Simulation.Scenario.Attacker.Name}'";
            errors.AddRange(entry.Simulation.Validate().Select(e => $"{label} : {e}"));
            if (entry.EliminatedInRound is int round && (round < 1 || round >= Rounds.Count))
            {
                errors.Add($"{label} : éliminée à une ronde impossible ({round}).");
            }
        }
        if (errors.Count > 0 || Entries.Count == 0)
        {
            return errors;
        }

        var first = Entries[0].Simulation;
        string defender = ScenarioJson.Serialize(first.Scenario.Defender);
        bool snapShooting = first.Scenario.Context.SnapShooting;
        foreach (var entry in Entries)
        {
            var data = entry.Simulation;
            string label = $"Entrée '{data.Scenario.Attacker.Name}'";
            if (ScenarioJson.Serialize(data.Scenario.Defender) != defender) errors.Add($"{label} : défenseur différent des autres entrées.");
            if (data.Scenario.Context.SnapShooting != snapShooting) errors.Add($"{label} : tir en overwatch différent des autres entrées (il est commun).");
            if (data.Seed != first.Seed) errors.Add($"{label} : graine différente des autres entrées.");
            int expected = Rounds[(entry.EliminatedInRound ?? Rounds.Count) - 1];
            if (data.Iterations != expected) errors.Add($"{label} : {data.Iterations} simulations au lieu des {expected} de sa dernière ronde.");
        }
        int finalists = Entries.Count(e => e.EliminatedInRound is null);
        if (finalists < Math.Min(EliminationRounds.Kept, Entries.Count))
        {
            errors.Add($"Seulement {finalists} finaliste(s) : le top {EliminationRounds.Kept} n'est jamais éliminé.");
        }
        return errors;
    }
}

/// <summary>Compteurs d'une entrée : ceux de sa dernière ronde, et la ronde où elle a été éliminée (null : finaliste).</summary>
public sealed record ComparisonEntryData
{
    public required SimulationData Simulation { get; init; }

    public int? EliminatedInRound { get; init; }
}
