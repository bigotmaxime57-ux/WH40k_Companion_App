using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Wh40kSim.Core.Model;
using Wh40kSim.Simulation;
using Wh40kSim.Simulation.Comparisons;

namespace Wh40kSim.Web.Tests;

/// <summary>
/// API de comparaison : flux SSE (progression par ronde, résultat identique au comparateur), refus en ProblemDetails
/// (demande illisible, comparaison invalide, limites du site), même file que les simulations, annulation, panne.
/// </summary>
public class ComparisonApiTests
{
    [Fact]
    public async Task A_valid_request_streams_round_progress_then_the_same_result_as_the_comparator()
    {
        await using var factory = Api.Create();
        var comparison = Api.ComparisonOf();

        await using var reader = new SseReader(await Api.PostComparisonAsync(factory.CreateClient(), Api.ComparisonBody(comparison, 20_000, seed: 5)));
        var events = await reader.ReadAllAsync();

        Assert.All(events[..^1], e => Assert.Equal(SimulationEndpoints.ProgressEvent, e.Type));
        var first = JsonNode.Parse(events[0].Data)!;
        Assert.Equal(1, first["round"]!.GetValue<int>());
        Assert.Equal(0, first["fraction"]!.GetValue<double>());

        Assert.Equal(SimulationEndpoints.ResultEvent, events[^1].Type);
        var result = new ComparisonResult(ComparisonDataJson.Deserialize(events[^1].Data));
        var expected = Comparator.Run(comparison, 20_000, seed: 5);
        Assert.Equal(expected.Ranking.Select(r => r.Name), result.Ranking.Select(r => r.Name));
        for (int i = 0; i < expected.Entries.Count; i++)
        {
            Assert.Equal(expected.Entries[i].Kills.Counts, result.Entries[i].Kills.Counts);
        }
        Assert.Equal(5, result.Seed);
    }

    [Theory]
    [InlineData("{ pas du json")]
    [InlineData("{ \"comparison\": {}, \"iterations\": 10 }")]
    [InlineData("{ \"comparison\": { \"defender\": {}, \"entries\": null }, \"iterations\": 10 }")]
    public async Task An_unreadable_request_is_rejected(string body)
    {
        await using var factory = Api.Create();

        using var response = await Api.PostComparisonAsync(factory.CreateClient(), body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Demande illisible", (await Api.ProblemAsync(response)).Title);
    }

    [Fact]
    public async Task An_invalid_comparison_is_rejected_with_its_errors()
    {
        await using var factory = Api.Create();

        using var response = await Api.PostComparisonAsync(factory.CreateClient(), Api.ComparisonBody(Api.ComparisonOf(Api.Attacker("Seul", 5)), 1_000));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var (title, errors) = await Api.ProblemAsync(response);
        Assert.Equal("Comparaison invalide", title);
        Assert.Contains(errors, e => e.Contains("au moins 2"));
    }

    [Fact]
    public async Task Too_many_iterations_or_variants_are_refused()
    {
        await using var factory = Api.Create();
        var crowd = Api.ComparisonOf([.. Enumerable.Range(1, 51).Select(i => Api.Attacker($"A{i}", i % 10 + 1))]);

        using var response = await Api.PostComparisonAsync(factory.CreateClient(), Api.ComparisonBody(crowd, 1_000_001));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var (title, errors) = await Api.ProblemAsync(response);
        Assert.Equal("Limites du site dépassées", title);
        Assert.Contains(errors, e => e.StartsWith("Nombre de simulations"));
        Assert.Contains(errors, e => e.Contains("au plus 50") && e.Contains("51"));
    }

    [Fact]
    public async Task The_limits_include_the_comparison()
    {
        await using var factory = Api.Create();

        var limits = await factory.CreateClient().GetFromJsonAsync<JsonObject>("/api/simulations/limits");

        Assert.Equal(1_000_000, limits!["maxIterations"]!.GetValue<int>());
        Assert.Equal(50, limits["maxComparisonVariants"]!.GetValue<int>());
    }

    [Fact]
    public async Task A_comparison_waits_behind_a_simulation_then_gets_its_result_with_the_finalist_cap()
    {
        var runner = new ControlledRunner();
        await using var factory = Api.Create(runner: runner);
        var client = factory.CreateClient();

        await using var simulation = new SseReader(await Api.PostAsync(client, Api.Body(Api.Scenario(), 1_000, seed: 1)));
        Assert.Equal((SimulationEndpoints.ProgressEvent, "0"), await simulation.NextAsync());
        await Wait.ForAsync(runner.Started);

        await using var comparison = new SseReader(await Api.PostComparisonAsync(client, Api.ComparisonBody(Api.ComparisonOf(), 1_000, seed: 1)));
        Assert.Equal((SimulationEndpoints.QueuedEvent, "1"), await comparison.NextAsync());

        runner.Release();
        Assert.Equal(SimulationEndpoints.ResultEvent, (await simulation.ReadAllAsync())[^1].Type);

        Assert.Equal(SimulationEndpoints.ProgressEvent, (await comparison.NextAsync())!.Value.Type);
        runner.Release();
        Assert.Equal(SimulationEndpoints.ResultEvent, (await comparison.ReadAllAsync())[^1].Type);
        Assert.Equal(5, runner.LastMaxFinalists);
    }

    [Fact]
    public async Task Closing_the_request_stops_the_comparison()
    {
        var runner = new ControlledRunner();
        await using var factory = Api.Create(runner: runner);
        using var cancellation = new CancellationTokenSource();

        var reader = new SseReader(await Api.PostComparisonAsync(factory.CreateClient(), Api.ComparisonBody(Api.ComparisonOf(), 1_000), cancellation.Token));
        Assert.Equal(SimulationEndpoints.ProgressEvent, (await reader.NextAsync())!.Value.Type);
        await Wait.ForAsync(runner.Started);

        cancellation.Cancel();
        await reader.DisposeAsync();
        await Wait.ForAsync(runner.Cancelled);
    }

    [Fact]
    public async Task An_unexpected_failure_sends_an_error_event()
    {
        var runner = new ControlledRunner { Fail = true };
        await using var factory = Api.Create(runner: runner);

        await using var reader = new SseReader(await Api.PostComparisonAsync(factory.CreateClient(), Api.ComparisonBody(Api.ComparisonOf(), 1_000)));
        runner.Release();

        Assert.Equal(SimulationEndpoints.ErrorEvent, (await reader.ReadAllAsync())[^1].Type);
    }
}
