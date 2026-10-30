using System.Text.Json;
using System.Text.Json.Nodes;
using Wh40kSim.Core.Serialization;

namespace Wh40kSim.Simulation.Tests;

/// <summary>
/// Contrat du résultat : les données brutes d'une simulation font l'aller-retour en JSON et redonnent exactement les
/// mêmes statistiques ; des données incohérentes avec leur scénario sont refusées.
/// </summary>
public class SimulationJsonTests
{
    /// <summary>Scénario riche (Precision, deux PERSONNAGES, plusieurs pools, marge, gaspillage), une seule fois.</summary>
    /// <remarks>Assez de simulations pour que l'unité soit parfois détruite (marge non vide : environ 1 sur 10 000).</remarks>
    private static readonly SimulationResult Rich = Simulator.Run(NonRegressionTests.RichScenario(), 200_000, seed: 9);

    [Fact]
    public void Round_trip_gives_exactly_the_same_statistics()
    {
        string json = SimulationJson.Serialize(Rich.Data);

        var copy = new SimulationResult(SimulationJson.Deserialize(json));

        // Même JSON, puis toutes les statistiques publiques, comparées en profondeur (distributions, groupes, pools,
        // conversion, frappe Precision, gardes du corps, gaspillage, marge)
        Assert.Equal(json, SimulationJson.Serialize(copy.Data));
        Assert.Equivalent(Rich, copy, strict: true);
        Assert.Equal(Rich.Seed, copy.Seed);
        Assert.Equal(Rich.Elapsed, copy.Elapsed);
        Assert.Equal(ScenarioJson.Serialize(Rich.Scenario), ScenarioJson.Serialize(copy.Scenario));
    }

    [Fact]
    public void The_scenario_covers_every_part_of_the_contract()
    {
        Assert.NotNull(Rich.PrecisionTarget?.PrecisionWeapons);
        Assert.NotNull(Rich.BodyguardKills);
        Assert.True(Rich.Pools.Count >= 2);
        Assert.All(Rich.Pools, p => Assert.NotNull(p.Conversion));
        Assert.True(Rich.Waste.MaxValue > 0);
        Assert.True(Rich.ExtendedKills.MaxValue > Rich.TotalModels);
    }

    [Fact]
    public void The_contract_is_written_on_a_single_line()
    {
        string json = SimulationJson.Serialize(Rich.Data);

        Assert.DoesNotContain('\n', json);
        Assert.DoesNotContain('\r', json);
    }

    [Fact]
    public void The_contract_holds_only_raw_counters()
    {
        var root = JsonNode.Parse(SimulationJson.Serialize(Rich.Data))!.AsObject();

        Assert.Equal(Rich.Kills.Counts, root["kills"]!.AsArray().Select(n => n!.GetValue<long>()));
        Assert.Null(root["iterations"]);
        Assert.Null(root["mean"]);
        Assert.NotNull(root["scenario"]!["attacker"]);
    }

    [Fact]
    public void A_histogram_of_the_wrong_size_is_rejected()
    {
        var data = Rich.Data with { Kills = Rich.Data.Kills[..^1] };

        var error = Assert.Throws<JsonException>(() => SimulationJson.Deserialize(SimulationJson.Serialize(data)));
        Assert.Contains("Figurines tuées", error.Message);
    }

    [Fact]
    public void An_extra_group_is_rejected()
    {
        var data = Rich.Data with { Groups = [.. Rich.Data.Groups, Rich.Data.Groups[0]] };

        var error = Assert.Throws<JsonException>(() => SimulationJson.Deserialize(SimulationJson.Serialize(data)));
        Assert.Contains("Groupes", error.Message);
    }

    [Fact]
    public void Inconsistent_simulation_counts_are_rejected()
    {
        var pool = Rich.Data.Pools[0];
        var data = Rich.Data with { Pools = [pool with { Kills = [.. pool.Kills.Select(c => c * 2)] }, .. Rich.Data.Pools.Skip(1)] };

        var error = Assert.Throws<ArgumentException>(() => new SimulationResult(data));
        Assert.Contains("Pool 1", error.Message);
    }

    [Theory]
    [InlineData("unitSurvives")]
    [InlineData("groups")]
    public void A_missing_property_is_rejected(string property)
    {
        var root = JsonNode.Parse(SimulationJson.Serialize(Rich.Data))!.AsObject();
        root.Remove(property);

        Assert.Throws<JsonException>(() => SimulationJson.Deserialize(root.ToJsonString()));
    }

    [Fact]
    public void An_unknown_property_is_rejected()
    {
        var root = JsonNode.Parse(SimulationJson.Serialize(Rich.Data))!.AsObject();
        root["mean"] = 1.5;

        Assert.Throws<JsonException>(() => SimulationJson.Deserialize(root.ToJsonString()));
    }
}
