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
using Wh40kSim.UI.Services;

namespace Wh40kSim.Web.Client.Services;

/// <summary>
/// Simulation par l'API du serveur (<c>POST /api/simulations</c>) : le flux Server-Sent Events est lu au fil de l'eau
/// (position dans la file, progression), puis les données brutes du résultat redeviennent un
/// <see cref="SimulationResult"/>. Annuler ferme la requête : le serveur arrête le calcul ou retire la demande.
/// Les refus et les pannes deviennent des <see cref="SimulationServiceException"/> (messages en français).
/// </summary>
public sealed class ApiSimulationService(HttpClient http) : ISimulationService
{
    public const string SimulationsPath = "api/simulations";
    public const string LimitsPath = "api/simulations/limits";

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
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
        {
            MaxIterations = null;
        }
    }

    public async Task<SimulationResult> RunAsync(
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
        using var request = new HttpRequestMessage(HttpMethod.Post, SimulationsPath)
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
                    case "progress" when double.TryParse(data, CultureInfo.InvariantCulture, out double done):
                        progress?.Report(done);
                        break;
                    case "result":
                        // Barre à 100 % et page redessinée avant le traitement du résultat, qui occupe un moment le
                        // navigateur (lecture, statistiques, affichage des tableaux)
                        progress?.Report(1);
                        await Task.Delay(ResultPaintDelay, cancellationToken);
                        return Result(data);
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

    private static SimulationResult Result(string data)
    {
        try
        {
            return new SimulationResult(SimulationJson.Deserialize(data));
        }
        catch (JsonException ex)
        {
            throw new SimulationServiceException("Résultat illisible", [ex.Message]);
        }
    }

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

    private sealed record SimulationLimits(int MaxIterations);
}
