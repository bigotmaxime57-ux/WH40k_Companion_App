using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;
using Wh40kSim.Simulation;
using Wh40kSim.Simulation.Comparisons;
using Wh40kSim.UI.Services;

namespace Wh40kSim.Web.Client.Services;

/// <summary>
/// Simulation et comparaison par l'API du serveur (<c>POST /api/simulations</c>, <c>/api/comparisons</c>) : le flux
/// Server-Sent Events est lu au fil de l'eau
/// (position dans la file, progression), puis les données brutes du résultat redeviennent un
/// <see cref="SimulationResult"/>. Annuler ferme la requête : le serveur arrête le calcul ou retire la demande.
/// Les refus et les pannes deviennent des <see cref="SimulationServiceException"/> (messages en français).
/// </summary>
public sealed class ApiSimulationService(HttpClient http) : ISimulationService
{
    public const string SimulationsPath = "api/simulations";
    public const string LimitsPath = "api/simulations/limits";
    public const string ComparisonsPath = "api/comparisons";

    /// <summary>Pause laissée au navigateur pour afficher la barre à 100 % avant de traiter le résultat.</summary>
    private static readonly TimeSpan ResultPaintDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>Plafond du serveur (null tant qu'il n'est pas connu : le serveur refusera alors au-delà, avec un message).</summary>
    public int? MaxIterations { get; private set; }

    /// <summary>Demande le plafond au serveur (au démarrage du client) ; sans réponse, il reste inconnu.</summary>
    public async Task LoadLimitsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var limits = await http.GetFromJsonAsync<SimulationLimits>(LimitsPath, cancellationToken);
            MaxIterations = limits?.MaxIterations;
            MaxComparisonVariants = limits?.MaxComparisonVariants;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
        {
            MaxIterations = null;
            MaxComparisonVariants = null;
        }
    }

    public Task<SimulationResult> RunAsync(
        Scenario scenario,
        int iterations,
        int? seed = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        IProgress<int>? queuePosition = null)
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
        return StreamAsync(
            SimulationsPath, body, queuePosition,
            data =>
            {
                if (double.TryParse(data, CultureInfo.InvariantCulture, out double done))
                {
                    progress?.Report(done);
                }
            },
            () => progress?.Report(1),
            data => new SimulationResult(SimulationJson.Deserialize(data)),
            cancellationToken);
    }

    /// <summary>Plafond de variantes d'une comparaison sur le serveur (null tant qu'il n'est pas connu).</summary>
    public int? MaxComparisonVariants { get; private set; }

    public bool SupportsComparison => true;

    /// <summary>Comparaison par l'API (<c>POST /api/comparisons</c>), même flux que la simulation, progression par ronde.</summary>
    public Task<ComparisonResult> RunComparisonAsync(
        Comparison comparison,
        int iterations,
        int? seed = null,
        IProgress<RoundProgress>? progress = null,
        CancellationToken cancellationToken = default,
        IProgress<int>? queuePosition = null)
    {
        var body = new JsonObject
        {
            ["comparison"] = JsonNode.Parse(ComparisonJson.Serialize(comparison)),
            ["iterations"] = iterations,
        };
        if (seed is int value)
        {
            body["seed"] = value;
        }
        var last = new RoundProgress(1, 1, 0);
        return StreamAsync(
            ComparisonsPath, body, queuePosition,
            data =>
            {
                if (JsonSerializer.Deserialize<ComparisonProgress>(data, ProgressOptions) is { } p)
                {
                    last = new RoundProgress(p.Round, p.RoundCount, p.Fraction);
                    progress?.Report(last);
                }
            },
            () => progress?.Report(last with { Round = last.RoundCount, Fraction = 1 }),
            data => new ComparisonResult(ComparisonDataJson.Deserialize(data)),
            cancellationToken);
    }

    /// <summary>
    /// Envoie la demande et lit le flux Server-Sent Events au fil de l'eau : position dans la file, progression, puis
    /// résultat (lu par <paramref name="read"/>) ou erreur. Refus, pannes et résultat illisible deviennent des
    /// <see cref="SimulationServiceException"/>.
    /// </summary>
    private async Task<T> StreamAsync<T>(
        string path,
        JsonObject body,
        IProgress<int>? queuePosition,
        Action<string> onProgress,
        Action onFinished,
        Func<string, T> read,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        // Dans le navigateur : lire la réponse au fil de l'eau plutôt qu'en une fois à la fin
        request.SetBrowserResponseStreamingEnabled(true);

        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw await RejectionAsync(response, cancellationToken);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await foreach (var (type, data) in ServerSentEvents.ReadAsync(stream, cancellationToken))
            {
                switch (type)
                {
                    case "queued" when int.TryParse(data, CultureInfo.InvariantCulture, out int position):
                        queuePosition?.Report(position);
                        break;
                    case "progress":
                        onProgress(data);
                        break;
                    case "result":
                        // Barre à 100 % et page redessinée avant le traitement du résultat, qui occupe un moment le
                        // navigateur (lecture, statistiques, affichage des tableaux)
                        onFinished();
                        await Task.Delay(ResultPaintDelay, cancellationToken);
                        try
                        {
                            return read(data);
                        }
                        catch (Exception ex) when (ex is JsonException or ArgumentException)
                        {
                            throw new SimulationServiceException("Résultat illisible", [ex.Message]);
                        }
                    case "error":
                        throw new SimulationServiceException("Échec du calcul", [data]);
                }
            }
            throw new SimulationServiceException("Réponse incomplète du serveur", ["La connexion s'est interrompue avant le résultat."]);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException && !cancellationToken.IsCancellationRequested)
        {
            throw new SimulationServiceException("Serveur injoignable", ["Vérifiez la connexion, puis relancez."]);
        }
    }

    private static readonly JsonSerializerOptions ProgressOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Progression d'une comparaison, telle que l'envoie le serveur.</summary>
    private sealed record ComparisonProgress(int Round, int RoundCount, double Fraction);

    /// <summary>Refus du serveur : titre et erreurs de son <c>ProblemDetails</c>, à défaut le code HTTP.</summary>
    private static async Task<SimulationServiceException> RejectionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            string? title = problem?["title"]?.GetValue<string>();
            var errors = problem?["errors"]?.AsArray().Select(e => e!.GetValue<string>()).ToList() ?? [];
            if (title is not null)
            {
                return new SimulationServiceException(title, errors);
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // Pas un ProblemDetails : message générique ci-dessous
        }
        return response.StatusCode == HttpStatusCode.TooManyRequests
            ? new SimulationServiceException("Trop de lancements", ["Réessayez dans un instant."])
            : new SimulationServiceException($"Erreur du serveur ({(int)response.StatusCode})", []);
    }

    private sealed record SimulationLimits(int MaxIterations, int? MaxComparisonVariants);
}
