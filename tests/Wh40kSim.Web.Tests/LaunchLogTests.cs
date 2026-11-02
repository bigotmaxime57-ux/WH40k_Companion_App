using Wh40kSim.Core.Model;

namespace Wh40kSim.Web.Tests;

/// <summary>
/// Journal des lancements : une ligne lisible par simulation ou comparaison demandée (demande, issue, attente, durée),
/// refus et annulations compris, sans adresse IP ; rien sans chemin configuré.
/// </summary>
public sealed class LaunchLogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wh40ksim-journal-" + Guid.NewGuid().ToString("N"));

    private string LogPath => Path.Combine(_directory, "lancements.log");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>Lignes du journal, une fois écrites (la dernière l'est à la fin du flux, juste après la réponse).</summary>
    private async Task<string[]> LinesAsync(int expected)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            if (File.Exists(LogPath) && File.ReadAllLines(LogPath) is { } lines && lines.Length >= expected)
            {
                return lines;
            }
            await Task.Delay(100);
        }
        return File.Exists(LogPath) ? File.ReadAllLines(LogPath) : [];
    }

    [Fact]
    public async Task A_finished_simulation_writes_one_readable_line()
    {
        await using var factory = Api.Create(launchLog: LogPath);
        var scenario = Api.Scenario() with { Attacker = Api.Scenario().Attacker with { Name = "Rubrics" } };

        await using var reader = new SseReader(await Api.PostAsync(factory.CreateClient(), Api.Body(scenario, 20_000, seed: 7)));
        await reader.ReadAllAsync();

        string line = Assert.Single(await LinesAsync(1));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}Z \| simulation \| terminé \| ", line);
        Assert.NotEqual(0xEF, File.ReadAllBytes(LogPath)[0]);   // pas de marqueur UTF-8 en tête (BOM)
        Assert.Contains("« Rubrics » contre « Défenseur »", line);
        Assert.Contains("| 1 arme(s) |", line);
        Assert.Contains("| 20 000 sim. |", line);
        Assert.Contains("| graine 7 |", line);
        Assert.Matches(@"\| attente \d+,\d s \| calcul \d+,\d s$", line);
    }

    [Fact]
    public async Task A_refused_request_is_logged_with_its_reason()
    {
        await using var factory = Api.Create(launchLog: LogPath);
        var invalid = new Scenario { Attacker = Api.Scenario().Attacker, Defender = new DefenderUnit { Groups = [] } };

        using var response = await Api.PostAsync(factory.CreateClient(), Api.Body(invalid, 1_000));

        Assert.Contains("| simulation | refusé : Scénario invalide |", Assert.Single(await LinesAsync(1)));
    }

    [Fact]
    public async Task A_comparison_is_logged_with_its_attackers_and_winner()
    {
        await using var factory = Api.Create(launchLog: LogPath);

        await using var reader = new SseReader(await Api.PostComparisonAsync(factory.CreateClient(),
            Api.ComparisonBody(Api.ComparisonOf(Api.Attacker("Fort", 10), Api.Attacker("Faible", 2)), 5_000, seed: 3)));
        await reader.ReadAllAsync();

        string line = Assert.Single(await LinesAsync(1));
        Assert.Contains("| comparaison | terminé | 2 attaquant(s) : « Fort », « Faible » contre « Défenseur » |", line);
        Assert.Contains("| finale 5 000 sim. | graine 3 | 1 ronde(s) | gagnant « Fort » |", line);
    }

    [Fact]
    public async Task A_request_cancelled_while_waiting_is_logged_as_such()
    {
        var runner = new ControlledRunner();
        await using var factory = Api.Create(runner: runner, launchLog: LogPath);
        var client = factory.CreateClient();
        string body = Api.Body(Api.Scenario(), 1_000);

        await using var first = new SseReader(await Api.PostAsync(client, body));
        await first.NextAsync();
        await Wait.ForAsync(runner.Started);

        using var cancellation = new CancellationTokenSource();
        var second = new SseReader(await Api.PostAsync(client, body, cancellation.Token));
        Assert.Equal((SimulationEndpoints.QueuedEvent, "1"), await second.NextAsync());
        cancellation.Cancel();
        await second.DisposeAsync();

        Assert.Contains(await LinesAsync(1), l => l.Contains("| simulation | annulé pendant l'attente |"));
        runner.Release();
    }

    [Fact]
    public async Task Names_cannot_break_the_line()
    {
        await using var factory = Api.Create(launchLog: LogPath);
        var scenario = Api.Scenario() with { Attacker = Api.Scenario().Attacker with { Name = "A | B\nC" } };

        await using var reader = new SseReader(await Api.PostAsync(factory.CreateClient(), Api.Body(scenario, 1_000)));
        await reader.ReadAllAsync();

        string line = Assert.Single(await LinesAsync(1));
        Assert.Contains("« A / B C »", line);
    }

    [Fact]
    public async Task Without_a_path_nothing_is_written()
    {
        await using var factory = Api.Create();

        await using var reader = new SseReader(await Api.PostAsync(factory.CreateClient(), Api.Body(Api.Scenario(), 1_000)));
        await reader.ReadAllAsync();

        Assert.False(Directory.Exists(_directory));
    }
}
