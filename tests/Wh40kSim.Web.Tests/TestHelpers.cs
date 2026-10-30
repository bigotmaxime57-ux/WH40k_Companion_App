using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;
using Wh40kSim.Simulation;

namespace Wh40kSim.Web.Tests;

/// <summary>Serveur de test : limites réglées par test, calcul réel ou remplacé.</summary>
internal static class Api
{
    public static WebApplicationFactory<Program> Create(int launchesPerMinute = 1_000, ISimulationRunner? runner = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Simulation:LaunchesPerMinute", launchesPerMinute.ToString());
            if (runner is not null)
            {
                builder.ConfigureTestServices(services => services.AddSingleton(runner));
            }
        });

    /// <summary>10 tireurs contre 10 cibles : valide et rapide.</summary>
    public static Scenario Scenario() => new()
    {
        Attacker = new AttackerUnit { Weapons = [new WeaponGroup { ModelCount = 10, Weapon = new WeaponProfile { Attacks = 2, Skill = 3 } }] },
        Defender = new DefenderUnit { Groups = [new ModelGroup { Name = "Cibles", Count = 10, Save = 4 }] },
    };

    /// <summary>Corps d'une demande, le scénario au format des fichiers.</summary>
    public static string Body(Scenario scenario, int iterations, int? seed = null)
    {
        var body = new JsonObject
        {
            ["scenario"] = JsonNode.Parse(ScenarioJson.Serialize(scenario)),
            ["iterations"] = iterations,
        };
        if (seed is int value)
        {
            body["seed"] = value;
        }
        return body.ToJsonString();
    }

    /// <summary>Envoie une demande sans attendre la fin de la réponse (flux SSE).</summary>
    public static Task<HttpResponseMessage> PostAsync(HttpClient client, string body, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/simulations")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        return client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    /// <summary>Détails d'un refus : titre et liste des erreurs.</summary>
    public static async Task<(string Title, string[] Errors)> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return (problem["title"]!.GetValue<string>(), problem["errors"]!.AsArray().Select(e => e!.GetValue<string>()).ToArray());
    }
}

/// <summary>Lecteur d'événements Server-Sent Events, un à un.</summary>
internal sealed class SseReader(HttpResponseMessage response) : IAsyncDisposable
{
    private StreamReader? _reader;

    public static bool IsEventStream(HttpResponseMessage response) =>
        response.Content.Headers.ContentType?.MediaType == "text/event-stream";

    /// <summary>Prochain événement, ou null à la fin du flux.</summary>
    public async Task<(string Type, string Data)?> NextAsync()
    {
        _reader ??= new StreamReader(await response.Content.ReadAsStreamAsync());
        string type = "message";
        var data = new List<string>();
        while (await _reader.ReadLineAsync() is { } line)
        {
            if (line.Length == 0)
            {
                if (data.Count > 0)
                {
                    return (type, string.Join("\n", data));
                }
                continue;
            }
            if (line.StartsWith("event:")) type = line["event:".Length..].Trim();
            else if (line.StartsWith("data:")) data.Add(line["data:".Length..].TrimStart());
        }
        return data.Count > 0 ? (type, string.Join("\n", data)) : null;
    }

    /// <summary>Tous les événements restants.</summary>
    public async Task<List<(string Type, string Data)>> ReadAllAsync()
    {
        var events = new List<(string, string)>();
        while (await NextAsync() is { } item)
        {
            events.Add(item);
        }
        return events;
    }

    public ValueTask DisposeAsync()
    {
        _reader?.Dispose();
        response.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Calcul remplacé : chaque appel attend qu'on le libère (<see cref="Release"/>), puis rend un vrai résultat ; ou lève
/// une erreur. Signale son démarrage et son annulation.
/// </summary>
internal sealed class ControlledRunner : ISimulationRunner
{
    private readonly SemaphoreSlim _gate = new(0);

    public bool Fail { get; init; }

    public SemaphoreSlim Started { get; } = new(0);

    public SemaphoreSlim Cancelled { get; } = new(0);

    public void Release() => _gate.Release();

    public async Task<SimulationResult> RunAsync(Scenario scenario, int iterations, int? seed, IProgress<double> progress, CancellationToken cancellationToken)
    {
        Started.Release();
        try
        {
            await _gate.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Cancelled.Release();
            throw;
        }
        if (Fail)
        {
            throw new InvalidOperationException("Panne simulée");
        }
        progress.Report(0.5);
        return Simulator.Run(scenario, iterations, seed, progress, cancellationToken);
    }
}

internal static class Wait
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static async Task ForAsync(SemaphoreSlim signal) => Assert.True(await signal.WaitAsync(Timeout), "Signal attendu non reçu.");
}
