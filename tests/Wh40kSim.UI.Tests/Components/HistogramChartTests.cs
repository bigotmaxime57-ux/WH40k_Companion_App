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
        Assert.Empty(chart.FindAll("[data-test=legend-limit]"));
        Assert.Empty(chart.FindAll("[data-test=chart-tooltip]"));
        // Sans effectif d'unité, rien n'est hachuré ni annoncé dans la légende
        Assert.Empty(chart.FindAll("[data-test=chart-hatch]"));
        Assert.Empty(chart.FindAll("[data-test=legend-wiped]"));
    }

    [Fact]
    public void Split_bars_are_stacked_in_two_parts_with_both_shares_in_the_tooltip()
    {
        // Valeurs 0 à 3 : à 2, les trois quarts viennent de la première partie
        var chart = Render<HistogramChart>(p => p
            .Add(c => c.Distribution, Sample)
            .Add(c => c.ValueTitle, "PV retirés : {0}")
            .Add(c => c.Split, new HistogramChart.BarSplit([0, 1, 0.75, 0.5], "Armes Precision", "Autres armes")));

        var primary = chart.FindAll("[data-test=chart-bar]");
        var secondary = chart.FindAll("[data-test=chart-bar-secondary]");
        Assert.Equal(4, primary.Count);
        Assert.Equal(4, secondary.Count);
        double Height(AngleSharp.Dom.IElement e) => double.Parse(e.GetAttribute("height")!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(3 * Height(secondary[2]), Height(primary[2]), 1);   // 75 % / 25 %

        chart.FindAll("[data-test=chart-column]")[2].MouseOver();
        var tooltip = chart.Find("[data-test=chart-tooltip-split]").TextContent;
        Assert.Contains("Armes Precision : 75", tooltip);
        Assert.Contains("Autres armes : 25", tooltip);
        Assert.Contains("Armes Precision", chart.Find("[data-test=legend-split]").TextContent);
    }

    [Fact]
    public void Stacked_bars_have_one_coloured_part_per_series_with_each_share_in_the_tooltip()
    {
        var chart = Render<HistogramChart>(p => p
            .Add(c => c.Distribution, Sample)
            .Add(c => c.ValueTitle, "PV perdus : {0}")
            .Add(c => c.Stack, new HistogramChart.BarStack([
                new HistogramChart.StackPart("Canon", "wh-share-0", [0, 1, 0.75, 0.5]),
                new HistogramChart.StackPart("Fusil", "wh-share-1", [0, 0, 0.25, 0.5]),
            ])));

        // Valeur 0 : barre simple, neutre (d'aucune série) ; valeurs 1 à 3 : deux parties chacune
        Assert.Contains("wh-chart-bar-neutral", Assert.Single(chart.FindAll("[data-test=chart-bar]")).ClassList);
        var parts = chart.FindAll("[data-test=chart-bar-stack]");
        Assert.Equal(6, parts.Count);
        double Height(AngleSharp.Dom.IElement e) => double.Parse(e.GetAttribute("height")!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(3 * Height(parts[3]), Height(parts[2]), 1);   // valeur 2 : 75 % / 25 %
        Assert.Contains("wh-share-1", parts[3].ClassList);

        chart.FindAll("[data-test=chart-column]")[2].MouseOver();
        var tooltip = chart.Find("[data-test=chart-tooltip-stack]").TextContent;
        Assert.Contains("Canon : 75", tooltip);
        Assert.Contains("Fusil : 25", tooltip);
        Assert.Contains("Fusil", chart.Find(".wh-chart-legend").TextContent);
    }

    [Fact]
    public void An_overlay_is_drawn_in_outline_over_the_union_of_both_value_ranges()
    {
        // Bruts de 0 à 3 ; superposée de 0 à 5 : les colonnes vont jusqu'à 5
        var chart = Render<HistogramChart>(p => p
            .Add(c => c.Distribution, Sample)
            .Add(c => c.ValueTitle, "Dégâts bruts : {0}")
            .Add(c => c.Overlay, new DiscreteDistribution(new long[] { 100, 100, 100, 100, 300, 300 }))
            .Add(c => c.OverlayLabel, "PV retirés"));

        Assert.Equal(6, chart.FindAll("[data-test=chart-column]").Count);
        Assert.Equal(6, chart.FindAll("[data-test=chart-overlay]").Count);
        chart.FindAll("[data-test=chart-column]")[4].MouseOver();
        Assert.Contains("PV retirés : 30,0", chart.Find("[data-test=chart-tooltip-overlay]").TextContent);
        Assert.Contains("PV retirés", chart.Find("[data-test=legend-overlay]").TextContent);
    }

    [Fact]
    public void Each_interval_between_two_gridlines_is_split_in_five_by_dotted_lines()
    {
        var chart = RenderChart();

        int major = chart.FindAll("line.wh-chart-grid").Count;
        var minor = chart.FindAll("[data-test=chart-grid-minor]");
        Assert.True(major >= 2);
        Assert.Equal((major - 1) * 4, minor.Count);
        // Sans libellé : seules les graduations principales ont un texte sur l'axe de gauche
        Assert.Equal(major, chart.FindAll("text.wh-chart-axis[text-anchor=end]").Count);
    }

    [Fact]
    public void The_smooth_curve_skips_impossible_values_but_their_column_keeps_its_tooltip()
    {
        // Dégâts par pas de 2 : 1 et 3 n'arrivent jamais
        var chart = Render<HistogramChart>(p => p
            .Add(c => c.Distribution, new DiscreteDistribution(new long[] { 100, 0, 400, 0, 500 }))
            .Add(c => c.ValueTitle, "PV retirés : {0}"));

        Assert.StartsWith("M", chart.Find("[data-test=chart-curve]").GetAttribute("d"));
        Assert.Equal(3, chart.FindAll("circle.wh-chart-dot").Count);
        Assert.Equal(5, chart.FindAll("[data-test=chart-column]").Count);

        chart.FindAll("[data-test=chart-column]")[3].MouseOver();
        Assert.Contains("Au moins : 50,0 %", chart.Find("[data-test=chart-tooltip]").TextContent);
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
        Assert.Contains("Limite de l'unité", chart.Find("[data-test=legend-limit]").TextContent);
        Assert.Contains("Moyenne marge comprise : 1,60", chart.Find("[data-test=legend-mean]").TextContent);
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
