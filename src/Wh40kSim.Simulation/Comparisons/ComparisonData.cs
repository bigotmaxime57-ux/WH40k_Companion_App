using Wh40kSim.Core.Serialization;

namespace Wh40kSim.Simulation.Comparisons;

/// <summary>
/// Données brutes d'une comparaison : sa définition (attaquants, profils de rechange : de quoi retrouver les
/// variantes), le plan des rondes, la durée totale et, pour chaque variante (dans l'ordre de
/// <see cref="Comparison.Variants"/>), les compteurs de sa dernière ronde et la ronde où elle a été éliminée ;
/// <see cref="ComparisonResult"/> en recalcule tout. Sérialisable : contrat entre le serveur et le navigateur
/// (phase 5, étape 6).
/// </summary>
public sealed record ComparisonData
{
    public required Comparison Comparison { get; init; }

    /// <summary>Simulations de chaque ronde, de la première à la finale (<see cref="RoundPlan"/>).</summary>
    public required IReadOnlyList<int> Rounds { get; init; }

    /// <summary>Durée totale du calcul, toutes rondes comprises.</summary>
    public required TimeSpan Elapsed { get; init; }

    /// <summary>Une par variante, dans l'ordre de <see cref="Comparison.Variants"/>.</summary>
    public required IReadOnlyList<ComparisonEntryData> Entries { get; init; }

    /// <summary>
    /// Incohérences (vide si les données sont utilisables) : comparaison valide, plan des rondes, une entrée par variante
    /// avec exactement son scénario, données de chaque entrée, ronde d'élimination et nombre de simulations accordés au
    /// plan, même graine partout, top 3 jamais éliminé.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        if (Comparison is null || Comparison.Defender is null || Comparison.Entries is null)
        {
            return ["La définition de la comparaison manque."];
        }
        var comparisonErrors = Comparison.Validate();
        if (comparisonErrors.Count > 0)
        {
            return [.. comparisonErrors.Select(e => "Comparaison : " + e)];
        }
        if (Rounds is null || Rounds.Count == 0 || Rounds.Any(r => r < 1) || Rounds.Zip(Rounds.Skip(1)).Any(p => p.Second <= p.First))
        {
            return ["Le plan des rondes doit compter au moins une ronde, de plus en plus longues."];
        }
        var variants = Comparison.Variants();
        if (Entries is null || Entries.Count != variants.Count || Entries.Any(e => e?.Simulation?.Scenario?.Attacker is null))
        {
            return [$"Les entrées de la comparaison doivent être {variants.Count}, une par variante."];
        }

        var errors = new List<string>();
        for (int i = 0; i < Entries.Count; i++)
        {
            var entry = Entries[i];
            string label = $"Entrée '{variants[i].Name}'";
            errors.AddRange(entry.Simulation.Validate().Select(e => $"{label} : {e}"));
            if (ScenarioJson.Serialize(entry.Simulation.Scenario) != ScenarioJson.Serialize(variants[i].Scenario))
            {
                errors.Add($"{label} : scénario différent de celui de la variante.");
            }
            if (entry.EliminatedInRound is int round && (round < 1 || round >= Rounds.Count))
            {
                errors.Add($"{label} : éliminée à une ronde impossible ({round}).");
            }
        }
        if (errors.Count > 0)
        {
            return errors;
        }

        var first = Entries[0].Simulation;
        for (int i = 0; i < Entries.Count; i++)
        {
            var data = Entries[i].Simulation;
            string label = $"Entrée '{variants[i].Name}'";
            if (data.Seed != first.Seed) errors.Add($"{label} : graine différente des autres entrées.");
            int expected = Rounds[(Entries[i].EliminatedInRound ?? Rounds.Count) - 1];
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

/// <summary>Compteurs d'une variante : ceux de sa dernière ronde, et la ronde où elle a été éliminée (null : finaliste).</summary>
public sealed record ComparisonEntryData
{
    public required SimulationData Simulation { get; init; }

    public int? EliminatedInRound { get; init; }
}
