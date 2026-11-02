using Bunit;
using Wh40kSim.Simulation.Statistics;
using Wh40kSim.UI.Components;

namespace Wh40kSim.UI.Tests.Components;

/// <summary>
/// Histogrammes superposés du comparateur : une série par gagnant (barres, courbe, moyenne, légende), la première
/// dessinée par-dessus, infobulle avec chaque série, légende cliquable, limite de l'unité.
/// </summary>
public class ComparisonChartTests : ComponentTestContext
{
    private static readonly ComparisonChart.ChartSeries[] Three =
    [
        new("1. Fort", new DiscreteDistribution([0, 10, 30, 60])),
        new("2. Moyen", new DiscreteDistribution([10, 40, 40, 10])),
        new("3. Faible", new DiscreteDistribution([50, 40, 10, 0])),
    ];

    private IRenderedComponent<ComparisonChart> RenderChart(IReadOnlyList<ComparisonChart.ChartSeries> series, int? unitSize = null) =>
        Render<ComparisonChart>(p => p
            .Add(c => c.Series, series)
            .Add(c => c.ValueTitle, "Figurines tuées : {0}")
            .Add(c => c.UnitSize, unitSize));

    [Fact]
    public void Each_series_has_its_bars_curve_mean_and_legend()
    {
        var chart = RenderChart(Three);

        for (int s = 0; s < 3; s++)
        {
            Assert.NotEmpty(chart.FindAll($"[data-test=cmp-bar-{s}]"));
            Assert.Single(chart.FindAll($"[data-test=cmp-box-{s}]"));
            Assert.Empty(chart.FindAll($"[data-test=cmp-curve-{s}]"));   // courbes « au moins k » sur demande
            Assert.Contains(Three[s].Label, chart.Find($"[data-test=cmp-legend-{s}]").TextContent);
        }
        // Valeur sans probabilité : pas de barre (le 3e ne tue jamais 3 figurines)
        Assert.Equal(3, chart.FindAll("[data-test=cmp-bar-2]").Count);
    }

    [Fact]
    public void The_best_series_is_drawn_on_top()
    {
        var chart = RenderChart(Three);

        var bars = chart.FindAll("rect.wh-cmp-bar").Select(b => b.GetAttribute("data-test")).ToList();
        Assert.True(bars.LastIndexOf("cmp-bar-2") < bars.IndexOf("cmp-bar-0"));
    }

    [Fact]
    public void The_tooltip_gives_every_series()
    {
        var chart = RenderChart(Three);

        chart.FindAll("[data-test=chart-column]")[2].MouseOver();

        var tooltip = chart.Find("[data-test=chart-tooltip]");
        Assert.Contains("Figurines tuées : 2", tooltip.TextContent);
        Assert.Contains("exactement 30,0", chart.Find("[data-test=chart-tooltip-0]").TextContent);
        Assert.Contains("au moins 90,0", chart.Find("[data-test=chart-tooltip-0]").TextContent);
        Assert.Contains("exactement 10,0", chart.Find("[data-test=chart-tooltip-2]").TextContent);
    }

    [Fact]
    public void A_click_on_the_legend_hides_and_shows_a_series()
    {
        var chart = RenderChart(Three);

        chart.Find("[data-test=cmp-legend-1]").Click();
        Assert.Empty(chart.FindAll("[data-test=cmp-bar-1]"));
        Assert.Empty(chart.FindAll("[data-test=cmp-box-1]"));
        Assert.Contains("wh-cmp-legend-hidden", chart.Find("[data-test=cmp-legend-1]").ClassName);

        chart.Find("[data-test=cmp-legend-1]").Click();
        Assert.NotEmpty(chart.FindAll("[data-test=cmp-bar-1]"));
    }

    [Fact]
    public void The_last_visible_series_cannot_be_hidden()
    {
        var chart = RenderChart(Three);

        chart.Find("[data-test=cmp-legend-0]").Click();
        chart.Find("[data-test=cmp-legend-1]").Click();
        chart.Find("[data-test=cmp-legend-2]").Click();

        Assert.NotEmpty(chart.FindAll("[data-test=cmp-box-2]"));
    }

    [Fact]
    public void The_unit_limit_is_drawn_when_given()
    {
        Assert.Empty(RenderChart(Three).FindAll("[data-test=cmp-limit]"));

        var chart = RenderChart(Three, unitSize: 3);
        Assert.Single(chart.FindAll("[data-test=cmp-limit]"));
        Assert.Single(chart.FindAll("[data-test=legend-limit]"));
    }

    [Fact]
    public void The_at_least_curves_are_shown_on_demand()
    {
        var chart = RenderChart(Three);

        chart.Find("input[data-test=cmp-show-at-least]").Change(true);

        Assert.Equal(3, chart.FindAll("path.wh-cmp-curve").Count);
    }

    [Fact]
    public void The_box_plot_spans_the_quartiles_and_the_central_range()
    {
        // 1er : 10 % à 1, 30 % à 2, 60 % à 3 → quartiles 2 et 3, médiane 3, 10-90 % de 1 à 3
        var chart = RenderChart(Three);

        var box = chart.Find("[data-test=cmp-box-0]");
        var rect = box.QuerySelector("rect")!;
        var median = box.QuerySelector("line.wh-cmp-box-median")!;
        double width = double.Parse(rect.GetAttribute("width")!, System.Globalization.CultureInfo.InvariantCulture);
        double medianX = double.Parse(median.GetAttribute("x1")!, System.Globalization.CultureInfo.InvariantCulture);
        double left = double.Parse(rect.GetAttribute("x")!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(width > 0);
        Assert.Equal(left + width, medianX, 1);   // médiane = 3e quartile
    }

    [Fact]
    public void The_legend_is_in_a_column_beside_the_chart()
    {
        var chart = RenderChart(Three, unitSize: 3);

        var side = chart.Find(".wh-cmp-side");
        Assert.Equal(3, side.QuerySelectorAll("[data-test^=cmp-legend-]").Length);
        Assert.NotNull(side.QuerySelector("[data-test=legend-limit]"));
        Assert.NotNull(side.QuerySelector("input[data-test=cmp-show-at-least]"));
        Assert.Null(chart.Find(".wh-cmp-chart").QuerySelector("[data-test^=cmp-legend-]"));
    }

    [Fact]
    public void Two_series_are_enough()
    {
        var chart = RenderChart(Three[..2]);

        Assert.Equal(2, chart.FindAll("[data-test^=cmp-box-]").Count);
        Assert.Empty(chart.FindAll("[data-test=cmp-legend-2]"));
    }
}
