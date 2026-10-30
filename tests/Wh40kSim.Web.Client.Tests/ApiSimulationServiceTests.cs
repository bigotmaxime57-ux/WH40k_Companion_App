using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Wh40kSim.Core.Model;
using Wh40kSim.Simulation;
using Wh40kSim.UI.Services;
using Wh40kSim.Web.Client.Services;

namespace Wh40kSim.Web.Client.Tests;

/// <summary>Simulation par l'API, face à un faux serveur : flux SSE, refus, pannes, annulation, plafond.</summary>
public class ApiSimulationServiceTests
{
    private static Scenario Scenario() => new()
    {
        Attacker = new AttackerUnit { Weapons = [new WeaponGroup { ModelCount = 10, Weapon = new WeaponProfile { Attacks = 2, Skill = 3 } }] },
        Defender = new DefenderUnit { Groups = [new ModelGroup { Name = "Cibles", Count = 10, Save = 4 }] },
    };

    private static string Sse(params (string Type, string Data)[] events) =>
        string.Concat(events.Select(e => $"event: {e.Type}\ndata: {e.Data}\n\n"));

    private static (ApiSimulationService Service, FakeHandler Handler) Create(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        var handler = new FakeHandler(respond);
        return (new ApiSimulationService(new HttpClient(handler) { BaseAddress = new Uri("http://serveur/") }), handler);
    }

    private static HttpResponseMessage EventStream(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "text/event-stream"),
    };

    [Fact]
    public async Task Queue_positions_progress_and_result_are_forwarded()
    {
        var expected = Simulator.Run(Scenario(), 5_000, seed: 4);
        var (service, handler) = Create((_, _) => Task.FromResult(EventStream(Sse(
            ("queued", "2"), ("queued", "1"), ("progress", "0"), ("progress", "0.5"), ("progress", "1"),
            ("result", SimulationJson.Serialize(expected.Data))))));
        var positions = new List<int>();
        var progress = new List<double>();

        var result = await service.RunAsync(Scenario(), 5_000, seed: 4, new SyncProgress<double>(progress.Add), queuePosition: new SyncProgress<int>(positions.Add));

        Assert.Equal([2, 1], positions);
        Assert.Equal([0, 0.5, 1], progress);
        Assert.Equal(expected.Kills.Counts, result.Kills.Counts);
        Assert.Equal(4, result.Seed);
    }

    [Fact]
    public async Task The_request_carries_the_scenario_iterations_and_seed()
    {
        var expected = Simulator.Run(Scenario(), 1_000, seed: 7);
        var (service, handler) = Create((_, _) => Task.FromResult(EventStream(Sse(("result", SimulationJson.Serialize(expected.Data))))));

        await service.RunAsync(Scenario(), 1_000, seed: 7);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("http://serveur/api/simulations", handler.Request.RequestUri!.ToString());
        var body = JsonNode.Parse(handler.Body!)!;
        Assert.Equal(1_000, body["iterations"]!.GetValue<int>());
        Assert.Equal(7, body["seed"]!.GetValue<int>());
        Assert.Equal("Cibles", body["scenario"]!["defender"]!["groups"]![0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_rejection_becomes_a_service_exception_with_the_server_errors()
    {
        var (service, _) = Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"title":"Demande refusée","status":400,"errors":["Erreur 1","Erreur 2"]}""", Encoding.UTF8, "application/problem+json"),
        }));

        var error = await Assert.ThrowsAsync<SimulationServiceException>(() => service.RunAsync(Scenario(), 10));

        Assert.Equal("Demande refusée", error.Title);
        Assert.Equal(["Erreur 1", "Erreur 2"], error.Errors);
    }

    [Fact]
    public async Task Too_many_launches_keep_the_server_message()
    {
        var (service, _) = Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"title":"Trop de lancements","errors":["Réessayez dans 30 s."]}""", Encoding.UTF8, "application/problem+json"),
        }));

        var error = await Assert.ThrowsAsync<SimulationServiceException>(() => service.RunAsync(Scenario(), 10));

        Assert.Equal("Trop de lancements", error.Title);
        Assert.Contains("30 s", error.Message);
    }

    [Fact]
    public async Task An_error_event_becomes_a_service_exception()
    {
        var (service, _) = Create((_, _) => Task.FromResult(EventStream(Sse(("progress", "0"), ("error", "Le calcul a échoué sur le serveur.")))));

        var error = await Assert.ThrowsAsync<SimulationServiceException>(() => service.RunAsync(Scenario(), 10));

        Assert.Equal("Échec du calcul", error.Title);
    }

    [Fact]
    public async Task A_stream_ending_without_result_is_reported()
    {
        var (service, _) = Create((_, _) => Task.FromResult(EventStream(Sse(("progress", "0")))));

        var error = await Assert.ThrowsAsync<SimulationServiceException>(() => service.RunAsync(Scenario(), 10));

        Assert.Equal("Réponse incomplète du serveur", error.Title);
    }

    [Fact]
    public async Task An_unreachable_server_is_reported()
    {
        var (service, _) = Create((_, _) => throw new HttpRequestException("Connexion refusée"));

        var error = await Assert.ThrowsAsync<SimulationServiceException>(() => service.RunAsync(Scenario(), 10));

        Assert.Equal("Serveur injoignable", error.Title);
    }

    [Fact]
    public async Task Cancelling_closes_the_request()
    {
        using var cancellation = new CancellationTokenSource();
        var (service, _) = Create(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return EventStream("");
        });

        var run = service.RunAsync(Scenario(), 10, cancellationToken: cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task The_limit_is_asked_to_the_server()
    {
        var (service, handler) = Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"maxIterations":1000000}""", Encoding.UTF8, "application/json"),
        }));
        Assert.Null(service.MaxIterations);

        await service.LoadLimitsAsync();

        Assert.Equal(1_000_000, service.MaxIterations);
        Assert.Equal("http://serveur/api/simulations/limits", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Without_an_answer_the_limit_stays_unknown()
    {
        var (service, _) = Create((_, _) => throw new HttpRequestException("Connexion refusée"));

        await service.LoadLimitsAsync();

        Assert.Null(service.MaxIterations);
    }

    /// <summary>Faux serveur : répond par la fonction donnée et garde la dernière demande et son corps.</summary>
    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await respond(request, cancellationToken);
        }
    }

    /// <summary>Progression rapportée tout de suite (Progress&lt;T&gt; la transmettrait plus tard).</summary>
    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
