using System.Globalization;
using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Wh40kSim.Core.Serialization;
using Wh40kSim.Simulation;
using Wh40kSim.Simulation.Comparisons;

namespace Wh40kSim.Web;

/// <summary>Demande de comparaison : comparaison (format des fichiers), simulations de la finale, graine facultative.</summary>
public sealed record ComparisonRequest
{
    public required Comparison Comparison { get; init; }

    public required int Iterations { get; init; }

    public int? Seed { get; init; }
}

/// <summary>Progression d'une comparaison, envoyée dans l'événement <c>progress</c> : ronde, nombre de rondes, avancement.</summary>
public sealed record ComparisonProgress(int Round, int RoundCount, double Fraction);

/// <summary>
/// <c>POST /api/comparisons</c> : comme <c>POST /api/simulations</c> (même file d'attente, même limite de lancements),
/// pour une comparaison en rondes éliminatoires. Refus en <c>ProblemDetails</c> avant tout calcul (demande illisible,
/// comparaison invalide, limites du site : simulations de la finale, nombre de variantes), sinon Server-Sent Events :
/// <c>queued</c> (position), <c>progress</c> (<see cref="ComparisonProgress"/> en JSON), <c>result</c>
/// (<see cref="ComparisonData"/> au format de <see cref="ComparisonDataJson"/>), <c>error</c>. Au-delà de
/// <see cref="SimulationOptions.MaxComparisonFinalists"/> entrées en lice avant la finale, seules les meilleures y vont.
/// </summary>
public static class ComparisonEndpoints
{
    private static readonly JsonSerializerOptions ProgressOptions = new(JsonSerializerDefaults.Web);

    public static void MapComparisonEndpoints(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/comparisons", RunAsync).RequireRateLimiting(SimulationEndpoints.RateLimitPolicy);

    private static async Task<IResult> RunAsync(
        HttpRequest request,
        SimulationQueue queue,
        ISimulationRunner runner,
        IOptions<SimulationOptions> options,
        ILoggerFactory loggers,
        LaunchLog log,
        CancellationToken cancellationToken)
    {
        ComparisonRequest? comparison;
        try
        {
            comparison = await JsonSerializer.DeserializeAsync<ComparisonRequest>(request.Body, ScenarioJson.Options, cancellationToken);
            if (comparison is not null)
            {
                ComparisonJson.EnsureComplete(comparison.Comparison);
            }
        }
        catch (JsonException ex)
        {
            log.Write("comparaison", "refusé : Demande illisible");
            return SimulationEndpoints.Problem("Demande illisible", [ex.Message]);
        }
        if (comparison is null)
        {
            log.Write("comparaison", "refusé : Demande illisible");
            return SimulationEndpoints.Problem("Demande illisible", ["La demande est vide."]);
        }

        // Tout ce qui est refusé est signalé en une fois : erreurs de la comparaison et limites du site
        var comparisonErrors = comparison.Comparison.Validate();
        var limitErrors = new List<string>();
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        int maxIterations = options.Value.MaxIterations;
        if (comparison.Iterations < 1 || comparison.Iterations > maxIterations)
        {
            limitErrors.Add($"Nombre de simulations : entre 1 et {maxIterations.ToString("N0", culture)} par lancement.");
        }
        int variants = comparison.Comparison.Variants().Count;
        int maxVariants = options.Value.MaxComparisonVariants;
        if (variants > maxVariants)
        {
            limitErrors.Add($"Variantes (attaquants et combinaisons de profils) : au plus {maxVariants} sur le site ({variants} demandées).");
        }
        if (comparisonErrors.Count > 0 || limitErrors.Count > 0)
        {
            string title = (comparisonErrors.Count > 0, limitErrors.Count > 0) switch
            {
                (true, true) => "Demande refusée",
                (true, false) => "Comparaison invalide",
                _ => "Limites du site dépassées",
            };
            log.Write(["comparaison", $"refusé : {title}", .. Describe(comparison)]);
            return SimulationEndpoints.Problem(title, [.. comparisonErrors, .. limitErrors]);
        }

        var events = Channel.CreateUnbounded<SseItem<string>>();
        _ = ProduceAsync(comparison, queue, runner, options.Value.MaxComparisonFinalists, events.Writer,
            loggers.CreateLogger(typeof(ComparisonEndpoints)), log, cancellationToken);
        return TypedResults.ServerSentEvents(events.Reader.ReadAllAsync(cancellationToken));
    }

    /// <summary>Ce qui a été demandé, pour le journal des lancements : attaquants, variantes, défenseur, finale, graine.</summary>
    private static string[] Describe(ComparisonRequest request)
    {
        var comparison = request.Comparison;
        int variants = comparison.Variants().Count;
        string attackers = string.Join(", ", comparison.Attackers.Select(a => LaunchLog.Name(a.Name)));
        string count = variants > comparison.Entries.Count
            ? $"{comparison.Entries.Count} attaquant(s), {variants} variantes"
            : $"{comparison.Entries.Count} attaquant(s)";
        return
        [
            $"{count} : {attackers} contre {LaunchLog.Name(comparison.Defender.Name)}",
            $"finale {LaunchLog.Number(request.Iterations)} sim.",
            LaunchLog.Seed(request.Seed),
        ];
    }

    private static async Task ProduceAsync(
        ComparisonRequest comparison,
        SimulationQueue queue,
        ISimulationRunner runner,
        int maxFinalists,
        ChannelWriter<SseItem<string>> events,
        ILogger logger,
        LaunchLog log,
        CancellationToken cancellationToken)
    {
        // Journal : issue (avec le gagnant), rondes, attente dans la file et durée du calcul
        var clock = System.Diagnostics.Stopwatch.StartNew();
        TimeSpan? waited = null;
        string outcome = "échec";
        string[] details = [];
        try
        {
            using var slot = await queue.AcquireAsync(
                position => events.TryWrite(new SseItem<string>(position.ToString(CultureInfo.InvariantCulture), SimulationEndpoints.QueuedEvent)),
                cancellationToken);
            waited = clock.Elapsed;

            // Début du calcul ; ensuite, un événement par ronde et par point de pourcentage
            events.TryWrite(Progress(new RoundProgress(1, 1, 0)));
            var shown = (Round: 0, Fraction: 0.0);
            var progress = new SimulationEndpoints.SynchronousProgress<RoundProgress>(value =>
            {
                if (value.Round != shown.Round || value.Fraction - shown.Fraction >= 0.01 || value.Fraction >= 1)
                {
                    shown = (value.Round, value.Fraction);
                    events.TryWrite(Progress(value));
                }
            });

            var result = await runner.RunComparisonAsync(
                comparison.Comparison, comparison.Iterations, comparison.Seed, maxFinalists, progress, cancellationToken);
            events.TryWrite(new SseItem<string>(ComparisonDataJson.Serialize(result.Data), SimulationEndpoints.ResultEvent));
            outcome = "terminé";
            details = [$"{result.Rounds.Count} ronde(s)", $"gagnant {LaunchLog.Name(result.Ranking[0].Name)}"];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Requête fermée par le client : rien à envoyer
            outcome = waited is null ? "annulé pendant l'attente" : "annulé pendant le calcul";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Échec d'une comparaison");
            events.TryWrite(new SseItem<string>("Le calcul a échoué sur le serveur.", SimulationEndpoints.ErrorEvent));
        }
        finally
        {
            events.TryComplete();
            log.Write(["comparaison", outcome, .. Describe(comparison), .. details, .. SimulationEndpoints.Timing(clock.Elapsed, waited)]);
        }
    }

    private static SseItem<string> Progress(RoundProgress progress) =>
        new(JsonSerializer.Serialize(new ComparisonProgress(progress.Round, progress.RoundCount, Math.Round(progress.Fraction, 3)), ProgressOptions),
            SimulationEndpoints.ProgressEvent);
}
