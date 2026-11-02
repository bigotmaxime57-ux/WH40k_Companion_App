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

/// <summary>Limites du serveur utiles au client (<c>GET /api/simulations/limits</c>) : simulations, variantes d'une comparaison.</summary>
public sealed record SimulationLimits(int MaxIterations, int MaxComparisonVariants);

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

    public static void MapSimulationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/simulations", RunAsync).RequireRateLimiting(RateLimitPolicy);
        // Plafond connu du client web au démarrage (choix rapides, saisie bornée)
        app.MapGet("/api/simulations/limits", (IOptions<SimulationOptions> options) =>
            TypedResults.Ok(new SimulationLimits(options.Value.MaxIterations, options.Value.MaxComparisonVariants)));
    }

    private static async Task<IResult> RunAsync(
        HttpRequest request,
        SimulationQueue queue,
        ISimulationRunner runner,
        IOptions<SimulationOptions> options,
        ILoggerFactory loggers,
        LaunchLog log,
        CancellationToken cancellationToken)
    {
        SimulationRequest? simulation;
        try
        {
            simulation = await JsonSerializer.DeserializeAsync<SimulationRequest>(request.Body, ScenarioJson.Options, cancellationToken);
        }
        catch (JsonException ex)
        {
            log.Write("simulation", "refusé : Demande illisible");
            return Problem("Demande illisible", [ex.Message]);
        }
        if (simulation is null)
        {
            log.Write("simulation", "refusé : Demande illisible");
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
            log.Write(["simulation", $"refusé : {title}", .. Describe(simulation)]);
            return Problem(title, errors);
        }

        // Les événements sont produits par le calcul (autre thread) et envoyés au fil de l'eau
        var events = Channel.CreateUnbounded<SseItem<string>>();
        _ = ProduceAsync(simulation, queue, runner, events.Writer, loggers.CreateLogger(typeof(SimulationEndpoints)), log, cancellationToken);
        return TypedResults.ServerSentEvents(events.Reader.ReadAllAsync(cancellationToken));
    }

    /// <summary>Ce qui a été demandé, pour le journal des lancements : unités, armes, simulations, graine.</summary>
    private static string[] Describe(SimulationRequest simulation) =>
    [
        $"{LaunchLog.Name(simulation.Scenario.Attacker.Name)} contre {LaunchLog.Name(simulation.Scenario.Defender.Name)}",
        $"{simulation.Scenario.Attacker.Weapons.Count} arme(s)",
        $"{LaunchLog.Number(simulation.Iterations)} sim.",
        LaunchLog.Seed(simulation.Seed),
    ];

    private static async Task ProduceAsync(
        SimulationRequest simulation,
        SimulationQueue queue,
        ISimulationRunner runner,
        ChannelWriter<SseItem<string>> events,
        ILogger logger,
        LaunchLog log,
        CancellationToken cancellationToken)
    {
        // Journal : issue, attente dans la file et durée du calcul
        var clock = System.Diagnostics.Stopwatch.StartNew();
        TimeSpan? waited = null;
        string outcome = "échec";
        try
        {
            using var slot = await queue.AcquireAsync(
                position => events.TryWrite(new SseItem<string>(position.ToString(CultureInfo.InvariantCulture), QueuedEvent)),
                cancellationToken);
            waited = clock.Elapsed;

            // Début du calcul : sort de l'état « en attente » côté client
            events.TryWrite(new SseItem<string>(Format(0), ProgressEvent));
            double shown = 0;
            var progress = new SynchronousProgress<double>(value =>
            {
                if (value - shown >= ProgressStep || value >= 1)
                {
                    shown = value;
                    events.TryWrite(new SseItem<string>(Format(value), ProgressEvent));
                }
            });

            var result = await runner.RunAsync(simulation.Scenario, simulation.Iterations, simulation.Seed, progress, cancellationToken);
            events.TryWrite(new SseItem<string>(SimulationJson.Serialize(result.Data), ResultEvent));
            outcome = "terminé";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Requête fermée par le client : rien à envoyer
            outcome = waited is null ? "annulé pendant l'attente" : "annulé pendant le calcul";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Échec d'une simulation");
            events.TryWrite(new SseItem<string>("Le calcul a échoué sur le serveur.", ErrorEvent));
        }
        finally
        {
            events.TryComplete();
            log.Write(["simulation", outcome, .. Describe(simulation), .. Timing(clock.Elapsed, waited)]);
        }
    }

    /// <summary>Attente dans la file et durée du calcul, pour le journal (calcul absent s'il n'a pas commencé).</summary>
    internal static string[] Timing(TimeSpan total, TimeSpan? waited) => waited is TimeSpan wait
        ? [$"attente {LaunchLog.Seconds(wait)}", $"calcul {LaunchLog.Seconds(total - wait)}"]
        : [$"attente {LaunchLog.Seconds(total)}"];

    internal static string Format(double progress) => progress.ToString("0.###", CultureInfo.InvariantCulture);

    internal static IResult Problem(string title, IReadOnlyList<string> errors) =>
        TypedResults.Problem(title: title, statusCode: StatusCodes.Status400BadRequest,
            extensions: new Dictionary<string, object?> { ["errors"] = errors });

    /// <summary>Progression rapportée tout de suite, depuis le thread de calcul (pas de contexte à retrouver).</summary>
    internal sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
