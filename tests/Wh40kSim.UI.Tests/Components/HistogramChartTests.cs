using Bunit;
using Wh40kSim.Simulation.Statistics;
using Wh40kSim.UI.Components;

namespace Wh40kSim.UI.Tests.Components;

/// <summary>Histogramme SVG : une barre par valeur non négligeable, infobulle en mots, marge, tableau en mots.</summary>
public class HistogramChartTests : ComponentTestContext
{
    // 0 : 10 %, 1 : 40 %, 2 : 30 %, 3 : 20 %
    private static readonly DiscreteDistribution Sample = new(new long[] { 100, 400, 300, 200 });

    private IRenderedComponent<HistogramChart> RenderChart(int? unitSize = null, bool extended = false) =>
        Render<HistogramChart>(p => p
            .Add(c => c.Distribution, Sample)
            .Add(c => c.ValueTitle, "Figurines tuées : {0}")
            .Add(c => c.UnitSize, unitSize)
            .Add(c => c.Extended, extended));

    [Fact]
    public void One_bar_and_one_hover_zone_per_value()
    {
        var chart = RenderChart();

        Assert.Equal(4, chart.FindAll("[data-test=chart-bar]").Count);
        Assert.Equal(4, chart.FindAll("[data-test=chart-column]").Count);
        Assert.NotNull(chart.Find("[data-test=chart-mean]"));
        Assert.Contains("Moyenne : 1,60", chart.Markup);
        Assert.Empty(chart.FindAll("[data-test=chart-tooltip]"));
    }

    [Fact]
    public void Hovering_a_column_shows_the_probabilities_in_words()
    {
        var chart = RenderChart();

        chart.FindAll("[data-test=chart-column]")[2].MouseOver();

        string tooltip = chart.Find("[data-test=chart-tooltip]").TextContent;
        Assert.Contains("Figurines tuées : 2", tooltip);
        Assert.Contains("Exactement : 30,0 %", tooltip);
        Assert.Contains("Au moins : 50,0 %", tooltip);
        Assert.Contains("1 chance sur 2", tooltip);
        Assert.DoesNotContain("P(", chart.Markup);

        chart.FindAll("[data-test=chart-column]")[2].MouseOut();
        Assert.Empty(chart.FindAll("[data-test=chart-tooltip]"));
    }

    [Fact]
    public void Certain_values_have_no_odds()
    {
        var chart = RenderChart();

        chart.FindAll("[data-test=chart-column]")[0].MouseOver();

        Assert.DoesNotContain("chance", chart.Find("[data-test=chart-tooltip]").TextContent);
    }

    [Fact]
    public void The_whole_unit_column_is_marked_as_destroyed_without_any_margin_line()
    {
        var chart = RenderChart(unitSize: 3);

        Assert.Equal("détruite", chart.Find("[data-test=chart-wiped]").TextContent);
        Assert.Empty(chart.FindAll("line.wh-chart-limit"));
        Assert.Empty(chart.FindAll("rect.wh-chart-bar-beyond"));

        chart.FindAll("[data-test=chart-column]")[3].MouseOver();
        Assert.Contains("Unité détruite", chart.Find("[data-test=chart-tooltip]").TextContent);
        chart.FindAll("[data-test=chart-column]")[2].MouseOver();
        Assert.DoesNotContain("Unité détruite", chart.Find("[data-test=chart-tooltip]").TextContent);
    }

    [Fact]
    public void Beyond_the_unit_columns_are_dimmed_and_labelled_with_the_margin()
    {
        var chart = RenderChart(unitSize: 2, extended: true);

        Assert.Single(chart.FindAll("rect.wh-chart-bar-beyond"));
        Assert.Single(chart.FindAll("line.wh-chart-limit"));
        Assert.Empty(chart.FindAll("[data-test=chart-wiped]"));
        chart.FindAll("[data-test=chart-column]")[3].MouseOver();
        Assert.Contains("Figurines tuées : 2 + 1", chart.Find("[data-test=chart-tooltip]").TextContent);
    }

    [Fact]
    public void Distribution_table_headers_are_in_words()
    {
        var table = Render<DistributionTable>(p => p
            .Add(c => c.Title, "Figurines tuées")
            .Add(c => c.ValueHeader, "Figurines")
            .Add(c => c.Distribution, Sample));

        var headers = table.FindAll("th").Select(th => th.TextContent).ToList();
        Assert.Equal(["Figurines", "Exactement", "Au moins"], headers);
    }
}
