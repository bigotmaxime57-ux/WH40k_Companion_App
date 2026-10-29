using Bunit;
using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;
using Wh40kSim.Simulation;
using Wh40kSim.UI.Components;

namespace Wh40kSim.UI.Tests.Components;

/// <summary>Onglet « Armes » : répartition par pool, une section par pool, dégâts bruts toutes armes.</summary>
public class WeaponsReportTests : ComponentTestContext
{
    /// <summary>Canon, puis bolters et pistolets (lancés ensemble : un seul pool) contre 10 figurines à 2 PV.</summary>
    private static Scenario Squad() => new()
    {
        Attacker = new AttackerUnit
        {
            Weapons =
            [
                new WeaponGroup
                {
                    Weapon = new WeaponProfile { Name = "Canon", Attacks = DiceExpression.Parse("D6"), Skill = 3, Strength = 9, Damage = DiceExpression.Parse("D3") },
                },
                new WeaponGroup { ModelCount = 10, Weapon = new WeaponProfile { Name = "Bolter", Attacks = 2, Skill = 3, Strength = 4 } },
                new WeaponGroup { ModelCount = 2, Weapon = new WeaponProfile { Name = "Pistolet", Attacks = 1, Skill = 3, Strength = 4 } },
            ],
        },
        Defender = new DefenderUnit { Groups = [new ModelGroup { Name = "Cibles", Count = 10, Wounds = 2, Save = 4 }] },
    };

    private (IRenderedComponent<WeaponsReport> Report, SimulationResult Result) Render()
    {
        var result = Simulator.Run(Squad(), 5_000, seed: 4);
        return (Render<WeaponsReport>(p => p.Add(c => c.Result, result)), result);
    }

    [Fact]
    public void The_summary_has_a_share_bar_and_one_row_per_pool_named_after_its_weapons()
    {
        var (report, result) = Render();

        Assert.Equal(2, report.FindAll("[data-test=weapons-share-bar] [data-test=share-part]").Count);
        var rows = report.FindAll("[data-test=weapons-table] [data-test^=weapons-row-]");
        Assert.Equal(2, rows.Count);
        Assert.Contains("Canon", rows[0].TextContent);
        Assert.Contains("Bolter + Pistolet", rows[1].TextContent);
        Assert.Contains(result.Pools[1].DamageShare.ToString("P0"), rows[1].TextContent);
        Assert.Contains(result.Pools[0].WastedShare.ToString("P0"), rows[0].TextContent);
    }

    [Fact]
    public void The_highest_value_of_each_column_is_highlighted()
    {
        var (report, result) = Render();

        var rows = report.FindAll("[data-test=weapons-table] [data-test^=weapons-row-]");
        int best = result.Pools[0].Damage.Mean > result.Pools[1].Damage.Mean ? 0 : 1;
        Assert.Contains("wh-top", rows[best].QuerySelector("[data-test=weapons-damage]")!.ClassList);
        Assert.DoesNotContain("wh-top", rows[1 - best].QuerySelector("[data-test=weapons-damage]")!.ClassList);
        // Exactement une mise en avant par colonne (pas d'ex æquo ici), le gaspillage dans la teinte d'alerte
        Assert.Equal(3, report.FindAll("[data-test=weapons-table] td.wh-top").Count);
        Assert.Single(report.FindAll("[data-test=weapons-table] td.wh-top-bad"));
    }

    [Fact]
    public void Each_pool_has_its_own_section_with_tiles_and_a_histogram()
    {
        var (report, result) = Render();

        var sections = report.FindAll("[data-test^=weapon-pool-]");
        Assert.Equal(2, sections.Count);
        var canon = sections[0];
        Assert.Contains("Canon", canon.QuerySelector("[data-test=group-title]")!.TextContent);
        Assert.Contains(result.Pools[0].MeanKills.ToString("F2"), canon.QuerySelector("[data-test=pool-killed]")!.TextContent);
        Assert.Contains(result.Pools[0].MeanRawDamage.ToString("F2"), canon.QuerySelector("[data-test=pool-raw]")!.TextContent);
        Assert.Contains(result.Pools[1].ProbabilityUnitAlreadyDestroyed.ToString("P1"),
            sections[1].QuerySelector("[data-test=pool-already-destroyed]")!.TextContent);
        // Figurines tuées puis PV retirés
        Assert.Equal(2, canon.QuerySelectorAll("[data-test=histogram]").Length);
    }

    [Fact]
    public void The_waste_section_shows_what_is_lost_per_activation()
    {
        var (report, result) = Render();

        var raw = report.Find("[data-test=raw-damage]");
        Assert.Contains(result.RawDamage.Mean.ToString("F2"), raw.QuerySelector("[data-test=raw-mean]")!.TextContent);
        Assert.Contains(result.WastedShare.ToString("P1"), raw.QuerySelector("[data-test=raw-waste]")!.TextContent);
        Assert.Contains(result.Waste.ProbabilityExactly(0).ToString("P1"), raw.QuerySelector("[data-test=raw-no-waste]")!.TextContent);
        Assert.Contains(result.MeanWasteWhenUnitSurvives!.Value.ToString("F2"), raw.QuerySelector("[data-test=raw-waste-survives]")!.TextContent);
        Assert.Contains(result.MeanWastedDamage.ToString("F2"), raw.QuerySelector("[data-test=raw-waste-bar] [data-test=split-secondary-value]")!.TextContent);
        // Histogramme des PV perdus, plus de superposition
        Assert.NotNull(raw.QuerySelector("[data-test=histogram]"));
        Assert.Empty(raw.QuerySelectorAll("[data-test=chart-overlay]"));
        // Empilé par arme : une partie par arme dans chaque barre non nulle, légende aux noms des armes
        Assert.NotEmpty(raw.QuerySelectorAll("[data-test=chart-bar-stack]"));
        Assert.Equal(0, raw.QuerySelectorAll("[data-test=chart-bar-stack]").Length % 2);
        Assert.Contains("Bolter + Pistolet", raw.QuerySelector(".wh-chart-legend")!.TextContent);
    }

    [Fact]
    public void Each_pool_shows_its_raw_damage_split_between_dealt_and_lost()
    {
        var (report, result) = Render();

        var bar = report.Find("[data-test=weapon-pool-0] [data-test=pool-waste-bar]");
        Assert.Contains(result.Pools[0].MeanWastedDamage.ToString("F2"), bar.QuerySelector("[data-test=split-secondary-value]")!.TextContent);
        Assert.Contains(result.Pools[0].Damage.Mean.ToString("F2"), bar.QuerySelector("[data-test=split-primary-value]")!.TextContent);
    }
}
