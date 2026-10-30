using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Wh40kSim.Core.Serialization;
using Wh40kSim.Simulation;

namespace Wh40kSim.Web.Tests;

/// <summary>API de simulation : flux SSE (attente, progression, résultat, erreur), refus en ProblemDetails, file, annulation.</summary>
public class SimulationApiTests
{
    [Fact]
    public async Task A_valid_request_streams_progress_then_the_same_result_as_the_simulator()
    {
        await using var factory = Api.Create();
        var scenario = Api.Scenario();

        await using var reader = new SseReader(await Api.PostAsync(factory.CreateClient(), Api.Body(scenario, 30_000, seed: 5)));
        var events = await reader.ReadAllAsync();

        Assert.All(events[..^1], e => Assert.Equal(SimulationEndpoints.ProgressEvent, e.Type));
        Assert.Equal("0", events[0].Data);
        var progress = events[..^1].Select(e => double.Parse(e.Data, CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(progress.Order(), progress);
        Assert.Equal(1.0, progress[^1]);

        Assert.Equal(SimulationEndpoints.ResultEvent, events[^1].Type);
        var result = new SimulationResult(SimulationJson.Deserialize(events[^1].Data));
        var expected = Simulator.Run(scenario, 30_000, seed: 5);
        Assert.Equal(expected.Kills.Counts, result.Kills.Counts);
        Assert.Equal(expected.Damage.Counts, result.Damage.Counts);
        Assert.Equal(5, result.Seed);
    }

    [Fact]
    public async Task The_response_is_an_event_stream()
    {
        await using var factory = Api.Create();

        using var response = await Api.PostAsync(factory.CreateClient(), Api.Body(Api.Scenario(), 1_000));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(SseReader.IsEventStream(response));
    }

    [Theory]
    [InlineData("{ pas du json")]
    [InlineData("{ \"scenario\": {}, \"iterations\": 10, \"inconnu\": 1 }")]
    [InlineData("{ \"scenario\": {} }")]
    public async Task An_unreadable_request_is_rejected(string body)
    {
        await using var factory = Api.Create();

        using var response = await Api.PostAsync(factory.CreateClient(), body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var (title, errors) = await Api.ProblemAsync(response);
        Assert.Equal("Demande illisible", title);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task An_invalid_scenario_is_rejected_with_its_errors()
    {
        await using var factory = Api.Create();
        var scenario = Api.Scenario() with { Defender = new() };

        using var response = await Api.PostAsync(factory.CreateClient(), Api.Body(scenario, 1_000));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var (title, errors) = await Api.ProblemAsync(response);
        Assert.Equal("Scénario invalide", title);
        Assert.Contains(errors, e => e.Contains("au moins un groupe"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_000_001)]
    public async Task Iterations_outside_the_limits_are_rejected(int iterations)
    {
        await using var factory = Api.Create();

        using var response = await Api.PostAsync(factory.CreateClient(), Api.Body(Api.Scenario(), iterations));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var (title, errors) = await Api.ProblemAsync(response);
        Assert.Equal("Nombre de simulations refusé", title);
        Assert.Contains("1 000 000", errors[0].Replace(' ', ' ').Replace(' ', ' '));
    }

    [Fact]
    public async Task Launches_beyond_the_limit_per_minute_are_rejected()
    {
        await using var factory = Api.Create(launchesPerMinute: 2);
        var client = factory.CreateClient();
        string body = Api.Body(Api.Scenario(), 1_000);

        for (int i = 0; i < 2; i++)
        {
            using var accepted = await Api.PostAsync(client, body);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }
        using var rejected = await Api.PostAsync(client, body);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
        var (title, _) = await Api.ProblemAsync(rejected);
        Assert.Equal("Trop de lancements", title);
    }

    [Fact]
    public async Task A_second_request_waits_its_turn_then_gets_its_result()
    {
        var runner = new ControlledRunner();
        await using var factory = Api.Create(runner: runner);
        var client = factory.CreateClient();
        string body = Api.Body(Api.Scenario(), 1_000, seed: 1);

        await using var first = new SseReader(await Api.PostAsync(client, body));
        Assert.Equal((SimulationEndpoints.ProgressEvent, "0"), await first.NextAsync());
        await Wait.ForAsync(runner.Started);

        await using var second = new SseReader(await Api.PostAsync(client, body));
        Assert.Equal((SimulationEndpoints.QueuedEvent, "1"), await second.NextAsync());

        runner.Release();
        Assert.Equal(SimulationEndpoints.ResultEvent, (await first.ReadAllAsync())[^1].Type);

        Assert.Equal((SimulationEndpoints.ProgressEvent, "0"), await second.NextAsync());
        runner.Release();
        Assert.Equal(SimulationEndpoints.ResultEvent, (await second.ReadAllAsync())[^1].Type);
    }

    [Fact]
    public async Task Closing_the_request_stops_the_computation_and_frees_the_place()
    {
        var runner = new ControlledRunner();
        await using var factory = Api.Create(runner: runner);
        var client = factory.CreateClient();
        string body = Api.Body(Api.Scenario(), 1_000);

        using var cancellation = new CancellationTokenSource();
        var first = new SseReader(await Api.PostAsync(client, body, cancellation.Token));
        Assert.Equal((SimulationEndpoints.ProgressEvent, "0"), await first.NextAsync());
        await Wait.ForAsync(runner.Started);

        await using var second = new SseReader(await Api.PostAsync(client, body));
        Assert.Equal((SimulationEndpoints.QueuedEvent, "1"), await second.NextAsync());

        cancellation.Cancel();
        await first.DisposeAsync();
        await Wait.ForAsync(runner.Cancelled);

        Assert.Equal((SimulationEndpoints.ProgressEvent, "0"), await second.NextAsync());
        runner.Release();
        Assert.Equal(SimulationEndpoints.ResultEvent, (await second.ReadAllAsync())[^1].Type);
    }

    [Fact]
    public async Task An_unexpected_failure_sends_an_error_event_and_the_server_keeps_working()
    {
        var runner = new ControlledRunner { Fail = true };
        await using var factory = Api.Create(runner: runner);
        var client = factory.CreateClient();
        string body = Api.Body(Api.Scenario(), 1_000);

        await using var failed = new SseReader(await Api.PostAsync(client, body));
        runner.Release();
        var events = await failed.ReadAllAsync();

        Assert.Equal(SimulationEndpoints.ErrorEvent, events[^1].Type);
        Assert.DoesNotContain("Panne", events[^1].Data); // détail technique gardé dans le journal du serveur

        await using var next = new SseReader(await Api.PostAsync(client, body));
        runner.Release();
        Assert.Equal(SimulationEndpoints.ErrorEvent, (await next.ReadAllAsync())[^1].Type);
    }

    [Fact]
    public async Task The_result_holds_the_raw_contract()
    {
        await using var factory = Api.Create();

        await using var reader = new SseReader(await Api.PostAsync(factory.CreateClient(), Api.Body(Api.Scenario(), 1_000, seed: 2)));
        var result = JsonNode.Parse((await reader.ReadAllAsync())[^1].Data)!;

        Assert.NotNull(result["kills"]);
        Assert.NotNull(result["scenario"]!["attacker"]);
        Assert.Equal(2, result["seed"]!.GetValue<int>());
    }
}
