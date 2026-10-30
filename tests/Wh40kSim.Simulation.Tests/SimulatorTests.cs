using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;

namespace Wh40kSim.Simulation.Tests;

/// <summary>
/// Le Monte Carlo doit retrouver les moyennes calculées à la main.
/// Base : 10 attaques, CT 3+ (2/3), F4 contre E4 (1/2), aucune sauvegarde, D1, cibles à 1 PV.
/// </summary>
public class SimulatorTests
{
    private const int Iterations = 200_000;
    private const double Tolerance = 0.03; // ≈ 9 erreurs-types : un échec signale un vrai bug, pas du bruit

    private static WeaponProfile BaseWeapon => new() { Skill = 3, Strength = 4 };

    private static void AssertMeanKills(double expected, Scenario scenario)
    {
        var result = Simulator.Run(scenario, Iterations, seed: 12345);
        Assert.InRange(result.Kills.Mean, expected - Tolerance, expected + Tolerance);
    }

    [Fact]
    public void Basic_profile_matches_the_analytic_mean()
    {
        AssertMeanKills(10 * (2.0 / 3) * 0.5, Build.Scenario(BaseWeapon, Build.Horde(), models: 10));
    }

    [Fact]
    public void Lethal_hits_matches_the_analytic_mean()
    {
        // 6 : blessure auto (1/6) ; 3-5 : jet de blessure normal (3/6 × 1/2)
        var weapon = BaseWeapon with { Abilities = new WeaponAbilities { LethalHits = true } };
        AssertMeanKills(10 * (1.0 / 6 + 0.5 * 0.5), Build.Scenario(weapon, Build.Horde(), models: 10));
    }

    [Fact]
    public void Sustained_hits_matches_the_analytic_mean()
    {
        // touches par attaque = 2/3 + 1/6
        var weapon = BaseWeapon with { Abilities = new WeaponAbilities { SustainedHits = 1 } };
        AssertMeanKills(10 * (2.0 / 3 + 1.0 / 6) * 0.5, Build.Scenario(weapon, Build.Horde(), models: 10));
    }

    [Fact]
    public void Devastating_wounds_matches_the_analytic_mean_against_a_2_up_save()
    {
        // blessure critique (1/6) sans svg + blessure normale (2/6) × svg 2+ ratée (1/6)
        var weapon = BaseWeapon with { Abilities = new WeaponAbilities { DevastatingWounds = true } };
        AssertMeanKills(10 * (2.0 / 3) * (1.0 / 6 + 2.0 / 6 * 1.0 / 6), Build.Scenario(weapon, Build.Horde(save: 2), models: 10));
    }

    [Fact]
    public void Twin_linked_matches_the_analytic_mean()
    {
        // blessure avec relance des échecs : 1/2 + 1/2 × 1/2
        var weapon = BaseWeapon with { Abilities = new WeaponAbilities { TwinLinked = true } };
        AssertMeanKills(10 * (2.0 / 3) * 0.75, Build.Scenario(weapon, Build.Horde(), models: 10));
    }

    [Fact]
    public void Cover_matches_the_analytic_mean()
    {
        // CT 3+ à couvert = 4+
        AssertMeanKills(10 * 0.5 * 0.5, Build.Scenario(BaseWeapon, Build.Horde() with { Boosts = new DefenderBoosts { InCover = true } }, models: 10));
    }

    [Fact]
    public void Same_seed_gives_identical_results()
    {
        var scenario = Build.Scenario(BaseWeapon with { Damage = DiceExpression.Parse("D3") }, Build.Horde(), models: 10);

        var first = Simulator.Run(scenario, 50_000, seed: 7);
        var second = Simulator.Run(scenario, 50_000, seed: 7);

        Assert.Equal(first.Kills.Counts, second.Kills.Counts);
        Assert.Equal(first.Damage.Counts, second.Damage.Counts);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Limiting_the_threads_does_not_change_the_results(int threads)
    {
        var scenario = Build.Scenario(BaseWeapon with { Damage = DiceExpression.Parse("D3") }, Build.Horde(), models: 10);

        var all = Simulator.Run(scenario, 50_000, seed: 7);
        var limited = Simulator.Run(scenario, 50_000, seed: 7, threads: new SimulationThreads(threads));

        Assert.Equal(all.Kills.Counts, limited.Kills.Counts);
        Assert.Equal(all.Damage.Counts, limited.Damage.Counts);
        Assert.Equal(all.MeanKilledInGroup(0), limited.MeanKilledInGroup(0));
    }

    [Fact]
    public void A_custom_scheduler_runs_the_computation_with_the_same_results()
    {
        var scenario = Build.Scenario(BaseWeapon, Build.Horde(), models: 10);
        var scheduler = new CountingScheduler();

        var custom = Simulator.Run(scenario, 5 * Simulator.ChunkSize, seed: 3, threads: new SimulationThreads(2, scheduler));

        Assert.True(scheduler.Queued > 0);
        Assert.Equal(Simulator.Run(scenario, 5 * Simulator.ChunkSize, seed: 3).Kills.Counts, custom.Kills.Counts);
    }

    /// <summary>Ordonnanceur du pool de threads qui compte les tâches reçues.</summary>
    private sealed class CountingScheduler : TaskScheduler
    {
        private int _queued;

        public int Queued => _queued;

        protected override void QueueTask(Task task)
        {
            Interlocked.Increment(ref _queued);
            ThreadPool.QueueUserWorkItem(_ => TryExecuteTask(task));
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => TryExecuteTask(task);

        protected override IEnumerable<Task> GetScheduledTasks() => [];
    }

    [Fact]
    public void Iteration_count_is_exact_even_when_not_a_multiple_of_the_chunk_size()
    {
        var result = Simulator.Run(Build.Scenario(BaseWeapon, Build.Horde()), 25_001, seed: 1);
        Assert.Equal(25_001, result.Iterations);
        Assert.Equal(25_001, result.Kills.Counts.Sum());
    }

    [Fact]
    public void Invalid_scenario_is_rejected()
    {
        var scenario = Build.Scenario(BaseWeapon with { Skill = 9 }, Build.Horde());
        Assert.Throws<ArgumentException>(() => Simulator.Run(scenario, 10));
    }

    [Fact]
    public void Progress_is_reported_once_per_chunk_in_increasing_order_up_to_one()
    {
        // 2,5 paquets → 3 rapports, le dernier paquet est incomplet
        var progress = new RecordingProgress();

        Simulator.Run(Build.Scenario(BaseWeapon, Build.Horde()), 2 * Simulator.ChunkSize + Simulator.ChunkSize / 2, seed: 1, progress);

        Assert.Equal([1.0 / 3, 2.0 / 3, 1.0], progress.Values);
    }

    [Fact]
    public void Progress_is_reported_once_when_there_is_a_single_chunk()
    {
        var progress = new RecordingProgress();

        Simulator.Run(Build.Scenario(BaseWeapon, Build.Horde()), 10, seed: 1, progress);

        Assert.Equal([1.0], progress.Values);
    }

    [Fact]
    public void Progress_stays_increasing_with_many_chunks_in_parallel()
    {
        var progress = new RecordingProgress();

        Simulator.Run(Build.Scenario(BaseWeapon, Build.Horde()), 50 * Simulator.ChunkSize, seed: 1, progress);

        Assert.Equal(50, progress.Values.Count);
        Assert.All(progress.Values.Zip(progress.Values.Skip(1)), pair => Assert.True(pair.First < pair.Second));
        Assert.Equal(1.0, progress.Values[^1]);
    }

    [Fact]
    public void Reporting_progress_does_not_change_the_results()
    {
        var scenario = Build.Scenario(BaseWeapon with { Damage = DiceExpression.Parse("D3") }, Build.Horde(), models: 10);

        var without = Simulator.Run(scenario, 50_000, seed: 7);
        var with = Simulator.Run(scenario, 50_000, seed: 7, new RecordingProgress());

        Assert.Equal(without.Kills.Counts, with.Kills.Counts);
        Assert.Equal(without.Damage.Counts, with.Damage.Counts);
        Assert.Equal(without.MeanKilledInGroup(0), with.MeanKilledInGroup(0));
    }

    [Fact]
    public void Cancelled_run_does_not_report_completion()
    {
        var progress = new RecordingProgress();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            Simulator.Run(Build.Scenario(BaseWeapon, Build.Horde()), 10 * Simulator.ChunkSize, seed: 1, progress, cancellation.Token));
        Assert.DoesNotContain(1.0, progress.Values);
    }

    [Fact]
    public void Example_scenario_file_loads_and_runs()
    {
        string path = Path.Combine(Build.RepositoryRoot(), "scenarios", "exemple-escouade-vs-leader.json");
        var scenario = ScenarioJson.Load(path);

        var result = Simulator.Run(scenario, 10_000, seed: 3);

        Assert.Equal(11, result.TotalModels);
        Assert.InRange(result.Kills.Mean, 0.5, 11);
    }
}
