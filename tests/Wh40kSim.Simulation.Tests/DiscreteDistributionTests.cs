using Wh40kSim.Simulation.Statistics;

namespace Wh40kSim.Simulation.Tests;

public class DiscreteDistributionTests
{
    // Valeurs 0, 1, 2 vues 1, 2 et 1 fois : moyenne 1, variance (1 + 0 + 1) / 4
    private static readonly DiscreteDistribution Small = new([1, 2, 1]);

    [Fact]
    public void Mean_and_standard_deviation_follow_the_histogram()
    {
        Assert.Equal(4, Small.Total);
        Assert.Equal(2, Small.MaxValue);
        Assert.Equal(1.0, Small.Mean);
        Assert.Equal(Math.Sqrt(0.5), Small.StdDev, 12);
    }

    [Fact]
    public void Mean_excess_is_measured_only_where_the_threshold_is_reached()
    {
        // Seuil 1 atteint 3 fois sur 4 : dépassements 0, 0 et 1 → 1/3
        Assert.Equal(1.0 / 3, Small.MeanExcessAbove(1), 12);
        Assert.Equal(0, Small.MeanExcessAbove(2));
        Assert.Equal(0, Small.MeanExcessAbove(3)); // jamais atteint
        Assert.Equal(Small.Mean, Small.MeanExcessAbove(0));
    }

    [Fact]
    public void Probabilities_cover_every_value_and_beyond()
    {
        Assert.Equal(0.25, Small.ProbabilityExactly(0));
        Assert.Equal(0.5, Small.ProbabilityExactly(1));
        Assert.Equal(0, Small.ProbabilityExactly(-1));
        Assert.Equal(0, Small.ProbabilityExactly(3));

        Assert.Equal(1, Small.ProbabilityAtLeast(-1));
        Assert.Equal(1, Small.ProbabilityAtLeast(0));
        Assert.Equal(0.75, Small.ProbabilityAtLeast(1));
        Assert.Equal(0.25, Small.ProbabilityAtLeast(2));
        Assert.Equal(0, Small.ProbabilityAtLeast(3));
    }

    [Fact]
    public void Percentile_is_the_smallest_value_reaching_the_cumulative_probability()
    {
        Assert.Equal(0, Small.Percentile(0.25));
        Assert.Equal(1, Small.Percentile(0.26));
        Assert.Equal(1, Small.Median);
        Assert.Equal(2, Small.Percentile(1));
    }

    [Fact]
    public void Central_range_goes_from_the_first_to_the_ninth_decile()
    {
        // 10 valeurs équiprobables : P(X ≤ 0) = 10 % et P(X ≤ 8) = 90 % pile. Une erreur d'arrondi sur les bornes
        // donnerait 9 au lieu de 8.
        var uniform = new DiscreteDistribution(Enumerable.Repeat(1L, 10).ToArray());

        Assert.Equal(new IntRange(0, 8), uniform.CentralRange);
    }

    [Fact]
    public void Rows_hide_negligible_values_at_both_ends_only()
    {
        // Sur 10 000 simulations : 0 et 1 au début et 5 à la fin sont sous le seuil (masqués) ;
        // 3 n'arrive jamais mais se trouve entre deux valeurs affichées (gardé).
        long[] counts = [0, 1, 4_000, 0, 5_998, 1];
        var distribution = new DiscreteDistribution(counts);

        var rows = distribution.Rows(minProbability: 0.0005);

        Assert.Equal([2, 3, 4], rows.Select(r => r.Value));
        Assert.Equal(new DistributionRow(2, 0.4, 0.9999), rows[0]);
        Assert.Equal(new DistributionRow(3, 0, 0.5999), rows[1]);
        Assert.Equal(new DistributionRow(4, 0.5998, 0.5999), rows[2]);
    }

    [Fact]
    public void Rows_use_the_default_threshold_of_half_a_per_mille()
    {
        var distribution = new DiscreteDistribution([9_995, 5]); // 0,05 % pile : gardée

        Assert.Equal([0, 1], distribution.Rows().Select(r => r.Value));
        Assert.Equal([0], new DiscreteDistribution([9_996, 4]).Rows().Select(r => r.Value));
    }

    [Fact]
    public void A_tail_spread_over_several_negligible_values_stays_visible_while_it_can_be_reached()
    {
        // Sur 10 000 simulations, 10 (0,1 %) atteignent 3 ou plus, étalés sur 3 à 7 : chaque valeur seule est sous le
        // seuil de 0,05 %, mais « au moins 3 » (0,1 %) et « au moins 4 » (0,06 %) ne le sont pas
        long[] counts = [5_000, 4_990, 0, 4, 3, 1, 1, 1];
        var distribution = new DiscreteDistribution(counts);

        var rows = distribution.Rows(minProbability: 0.0005);

        Assert.Equal([0, 1, 2, 3, 4], rows.Select(r => r.Value));
        Assert.Equal(0.001, rows[3].AtLeast, precision: 10);
    }

    [Fact]
    public void Rows_always_keep_at_least_one_value()
    {
        var rows = new DiscreteDistribution([0, 0, 1]).Rows(minProbability: 2);

        Assert.Single(rows);
    }

    [Fact]
    public void Histogram_is_copied()
    {
        long[] counts = [1, 1];
        var distribution = new DiscreteDistribution(counts);

        counts[0] = 100;

        Assert.Equal([1L, 1L], distribution.Counts);
    }

    [Theory]
    [InlineData(new long[0])]
    [InlineData(new long[] { 0, 0 })]
    [InlineData(new long[] { 3, -1 })]
    public void Invalid_histogram_is_rejected(long[] counts)
    {
        Assert.Throws<ArgumentException>(() => new DiscreteDistribution(counts));
    }
}
