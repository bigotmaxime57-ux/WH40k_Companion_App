using Wh40kSim.Core.Engine;
using Wh40kSim.Core.Model;
using Wh40kSim.UI.Services;

namespace Wh40kSim.UI.Tests;

public class LocalSimulationServiceTests
{
    private static readonly Scenario Scenario = new()
    {
        Attacker = new AttackerUnit { Weapons = [new WeaponGroup { ModelCount = 10, Weapon = new WeaponProfile { Skill = 3 } }] },
        Defender = new DefenderUnit { Groups = [new ModelGroup { Count = 20 }] },
    };

    private readonly LocalSimulationService _service = new();

    [Fact]
    public async Task Gives_the_same_result_as_the_core_with_the_same_seed()
    {
        var expected = Simulator.Run(Scenario, 30_000, seed: 5);

        var actual = await _service.RunAsync(Scenario, 30_000, seed: 5);

        Assert.Equal(expected.KillsHistogram, actual.KillsHistogram);
        Assert.Equal(expected.DamageHistogram, actual.DamageHistogram);
    }

    [Fact]
    public async Task Forwards_progress_up_to_one()
    {
        var progress = new RecordingProgress();

        await _service.RunAsync(Scenario, 3 * Simulator.ChunkSize, seed: 1, progress);

        Assert.Equal([1.0 / 3, 2.0 / 3, 1.0], progress.Values);
    }

    [Fact]
    public async Task Cancellation_stops_the_simulation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.RunAsync(Scenario, 1_000_000, cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task Cancellation_during_the_run_stops_it_early()
    {
        using var cancellation = new CancellationTokenSource();
        // Annule dès le premier paquet terminé
        var progress = new CallbackProgress(_ => cancellation.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.RunAsync(Scenario, 10_000_000, seed: 1, progress, cancellation.Token));
    }

    [Fact]
    public async Task Invalid_scenario_is_reported()
    {
        var invalid = Scenario with { Defender = new DefenderUnit() };

        await Assert.ThrowsAsync<ArgumentException>(() => _service.RunAsync(invalid, 10));
    }

    /// <summary>Enregistre les valeurs dès leur arrivée (Progress&lt;T&gt; les transmettrait plus tard).</summary>
    private sealed class RecordingProgress : IProgress<double>
    {
        private readonly List<double> _values = [];

        public IReadOnlyList<double> Values
        {
            get
            {
                lock (_values)
                {
                    return _values.ToList();
                }
            }
        }

        public void Report(double value)
        {
            lock (_values)
            {
                _values.Add(value);
            }
        }
    }

    private sealed class CallbackProgress(Action<double> callback) : IProgress<double>
    {
        public void Report(double value) => callback(value);
    }
}
