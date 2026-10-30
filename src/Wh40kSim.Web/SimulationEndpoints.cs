using System.Globalization;
using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;
using Wh40kSim.Simulation;

namespace Wh40kSim.Web;

/// <summary>Demande de simulation : scénario complet (format des fichiers), nombre de simulations, graine facultative.</summary>
public sealed record SimulationRequest
{
    public required Scenario Scenario { get; init; }

    public required int Iterations { get; init; }

    public int? Seed { get; init; }
}

/// <summary>
/// <c>POST /api/simulations</c> : refus en <c>ProblemDetails</c> avant tout calcul (demande illisible, scénario invalide,
/// nombre de simulations hors limites), sinon réponse en Server-Sent Events : <c>queued</c> (position dans la file),
/// <c>progress</c> (0 à 1), <c>result</c> (<see cref="SimulationData"/> au format de <see cref="SimulationJson"/>),
/// <c>error</c> (échec inattendu). Requête fermée : la demande quitte la file ou le calcul s'arrête.
/// </summary>
public static class SimulationEndpoints
{
    public const string RateLimitPolicy = "simulations";

    public const string QueuedEvent = "queued";
    public const string ProgressEvent = "progress";
    public const string ResultEvent = "result";
    public const string ErrorEvent = "error";

    /// <summary>Progression minimale entre deux événements (1 %).</summary>
    private const double ProgressStep = 0.01;

    public static void MapSimulationEndpoints(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/simulations", RunAsync).RequireRateLimiting(RateLimitPolicy);

    private static async Task<IResult> RunAsync(
        HttpRequest request,
        SimulationQueue queue,
        ISimulationRunner runner,
        IOptions<SimulationOptions> options,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        SimulationRequest? simulation;
        try
        {
            simulation = await JsonSerializer.DeserializeAsync<SimulationRequest>(request.Body, ScenarioJson.Options, cancellationToken);
        }
        catch (JsonException ex)
        {
            return Problem("Demande illisible", [ex.Message]);
        }
        if (simulation is null)
        {
            return Problem("Demande illisible", ["La demande est vide."]);
        }

        // Tout ce qui est refusé est signalé en une fois : erreurs du scénario et nombre de simulations
        var scenarioErrors = simulation.Scenario.Validate();
        int max = options.Value.MaxIterations;
        bool iterationsRefused = simulation.Iterations < 1 || simulation.Iterations > max;
        if (scenarioErrors.Count > 0 || iterationsRefused)
        {
            var errors = scenarioErrors.ToList();
            if (iterationsRefused)
            {
                errors.Add($"Nombre de simulations : entre 1 et {max.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))} par lancement.");
            }
            string title = (scenarioErrors.Count > 0, iterationsRefused) switch
            {
                (true, true) => "Demande refusée",
                (true, false) => "Scénario invalide",
                _ => "Nombre de simulations refusé",
            };
            return Problem(title, errors);
        }

        // Les événements sont produits par le calcul (autre thread) et envoyés au fil de l'eau
        var events = Channel.CreateUnbounded<SseItem<string>>();
        _ = ProduceAsync(simulation, queue, runner, events.Writer, loggers.CreateLogger(typeof(SimulationEndpoints)), cancellationToken);
        return TypedResults.ServerSentEvents(events.Reader.ReadAllAsync(cancellationToken));
    }

    private static async Task ProduceAsync(
        SimulationRequest simulation,
        SimulationQueue queue,
        ISimulationRunner runner,
        ChannelWriter<SseItem<string>> events,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            using var slot = await queue.AcquireAsync(
                position => events.TryWrite(new SseItem<string>(position.ToString(CultureInfo.InvariantCulture), QueuedEvent)),
                cancellationToken);

            // Début du calcul : sort de l'état « en attente » côté client
            events.TryWrite(new SseItem<string>(Format(0), ProgressEvent));
            double shown = 0;
            var progress = new SynchronousProgress(value =>
            {
                if (value - shown >= ProgressStep || value >= 1)
                {
                    shown = value;
                    events.TryWrite(new SseItem<string>(Format(value), ProgressEvent));
                }
            });

            var result = await runner.RunAsync(simulation.Scenario, simulation.Iterations, simulation.Seed, progress, cancellationToken);
            events.TryWrite(new SseItem<string>(SimulationJson.Serialize(result.Data), ResultEvent));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Requête fermée par le client : rien à envoyer
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Échec d'une simulation");
            events.TryWrite(new SseItem<string>("Le calcul a échoué sur le serveur.", ErrorEvent));
        }
        finally
        {
            events.TryComplete();
        }
    }

    private static string Format(double progress) => progress.ToString("0.###", CultureInfo.InvariantCulture);

    private static IResult Problem(string title, IReadOnlyList<string> errors) =>
        TypedResults.Problem(title: title, statusCode: StatusCodes.Status400BadRequest,
            extensions: new Dictionary<string, object?> { ["errors"] = errors });

    /// <summary>Progression rapportée tout de suite, depuis le thread de calcul (pas de contexte à retrouver).</summary>
    private sealed class SynchronousProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
