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
        // Sans effectif d'unité, rien n'est hachuré ni annoncé dans la légende
        Assert.Empty(chart.FindAll("[data-test=chart-hatch]"));
        Assert.Empty(chart.FindAll("[data-test=legend-wiped]"));
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
        Assert.DoesNotContain("chance", tooltip);
        Assert.DoesNotContain("P(", chart.Markup);

        chart.FindAll("[data-test=chart-column]")[2].MouseOut();
        Assert.Empty(chart.FindAll("[data-test=chart-tooltip]"));
    }

    [Fact]
    public void The_whole_unit_column_is_hatched_and_explained_in_the_legend()
    {
        var chart = RenderChart(unitSize: 3);

        Assert.Single(chart.FindAll("[data-test=chart-hatch]"));
        Assert.Contains("Unité détruite", chart.Find("[data-test=legend-wiped]").TextContent);
        Assert.Empty(chart.FindAll("line.wh-chart-limit"));
        Assert.Empty(chart.FindAll("rect.wh-chart-bar-beyond"));

        chart.FindAll("[data-test=chart-column]")[3].MouseOver();
        Assert.Contains("Unité détruite", chart.Find("[data-test=chart-tooltip]").TextContent);
        chart.FindAll("[data-test=chart-column]")[2].MouseOver();
        Assert.DoesNotContain("Unité détruite", chart.Find("[data-test=chart-tooltip]").TextContent);
    }

    [Fact]
    public void Beyond_the_unit_columns_are_dimmed_with_the_exact_number_on_the_axis()
    {
        var chart = RenderChart(unitSize: 2, extended: true);

        Assert.Single(chart.FindAll("rect.wh-chart-bar-beyond"));
        Assert.Single(chart.FindAll("line.wh-chart-limit"));
        // Unité entière (2) et au-delà (3) : toutes deux hachurées
        Assert.Equal(2, chart.FindAll("[data-test=chart-hatch]").Count);
        var axisLabels = chart.FindAll("text.wh-chart-axis").Select(t => t.TextContent).ToList();
        Assert.Contains("3", axisLabels);
        Assert.DoesNotContain(axisLabels, label => label.Contains('+'));
        chart.FindAll("[data-test=chart-column]")[3].MouseOver();
        Assert.Contains("Figurines tuées : 3 (2 + 1)", chart.Find("[data-test=chart-tooltip]").TextContent);
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
