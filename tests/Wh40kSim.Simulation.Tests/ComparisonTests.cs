using System.Text.Json;
using System.Text.Json.Nodes;
using Wh40kSim.Core.Model;
using Wh40kSim.Simulation.Comparisons;

namespace Wh40kSim.Simulation.Tests;

/// <summary>
/// Comparaison de plusieurs attaquants contre un même défenseur : validation, JSON, lancement (entrées identiques à une
/// simulation seule à graine égale), classement par score d'efficacité, top 3 et meilleures entrées, ex æquo compris.
/// </summary>
public class ComparisonTests
{
    private const int Iterations = 20_000;

    private static DefenderUnit Squad => new()
    {
        Groups = [new ModelGroup { Name = "Gardes", Count = 10, Wounds = 1, Toughness = 4, Save = 4 }],
    };

    private static AttackerUnit Attacker(string name, int attacks, WeaponType type = WeaponType.Ranged) => new()
    {
        Name = name,
        Weapons = [new WeaponGroup { ModelCount = 1, Weapon = new WeaponProfile { Name = "Bolter", Type = type, Attacks = attacks, Skill = 3, Strength = 4 } }],
    };

    private static Comparison Of(params AttackerUnit[] attackers) => Comparison.Of(Squad, attackers);

    // Validation

    [Fact]
    public void A_valid_comparison_has_no_error()
    {
        Assert.Empty(Of(Attacker("A", 5), Attacker("B", 10)).Validate());
    }

    [Fact]
    public void At_least_two_attackers_are_required()
    {
        var error = Assert.Single(Of(Attacker("A", 5)).Validate());
        Assert.Contains("au moins 2 attaquants", error);
    }

    [Fact]
    public void Names_must_be_unique_ignoring_case_and_surrounding_spaces()
    {
        var error = Assert.Single(Of(Attacker("Rubrics", 5), Attacker(" rubrics ", 10)).Validate());
        Assert.Contains("Rubrics", error);
        Assert.Contains("unique", error);
    }

    [Fact]
    public void A_name_is_required()
    {
        var error = Assert.Single(Of(Attacker("A", 5), Attacker("  ", 10)).Validate());
        Assert.Contains("Attaquant n° 2", error);
    }

    [Fact]
    public void Errors_of_an_entry_are_prefixed_with_its_name()
    {
        var invalid = Attacker("Rubrics", 5) with { Weapons = [] };

        var error = Assert.Single(Of(Attacker("A", 5), invalid).Validate());
        Assert.StartsWith("Rubrics : ", error);
        Assert.Throws<ArgumentException>(() => Comparator.Run(Of(Attacker("A", 5), invalid), Iterations));
    }

    [Fact]
    public void Warnings_of_an_entry_are_prefixed_with_its_name()
    {
        var mixed = Attacker("Mixte", 5) with { Weapons = [.. Attacker("x", 5).Weapons, .. Attacker("y", 3, WeaponType.Melee).Weapons] };
        var comparison = Of(Attacker("A", 5), mixed) with { SnapShooting = true };

        var warning = Assert.Single(comparison.Warnings());
        Assert.StartsWith("Mixte : ", warning);
    }

    [Fact]
    public void Each_scenario_has_its_own_situation_and_the_common_overwatch()
    {
        var comparison = new Comparison
        {
            Defender = Squad,
            SnapShooting = true,
            Entries =
            [
                new ComparisonEntry { Attacker = Attacker("A", 5), Context = new AttackContext { HalfRange = true } },
                new ComparisonEntry { Attacker = Attacker("B", 10) },
            ],
        };

        var scenarios = comparison.ToScenarios();

        Assert.Equal(["A", "B"], scenarios.Select(s => s.Attacker.Name));
        Assert.Equal([true, false], scenarios.Select(s => s.Context.HalfRange));
        Assert.All(scenarios, s => Assert.True(s.Context.SnapShooting));
        Assert.All(scenarios, s => Assert.Equal("Gardes", s.Defender.Groups[0].Name));
    }

    [Fact]
    public void Overwatch_in_the_situation_of_an_entry_is_rejected()
    {
        var comparison = Of(Attacker("A", 5), Attacker("B", 10));
        comparison = comparison with { Entries = [comparison.Entries[0] with { Context = new AttackContext { SnapShooting = true } }, comparison.Entries[1]] };

        var error = Assert.Single(comparison.Validate());
        Assert.StartsWith("A : ", error);
        Assert.Contains("commun", error);
    }

    // JSON

    [Fact]
    public void Json_round_trip_keeps_the_comparison()
    {
        var comparison = Of(Attacker("A", 5), Attacker("B", 10)) with { SnapShooting = true };
        comparison = comparison with { Entries = [comparison.Entries[0] with { Context = new AttackContext { Charged = true } }, comparison.Entries[1]] };
        string json = ComparisonJson.Serialize(comparison);

        var copy = ComparisonJson.Deserialize(json);

        Assert.Equal(json, ComparisonJson.Serialize(copy));
        Assert.True(copy.SnapShooting);
        Assert.True(copy.Entries[0].Context.Charged);
        var root = JsonNode.Parse(json)!.AsObject();
        Assert.Equal(["defender", "snapShooting", "entries"], root.Select(p => p.Key));
        Assert.Equal(["attacker", "context"], root["entries"]![0]!.AsObject().Select(p => p.Key));
    }

    [Fact]
    public void Overwatch_and_situations_are_optional()
    {
        var root = JsonNode.Parse(ComparisonJson.Serialize(Of(Attacker("A", 5), Attacker("B", 10))))!.AsObject();
        root.Remove("snapShooting");
        root["entries"]![0]!.AsObject().Remove("context");

        var comparison = ComparisonJson.Deserialize(root.ToJsonString());
        Assert.False(comparison.SnapShooting);
        Assert.False(comparison.Entries[0].Context.HalfRange);
    }

    [Theory]
    [InlineData("defender")]
    [InlineData("entries")]
    public void A_missing_section_is_rejected(string section)
    {
        var root = JsonNode.Parse(ComparisonJson.Serialize(Of(Attacker("A", 5), Attacker("B", 10))))!.AsObject();
        root.Remove(section);

        Assert.Throws<JsonException>(() => ComparisonJson.Deserialize(root.ToJsonString()));
    }

    [Fact]
    public void Null_entries_or_an_entry_without_attacker_are_rejected()
    {
        var root = JsonNode.Parse(ComparisonJson.Serialize(Of(Attacker("A", 5), Attacker("B", 10))))!.AsObject();
        root["entries"] = null;
        Assert.Throws<JsonException>(() => ComparisonJson.Deserialize(root.ToJsonString()));

        root = JsonNode.Parse(ComparisonJson.Serialize(Of(Attacker("A", 5), Attacker("B", 10))))!.AsObject();
        root["entries"]![0]!.AsObject().Remove("attacker");
        Assert.Throws<JsonException>(() => ComparisonJson.Deserialize(root.ToJsonString()));
    }

    [Fact]
    public void An_unknown_property_is_rejected()
    {
        var root = JsonNode.Parse(ComparisonJson.Serialize(Of(Attacker("A", 5), Attacker("B", 10))))!.AsObject();
        root["attacker"] = new JsonObject();

        Assert.Throws<JsonException>(() => ComparisonJson.Deserialize(root.ToJsonString()));
    }

    // Lancement

    [Fact]
    public void Each_entry_is_identical_to_a_single_simulation_with_the_same_seed()
    {
        var comparison = Of(Attacker("A", 5), Attacker("B", 10));

        var result = Comparator.Run(comparison, Iterations, seed: 42);

        Assert.Equal(42, result.Seed);
        Assert.Equal(Iterations, result.Iterations);
        var scenarios = comparison.ToScenarios();
        for (int i = 0; i < scenarios.Count; i++)
        {
            var alone = Simulator.Run(scenarios[i], Iterations, seed: 42);
            Assert.Equal(Comparable(alone.Data), Comparable(result.Entries[i].Data));
        }
    }

    [Fact]
    public void Without_seed_every_entry_gets_the_same_one()
    {
        var result = Comparator.Run(Of(Attacker("A", 5), Attacker("B", 10), Attacker("C", 3)), 1_000);

        Assert.All(result.Entries, e => Assert.Equal(result.Seed, e.Seed));
    }

    [Fact]
    public void Progress_increases_up_to_one_over_all_entries()
    {
        var progress = new RecordingProgress<RoundProgress>();

        Comparator.Run(Of(Attacker("A", 5), Attacker("B", 10), Attacker("C", 3)), 3 * Simulator.ChunkSize, seed: 1, progress: progress);

        var values = progress.Values;
        Assert.Equal(9, values.Count);
        Assert.All(values, v => Assert.Equal((1, 1), (v.Round, v.RoundCount)));
        Assert.True(values.Zip(values.Skip(1)).All(pair => pair.Second.Fraction > pair.First.Fraction));
        Assert.Equal(1.0, values[^1].Fraction);
    }

    [Fact]
    public void A_cancelled_comparison_gives_no_result()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            Comparator.Run(Of(Attacker("A", 5), Attacker("B", 10)), Iterations, cancellationToken: cancellation.Token));
    }

    // Classement

    [Fact]
    public void Entries_are_ranked_by_efficiency_score()
    {
        var result = Comparator.Run(Of(Attacker("Faible", 3), Attacker("Fort", 20), Attacker("Moyen", 8)), Iterations, seed: 7);

        Assert.Equal(["Fort", "Moyen", "Faible"], result.Ranking.Select(r => r.Name));
        Assert.Equal([1, 2, 3], result.Ranking.Select(r => r.Rank));
        Assert.Equal([1, 2, 0], result.Ranking.Select(r => r.Index));
        Assert.All(result.Ranking, r => Assert.False(r.TiedWithPrevious));
        Assert.Equal([1], result.BestScore);
        Assert.Equal([1], result.BestMeanKills);
        Assert.Equal([1], result.BestMeanDamage);
        Assert.Equal([1], result.BestWipe);
    }

    [Fact]
    public void Identical_attackers_are_tied()
    {
        // Même graine, même profil : résultats identiques, écart nul
        var result = Comparator.Run(Of(Attacker("Faible", 3), Attacker("Fort", 20), Attacker("Fort bis", 20)), Iterations, seed: 7);

        Assert.Equal(["Fort", "Fort bis", "Faible"], result.Ranking.Select(r => r.Name));
        Assert.True(result.Ranking[1].TiedWithPrevious);
        Assert.False(result.Ranking[2].TiedWithPrevious);
        Assert.Equal([1, 2], result.BestScore);
    }

    [Fact]
    public void A_probability_reached_by_everyone_is_a_tie()
    {
        var result = Comparator.Run(Of(Attacker("Faible", 3), Attacker("Fort", 20)), Iterations, seed: 7);

        Assert.Equal([0, 1], result.BestKillsAtLeast(0));
        Assert.Equal([1], result.BestKillsAtLeast(5));
        Assert.Equal([1], result.BestDamageAtLeast(5));
    }

    [Fact]
    public void The_podium_always_holds_three_entries()
    {
        var result = Comparator.Run(
            Of(Attacker("A", 3), Attacker("B", 20), Attacker("C", 12), Attacker("D", 6), Attacker("E", 1)), Iterations, seed: 7);

        Assert.Equal(["B", "C", "D"], result.Podium.Select(r => r.Name));
        Assert.False(result.PodiumBoundaryTied);
    }

    [Fact]
    public void A_fourth_tied_with_the_third_is_reported()
    {
        var result = Comparator.Run(Of(Attacker("A", 20), Attacker("B", 12), Attacker("C", 6), Attacker("C bis", 6)), Iterations, seed: 7);

        Assert.Equal(["A", "B", "C"], result.Podium.Select(r => r.Name));
        Assert.True(result.PodiumBoundaryTied);
    }

    [Fact]
    public void With_two_entries_the_podium_holds_both()
    {
        var result = Comparator.Run(Of(Attacker("A", 3), Attacker("B", 20)), Iterations, seed: 7);

        Assert.Equal(2, result.Podium.Count);
        Assert.False(result.PodiumBoundaryTied);
    }

    [Fact]
    public void A_small_gap_within_the_noise_is_a_tie()
    {
        Assert.True(ComparisonResult.AreTied(50, 1, 52, 1));    // écart 2 ≤ 2 × √2
        Assert.False(ComparisonResult.AreTied(50, 1, 53, 1));   // écart 3 > 2 × √2
        Assert.True(ComparisonResult.AreTied(1, 0, 1, 0));
        Assert.False(ComparisonResult.AreTied(1, 0, 0.99, 0));
    }

    // Données

    [Fact]
    public void The_result_is_rebuilt_from_its_raw_data()
    {
        var result = Comparator.Run(Of(Attacker("A", 3), Attacker("B", 20), Attacker("C", 8)), Iterations, seed: 7);

        var copy = new ComparisonResult(result.Data);

        Assert.Equal(result.Ranking.Select(r => r.Name), copy.Ranking.Select(r => r.Name));
        Assert.Equal(ComparisonJson.Serialize(result.Comparison), ComparisonJson.Serialize(copy.Comparison));
    }

    [Fact]
    public void Inconsistent_data_are_rejected()
    {
        var result = Comparator.Run(Of(Attacker("A", 3), Attacker("B", 20)), 1_000, seed: 7);
        var other = Comparator.Run(Of(Attacker("A", 3), Attacker("B", 20)), 1_000, seed: 8);
        var otherDefender = Comparator.Run(Of(Attacker("A", 3), Attacker("B", 20)) with { Defender = Build.Horde() }, 1_000, seed: 7);

        Assert.Contains("graine différente", Errors(result.Data.Entries[0], other.Data.Entries[1]));
        Assert.Contains("défenseur différent", Errors(result.Data.Entries[0], otherDefender.Data.Entries[1]));
        Assert.Contains("unique", Errors(result.Data.Entries[0], result.Data.Entries[0]));
        Assert.Contains("au moins 2", Errors(result.Data.Entries[0]));
        Assert.Throws<ArgumentException>(() => new ComparisonResult(result.Data with { Entries = [result.Data.Entries[0]] }));
    }

    private static string Errors(params ComparisonEntryData[] entries) =>
        string.Join(" | ", new ComparisonData { Rounds = [1_000], Elapsed = TimeSpan.Zero, Entries = entries }.Validate());

    /// <summary>JSON des données sans la durée, seule à changer d'un calcul à l'autre.</summary>
    private static string Comparable(SimulationData data) => SimulationJson.Serialize(data with { Elapsed = TimeSpan.Zero });
}
