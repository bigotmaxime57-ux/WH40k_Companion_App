using Wh40kSim.Core.Model;
using Wh40kSim.Simulation.Comparisons;

namespace Wh40kSim.Simulation.Tests;

/// <summary>
/// Rondes éliminatoires : plan selon le nombre d'entrées (seuils du banc d'essai), élimination à 3 erreurs types sous
/// le 3e, finalistes identiques à une simulation seule à graine égale, comparaison en rondes.
/// </summary>
public class EliminationRoundsTests
{
    private static DefenderUnit Squad => new()
    {
        Groups = [new ModelGroup { Name = "Gardes", Count = 10, Wounds = 1, Toughness = 4, Save = 4 }],
    };

    private static AttackerUnit Attacker(string name, int attacks) => new()
    {
        Name = name,
        Weapons = [new WeaponGroup { ModelCount = 1, Weapon = new WeaponProfile { Name = "Bolter", Attacks = attacks, Skill = 3, Strength = 4 } }],
    };

    private static Scenario Against(AttackerUnit attacker) => new() { Attacker = attacker, Defender = Squad };

    // Plan

    [Theory]
    [InlineData(2, 1_000_000, new[] { 1_000_000 })]
    [InlineData(3, 1_000_000, new[] { 1_000_000 })]
    [InlineData(4, 1_000_000, new[] { 100_000, 1_000_000 })]
    [InlineData(9, 1_000_000, new[] { 100_000, 1_000_000 })]
    [InlineData(10, 1_000_000, new[] { 10_000, 100_000, 1_000_000 })]
    [InlineData(100, 1_000_000, new[] { 10_000, 100_000, 1_000_000 })]
    [InlineData(10, 100_000, new[] { 10_000, 100_000 })]   // 1 000 : trop court, sauté
    [InlineData(10, 10_000, new[] { 10_000 })]
    [InlineData(5, 50_000, new[] { 50_000 })]              // 5 000 : sauté
    public void The_plan_follows_the_number_of_entries(int entries, int iterations, int[] expected)
    {
        Assert.Equal(expected, RoundPlan.For(entries, iterations));
    }

    // Élimination

    [Fact]
    public void Clearly_weaker_candidates_are_eliminated_and_the_top_3_reaches_the_final()
    {
        var candidates = new[] { Attacker("Fort", 20), Attacker("Faible", 1), Attacker("Bon", 12), Attacker("Moyen", 6), Attacker("Très faible", 1) }
            .Select(Against).ToList();

        var outcomes = EliminationRounds.Run(candidates, [10_000, 100_000], seed: 3);

        Assert.Equal([null, 1, null, null, 1], outcomes.Select(o => o.EliminatedInRound));
        Assert.Equal([100_000, 10_000, 100_000, 100_000, 10_000], outcomes.Select(o => o.Result.Iterations));
    }

    [Fact]
    public void A_candidate_tied_with_the_third_stays_in_the_running()
    {
        var candidates = new[] { Attacker("A", 20), Attacker("B", 12), Attacker("C", 6), Attacker("C bis", 6), Attacker("D", 1) }
            .Select(Against).ToList();

        var outcomes = EliminationRounds.Run(candidates, [10_000, 100_000], seed: 3);

        Assert.Equal([null, null, null, null, 1], outcomes.Select(o => o.EliminatedInRound));
    }

    [Fact]
    public void Finalists_are_identical_to_a_single_simulation_with_the_same_seed()
    {
        var candidates = new[] { Attacker("A", 20), Attacker("B", 12), Attacker("C", 6), Attacker("D", 1) }.Select(Against).ToList();

        var outcomes = EliminationRounds.Run(candidates, [10_000, 100_000], seed: 3);

        for (int i = 0; i < 3; i++)
        {
            var alone = Simulator.Run(candidates[i], 100_000, seed: 3);
            Assert.Equal(Comparable(alone.Data), Comparable(outcomes[i].Result.Data));
        }
        // Le candidat éliminé garde sa ronde de 10 000 : le début exact d'une simulation plus longue à la même graine
        Assert.Equal(Comparable(Simulator.Run(candidates[3], 10_000, seed: 3).Data), Comparable(outcomes[3].Result.Data));
    }

    [Fact]
    public void The_finalist_cap_keeps_only_the_best_scores()
    {
        // 6 candidats identiques (même graine : mêmes résultats) : sans plafond, tous à égalité vont en finale ;
        // avec un plafond de 4, les 4 premiers (égalité exacte : ordre des candidats)
        var candidates = Enumerable.Range(0, 6).Select(i => Against(Attacker($"C{i}", 20))).ToList();

        var free = EliminationRounds.Run(candidates, [10_000, 20_000], seed: 3);
        var capped = EliminationRounds.Run(candidates, [10_000, 20_000], seed: 3, maxFinalists: 4);

        Assert.All(free, o => Assert.Null(o.EliminatedInRound));
        Assert.Equal([null, null, null, null, 1, 1], capped.Select(o => o.EliminatedInRound));
    }

    [Fact]
    public void The_finalist_cap_never_goes_below_the_podium()
    {
        var candidates = new[] { 20, 20, 20, 20 }.Select((a, i) => Against(Attacker($"C{i}", a))).ToList();

        var capped = EliminationRounds.Run(candidates, [10_000, 20_000], seed: 3, maxFinalists: 1);

        Assert.Equal(3, capped.Count(o => o.EliminatedInRound is null));
    }

    [Fact]
    public void Three_candidates_or_fewer_are_never_eliminated()
    {
        var candidates = new[] { Attacker("A", 20), Attacker("B", 1), Attacker("C", 1) }.Select(Against).ToList();

        var outcomes = EliminationRounds.Run(candidates, [10_000, 20_000], seed: 3);

        Assert.All(outcomes, o => Assert.Null(o.EliminatedInRound));
    }

    [Fact]
    public void Progress_is_reported_round_by_round()
    {
        var progress = new RecordingProgress<RoundProgress>();
        var candidates = new[] { Attacker("A", 20), Attacker("B", 12), Attacker("C", 6), Attacker("D", 1) }.Select(Against).ToList();

        EliminationRounds.Run(candidates, [Simulator.ChunkSize, 2 * Simulator.ChunkSize], seed: 3, progress: progress);

        var values = progress.Values;
        Assert.All(values, v => Assert.Equal(2, v.RoundCount));
        Assert.Equal([1, 2], values.Select(v => v.Round).Distinct());
        foreach (var round in values.GroupBy(v => v.Round))
        {
            var fractions = round.Select(v => v.Fraction).ToList();
            Assert.True(fractions.Zip(fractions.Skip(1)).All(pair => pair.Second > pair.First));
            Assert.Equal(1.0, fractions[^1]);
        }
        Assert.Equal(4, values.Count(v => v.Round == 1));   // 4 candidats, un paquet chacun
        Assert.Equal(6, values.Count(v => v.Round == 2));   // 3 finalistes, deux paquets chacun
    }

    [Fact]
    public void A_cancelled_run_gives_no_result()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var candidates = new[] { Attacker("A", 20), Attacker("B", 12) }.Select(Against).ToList();

        Assert.ThrowsAny<OperationCanceledException>(() => EliminationRounds.Run(candidates, [10_000], seed: 3, cancellationToken: cancellation.Token));
    }

    // Comparaison en rondes

    [Fact]
    public void A_comparison_of_ten_attackers_runs_in_rounds()
    {
        var attackers = new[] { Attacker("Fort", 20), Attacker("Bon", 16), Attacker("Moyen", 12) }
            .Concat(Enumerable.Range(1, 7).Select(i => Attacker($"Faible {i}", 1)))
            .ToArray();
        var comparison = Comparison.Of(Squad, attackers);

        var result = Comparator.Run(comparison, 100_000, seed: 5);

        Assert.Equal([10_000, 100_000], result.Rounds);
        Assert.Equal(100_000, result.Iterations);
        Assert.Equal([0, 1, 2], result.Finalists);
        Assert.Equal(["Fort", "Bon", "Moyen"], result.Podium.Select(r => r.Name));
        Assert.All(result.Podium, r => Assert.True(r.IsFinalist));
        Assert.All(result.Ranking.Skip(3), r =>
        {
            Assert.Equal(1, r.EliminatedInRound);
            Assert.Equal(10_000, r.Result.Iterations);
            Assert.False(r.TiedWithPrevious);
        });
        Assert.Equal([0], result.BestScore);
        Assert.Equal([0], result.BestWipe);
        Assert.False(result.PodiumBoundaryTied);

        var copy = new ComparisonResult(result.Data);
        Assert.Equal(result.Ranking.Select(r => r.Name), copy.Ranking.Select(r => r.Name));
    }

    [Fact]
    public void Eliminated_entries_are_ranked_from_the_latest_rounds_then_by_score()
    {
        var attackers = new[] { Attacker("A", 20), Attacker("B", 16), Attacker("C", 12), Attacker("D", 1), Attacker("E", 3) };
        var result = Comparator.Run(Comparison.Of(Squad, attackers), 100_000, seed: 5);

        Assert.Equal([10_000, 100_000], result.Rounds);
        Assert.Equal(["A", "B", "C", "E", "D"], result.Ranking.Select(r => r.Name));
        Assert.Equal([1, 2, 3, 4, 5], result.Ranking.Select(r => r.Rank));
    }

    // Données

    [Fact]
    public void Data_inconsistent_with_the_rounds_are_rejected()
    {
        var attackers = new[] { Attacker("A", 20), Attacker("B", 16), Attacker("C", 12), Attacker("D", 1) };
        var data = Comparator.Run(Comparison.Of(Squad, attackers), 100_000, seed: 5).Data;
        var entries = data.Entries.ToList();

        string Errors(ComparisonData d) => string.Join(" | ", d.Validate());
        Assert.Empty(data.Validate());
        Assert.Contains("simulations au lieu", Errors(data with { Entries = [.. entries.Take(3), entries[3] with { EliminatedInRound = null }] }));
        Assert.Contains("ronde impossible", Errors(data with { Entries = [.. entries.Take(3), entries[3] with { EliminatedInRound = 2 }] }));
        Assert.Contains("finaliste", Errors(data with { Rounds = [100_000, 200_000], Entries = [.. entries.Take(2), entries[2] with { EliminatedInRound = 1 }, entries[3]] }));
        Assert.Contains("plan des rondes", Errors(data with { Rounds = [100_000, 10_000] }));
    }

    /// <summary>JSON des données sans la durée, seule à changer d'un calcul à l'autre.</summary>
    private static string Comparable(SimulationData data) => SimulationJson.Serialize(data with { Elapsed = TimeSpan.Zero });
}
