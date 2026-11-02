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
        Assert.Equal(["attacker", "context", "alternatives"], root["entries"]![0]!.AsObject().Select(p => p.Key));
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
        var data = Comparator.Run(Of(Attacker("A", 3), Attacker("B", 20)), 1_000, seed: 7).Data;
        var other = Comparator.Run(Of(Attacker("A", 3), Attacker("B", 20)), 1_000, seed: 8).Data;
        var otherDefender = Comparator.Run(Of(Attacker("A", 3), Attacker("B", 20)) with { Defender = Build.Horde() }, 1_000, seed: 7).Data;

        Assert.Empty(data.Validate());
        Assert.Contains("graine différente", Errors(data with { Entries = [data.Entries[0], other.Entries[1]] }));
        Assert.Contains("scénario différent", Errors(data with { Entries = [data.Entries[0], otherDefender.Entries[1]] }));
        Assert.Contains("une par variante", Errors(data with { Entries = [data.Entries[0]] }));
        Assert.Contains("unique", Errors(data with { Comparison = Of(Attacker("A", 3), Attacker("A", 20)) }));
        Assert.Throws<ArgumentException>(() => new ComparisonResult(data with { Entries = [data.Entries[0]] }));
    }

    private static string Errors(ComparisonData data) => string.Join(" | ", data.Validate());

    // Armes à plusieurs profils

    private static WeaponProfile Profile(string name, int attacks, int strength = 4) => new() { Name = name, Attacks = attacks, Skill = 3, Strength = strength };

    /// <summary>Attaquant à deux armes : Plasma (2 profils de rechange) et Pistolet (1 profil de rechange).</summary>
    private static ComparisonEntry Hellblasters => new()
    {
        Attacker = new AttackerUnit
        {
            Name = "Hellblasters",
            Weapons =
            [
                new WeaponGroup { ModelCount = 5, Weapon = Profile("Plasma", 2, 7) },
                new WeaponGroup { ModelCount = 1, Weapon = Profile("Pistolet", 1) },
            ],
        },
        Context = new AttackContext { HalfRange = true },
        Alternatives =
        [
            new WeaponAlternatives { Weapon = 0, Profiles = [Profile("Plasma (surchargé)", 2, 8), Profile("Plasma (rafale)", 3, 7)] },
            new WeaponAlternatives { Weapon = 1, Profiles = [Profile("Pistolet (surchargé)", 1, 8)] },
        ],
    };

    [Fact]
    public void An_entry_unfolds_into_every_combination_of_profiles()
    {
        var comparison = new Comparison { Defender = Squad, Entries = [Hellblasters, new ComparisonEntry { Attacker = Attacker("Bolters", 10) }] };

        var variants = comparison.Variants();

        Assert.Equal(7, variants.Count);   // 3 × 2 combinaisons + Bolters
        Assert.Equal("Hellblasters · Plasma, Pistolet", variants[0].Name);
        Assert.Equal("Hellblasters · Plasma (rafale), Pistolet (surchargé)", variants[5].Name);
        Assert.Equal("Bolters", variants[6].Name);
        Assert.Equal(["Plasma (rafale)", "Pistolet (surchargé)"], variants[5].Scenario.Attacker.Weapons.Select(w => w.Weapon.Name));
        Assert.Equal([5, 1], variants[5].Scenario.Attacker.Weapons.Select(w => w.ModelCount));
        Assert.All(variants.Take(6), v => Assert.Equal(0, v.Entry));
        Assert.All(variants.Take(6), v => Assert.True(v.Scenario.Context.HalfRange));
        Assert.Equal([new ProfileChoice(0, "Plasma", "Plasma (rafale)"), new ProfileChoice(1, "Pistolet", "Pistolet (surchargé)")], variants[5].Choices);
        Assert.Empty(variants[6].Choices);
        Assert.Empty(comparison.Validate());
    }

    [Fact]
    public void A_single_attacker_with_alternative_profiles_is_enough()
    {
        Assert.Empty(new Comparison { Defender = Squad, Entries = [Hellblasters] }.Validate());
        Assert.Contains("au moins 2", Assert.Single(Of(Attacker("A", 3)).Validate()));
    }

    [Fact]
    public void Invalid_alternative_profiles_are_rejected()
    {
        var entry = Hellblasters;
        string Errors(params WeaponAlternatives[] alternatives) =>
            string.Join(" | ", new Comparison { Defender = Squad, Entries = [entry with { Alternatives = alternatives }] }.Validate());

        Assert.Contains("n'existe pas", Errors(new WeaponAlternatives { Weapon = 5, Profiles = [Profile("X", 1)] }));
        Assert.Contains("aucun", Errors(new WeaponAlternatives { Weapon = 0, Profiles = [] }));
        Assert.Contains("s'appellent 'Plasma'", Errors(new WeaponAlternatives { Weapon = 0, Profiles = [Profile("plasma ", 3)] }));
        Assert.Contains("CT/CC", Errors(new WeaponAlternatives { Weapon = 0, Profiles = [Profile("Plasma (cassé)", 2) with { Skill = 9 }] }));
        Assert.Contains("plusieurs fois", Errors(
            new WeaponAlternatives { Weapon = 0, Profiles = [Profile("P2", 1)] }, new WeaponAlternatives { Weapon = 0, Profiles = [Profile("P3", 1)] }));
    }

    [Fact]
    public void Alternative_profiles_survive_the_json_round_trip()
    {
        var comparison = new Comparison { Defender = Squad, Entries = [Hellblasters] };
        string json = ComparisonJson.Serialize(comparison);

        var copy = ComparisonJson.Deserialize(json);

        Assert.Equal(json, ComparisonJson.Serialize(copy));
        Assert.Equal(6, copy.Variants().Count);
        var root = JsonNode.Parse(json)!.AsObject();
        Assert.Equal(2, root["entries"]![0]!["alternatives"]!.AsArray().Count);
    }

    [Fact]
    public void Alternative_profiles_are_optional_in_json()
    {
        var root = JsonNode.Parse(ComparisonJson.Serialize(Of(Attacker("A", 5), Attacker("B", 10))))!.AsObject();
        root["entries"]![0]!.AsObject().Remove("alternatives");

        Assert.Empty(ComparisonJson.Deserialize(root.ToJsonString()).Entries[0].Alternatives);
    }

    [Fact]
    public void Variants_compete_and_the_best_profile_of_each_weapon_is_given()
    {
        var entry = new ComparisonEntry
        {
            Attacker = Attacker("Escouade", 3),
            Alternatives = [new WeaponAlternatives { Weapon = 0, Profiles = [Profile("Bolter (rafale)", 12), Profile("Bolter (lourd)", 6)] }],
        };
        var comparison = new Comparison { Defender = Squad, Entries = [entry, new ComparisonEntry { Attacker = Attacker("Autre", 1) }] };

        var result = Comparator.Run(comparison, Iterations, seed: 7);

        Assert.Equal(4, result.Entries.Count);
        Assert.Equal("Escouade · Bolter (rafale)", result.Ranking[0].Name);
        Assert.Equal(["Escouade · Bolter (rafale)", "Escouade · Bolter (lourd)", "Escouade · Bolter"], result.Podium.Select(r => r.Name));
        var best = Assert.Single(result.BestProfilesByAttacker);
        Assert.Equal("Escouade", best.AttackerName);
        Assert.Equal("Bolter (rafale)", Assert.Single(best.Choices).Profile);
        Assert.Equal(1, result.Ranking.Single(r => r.Name == "Autre").Variant.Entry);
        // Données refaites à l'identique depuis le JSON de la définition
        Assert.Equal(result.Ranking.Select(r => r.Name), new ComparisonResult(result.Data).Ranking.Select(r => r.Name));
    }

    [Fact]
    public void The_round_plan_counts_the_variants()
    {
        var comparison = new Comparison { Defender = Squad, Entries = [Hellblasters] };   // 6 variantes

        var result = Comparator.Run(comparison, 100_000, seed: 7);

        Assert.Equal([10_000, 100_000], result.Rounds);
    }

    /// <summary>JSON des données sans la durée, seule à changer d'un calcul à l'autre.</summary>
    private static string Comparable(SimulationData data) => SimulationJson.Serialize(data with { Elapsed = TimeSpan.Zero });
}
