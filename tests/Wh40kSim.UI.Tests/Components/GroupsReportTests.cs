using Bunit;
using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;
using Wh40kSim.Simulation;
using Wh40kSim.UI.Components;

namespace Wh40kSim.UI.Tests.Components;

/// <summary>Onglet « Groupes » : pertes par groupe, gardes du corps et frappe Precision, affichés selon le scénario.</summary>
public class GroupsReportTests : ComponentTestContext
{
    private static Scenario Scenario(bool precision, string? precisionTarget = null, bool characters = true) => new()
    {
        Attacker = new AttackerUnit
        {
            Weapons =
            [
                new WeaponGroup
                {
                    ModelCount = 10,
                    Weapon = new WeaponProfile
                    {
                        Skill = 3, Strength = 5, Damage = DiceExpression.Parse("D3"),
                        Abilities = new WeaponAbilities { Precision = precision },
                    },
                },
            ],
        },
        Defender = new DefenderUnit
        {
            Groups = characters
                ?
                [
                    new ModelGroup { Name = "Gardes", Count = 5, Wounds = 2, Save = 4 },
                    new ModelGroup { Name = "Leader", IsCharacter = true, Wounds = 4, Save = 3 },
                    new ModelGroup { Name = "Soutien", IsCharacter = true, Wounds = 3, Save = 4 },
                ]
                : [new ModelGroup { Name = "Cibles", Count = 10 }],
        },
        Context = new AttackContext { PrecisionTarget = precisionTarget },
    };

    private IRenderedComponent<GroupsReport> Report(Scenario scenario) =>
        Render<GroupsReport>(p => p.Add(c => c.Result, Simulator.Run(scenario, 5_000, seed: 3)));

    [Fact]
    public void Every_group_has_a_card_in_allocation_order()
    {
        var report = Report(Scenario(precision: false));

        var cards = report.FindAll("[data-test^=group-losses-]");
        Assert.Equal(3, cards.Count);
        Assert.Contains("Gardes", cards[0].TextContent);
        Assert.Contains("Leader", cards[1].TextContent);
        Assert.Contains("PERSONNAGE tué", cards[1].TextContent);
        Assert.Contains("Groupe détruit", cards[0].TextContent);
        Assert.Contains("wh-group-section-character", cards[1].ClassName);
        Assert.DoesNotContain("wh-group-section-character", cards[0].ClassName);
        Assert.Empty(report.FindAll("[data-test=groups-report] .wh-subcard"));   // sections sans cadre, dans tout l'onglet
        // Chaque carte s'ouvre sur un sous-titre : numéro d'allocation et nom du groupe
        Assert.Equal(["1Gardes", "2LeaderPERSONNAGE", "3SoutienPERSONNAGE"],
            cards.Select(c => c.QuerySelector("[data-test=group-title]")!.TextContent.Replace(" ", "").Replace("\n", "").Replace("\r", "")));
    }

    [Fact]
    public void Tiles_follow_the_distributions_order_and_wipe_tiles_are_coloured_by_reliability()
    {
        var result = Simulator.Run(Scenario(precision: false), 5_000, seed: 3);
        var report = Render<GroupsReport>(p => p.Add(c => c.Result, result));

        // Figurines tuées, PV retirés, puis destruction (comme l'onglet Distributions)
        var tiles = report.FindAll("[data-test=group-losses-0] .wh-tiles > [data-test]").Select(t => t.GetAttribute("data-test"));
        Assert.Equal(["group-killed", "group-damage", "group-wiped"], tiles);
        var guards = report.FindAll("[data-test=bodyguards] .wh-tiles > [data-test]").Select(t => t.GetAttribute("data-test"));
        Assert.Equal(["bodyguards-killed", "bodyguards-damage", "bodyguards-wiped"], guards);

        string Tone(double p) => "wh-tile-" + SummaryTiles.WipeToneFor(p).ToString().ToLowerInvariant();
        Assert.Contains(Tone(result.Groups[0].ProbabilityWiped), report.Find("[data-test=group-losses-0] [data-test=group-wiped]").ClassName);
        Assert.Contains(Tone(result.ProbabilityBodyguardsWiped!.Value), report.Find("[data-test=bodyguards-wiped]").ClassName);
    }

    [Fact]
    public void A_summary_table_lists_every_group_before_the_cards()
    {
        var report = Report(Scenario(precision: false));

        var rows = report.FindAll("[data-test=groups-summary] [data-test^=group-summary-]");
        Assert.Equal(3, rows.Count);
        Assert.Contains("Gardes", rows[0].TextContent);
        Assert.Contains("/ 5", rows[0].TextContent);   // figurines tuées sur l'effectif
        Assert.Contains("/ 10", rows[0].TextContent);  // PV retirés sur les PV du groupe
        Assert.Contains("PERSONNAGE", rows[1].TextContent);
        Assert.Contains("—", rows[1].TextContent);     // une figurine seule : pas de colonne des figurines tuées
    }

    [Fact]
    public void Wipe_probability_is_a_bar_coloured_by_reliability()
    {
        var report = Report(Scenario(precision: false));

        foreach (var (losses, bar) in Simulator.Run(Scenario(precision: false), 5_000, seed: 3).Groups
                     .Zip(report.FindAll("[data-test=groups-summary] [data-test=group-wipe-bar]")))
        {
            string tone = SummaryTiles.WipeToneFor(losses.ProbabilityWiped).ToString().ToLowerInvariant();
            Assert.Contains($"wh-wipe-{tone}", bar.ClassName);
            Assert.Contains(losses.ProbabilityWiped.ToString("P1"), bar.TextContent);
        }
        Assert.Equal(3, report.FindAll("[data-test=group-wipe-bar] .wh-wipe-fill").Count);
    }

    [Theory]
    [InlineData(0.30, StatTile.TileTone.Bad)]
    [InlineData(0.60, StatTile.TileTone.Neutral)]
    [InlineData(0.90, StatTile.TileTone.Good)]
    public void Wipe_tone_follows_the_reliability_thresholds(double probability, StatTile.TileTone expected) =>
        Assert.Equal(expected, SummaryTiles.WipeToneFor(probability));

    [Fact]
    public void A_group_of_several_models_shows_both_histograms_a_single_model_only_wounds()
    {
        var report = Report(Scenario(precision: false));

        // Gardes : figurines tuées et PV retirés ; leader (une figurine) : pas de tuile des figurines, histogramme des PV
        Assert.Equal(2, report.FindAll("[data-test=group-losses-0] [data-test=histogram]").Count);
        Assert.Single(report.FindAll("[data-test=group-losses-0] [data-test=group-killed]"));
        Assert.Single(report.FindAll("[data-test=group-losses-1] [data-test=histogram]"));
        Assert.Empty(report.FindAll("[data-test=group-losses-1] [data-test=group-killed]"));
        Assert.Contains("/ 4", report.Find("[data-test=group-losses-1] [data-test=group-damage]").TextContent);
    }

    [Fact]
    public void Each_section_can_be_collapsed_on_its_own()
    {
        var report = Report(Scenario(precision: true));

        // Bouton de repli commun, à gauche du titre comme partout ailleurs
        var header = report.Find("[data-test=group-losses] .wh-section-header");
        Assert.Equal("toggle-section", header.FirstElementChild!.GetAttribute("data-test"));
        Assert.Contains("wh-collapse-toggle", header.FirstElementChild.ClassName);

        report.Find("[data-test=group-losses] [data-test=toggle-section]").Click();
        report.Find("[data-test=bodyguards] [data-test=toggle-section]").Click();

        Assert.Empty(report.FindAll("[data-test=groups-summary]"));
        Assert.Empty(report.FindAll("[data-test^=group-losses-]"));
        Assert.Empty(report.FindAll("[data-test=bodyguards-wiped]"));
        Assert.NotEmpty(report.FindAll("[data-test^=precision-target-]"));

        report.Find("[data-test=group-losses] [data-test=toggle-section]").Click();
        Assert.Equal(3, report.FindAll("[data-test^=group-losses-]").Count);
    }

    [Fact]
    public void Bodyguards_are_reported_when_they_protect_a_character()
    {
        var report = Report(Scenario(precision: false));

        Assert.NotNull(report.Find("[data-test=bodyguards]"));
        Assert.Contains("/ 5", report.Find("[data-test=bodyguards-killed]").TextContent);
        Assert.Contains("/ 10", report.Find("[data-test=bodyguards-damage]").TextContent);   // 5 gardes à 2 PV
        Assert.Equal(2, report.FindAll("[data-test=bodyguards] [data-test=histogram]").Count);
    }

    [Fact]
    public void Without_a_character_there_is_neither_bodyguard_nor_precision_section()
    {
        var report = Report(Scenario(precision: true, characters: false));

        Assert.Single(report.FindAll("[data-test^=group-losses-]"));
        Assert.Empty(report.FindAll("[data-test=bodyguards]"));
        Assert.Empty(report.FindAll("[data-test=precision]"));
    }

    [Fact]
    public void Precision_section_appears_only_with_a_precision_weapon()
    {
        Assert.Empty(Report(Scenario(precision: false)).FindAll("[data-test=precision]"));

        var report = Report(Scenario(precision: true));
        Assert.Single(report.FindAll("[data-test^=precision-target-]"));   // le seul PERSONNAGE visé : le premier, sans choix
        Assert.Contains("Leader", report.Find("[data-test=precision-target-1]").TextContent);
        Assert.Empty(report.FindAll("[data-test=precision-share]"));   // ancienne tuile « Grâce à Precision » retirée
    }

    [Fact]
    public void Each_precision_target_shows_what_the_precision_weapons_do()
    {
        var scenario = Scenario(precision: true);
        var result = Simulator.Run(scenario, 5_000, seed: 3);
        var report = Render<GroupsReport>(p => p.Add(c => c.Result, result));
        var leader = result.Groups[1];
        var work = leader.PrecisionWeapons!;

        var target = report.Find("[data-test=precision-target-1]");
        var killBar = target.QuerySelector("[data-test=precision-kill-bar]")!;
        Assert.Contains(leader.ProbabilityWiped.ToString("P1"), killBar.TextContent);
        // Couleur de fiabilité du kill, comme la tuile « Unité détruite »
        string tone = SummaryTiles.WipeToneFor(leader.ProbabilityWiped).ToString().ToLowerInvariant();
        Assert.Contains($"wh-split-bar-{tone}", killBar.ClassName);
        Assert.Contains("wh-split-bar-neutral", target.QuerySelector("[data-test=precision-damage-bar]")!.ClassName);
        Assert.Contains(work.ProbabilityKilledByPrecisionAlone.ToString("P1"), killBar.QuerySelector("[data-test=split-primary-value]")!.TextContent);
        Assert.Contains(work.ProbabilityKilledWithHelp.ToString("P1"), killBar.QuerySelector("[data-test=split-secondary-value]")!.TextContent);
        var damageBar = target.QuerySelector("[data-test=precision-damage-bar]")!;
        Assert.Contains("/ 4", damageBar.TextContent);
        Assert.Contains(work.MeanPrecisionDamage.ToString("F2"), damageBar.QuerySelector("[data-test=split-primary-value]")!.TextContent);
        Assert.Contains(work.ProbabilityKilledWhileBodyguardsAlive.ToString("P1"),
            target.QuerySelector("[data-test=precision-guards-alive]")!.TextContent);
        Assert.Contains("PV restants", target.QuerySelector("[data-test=precision-short]")!.TextContent);
        // Histogramme empilé : une seconde partie (autres armes) sur chaque barre
        Assert.NotEmpty(target.QuerySelectorAll("[data-test=chart-bar-secondary]"));
        Assert.NotNull(target.QuerySelector("[data-test=legend-split]"));
    }

    [Fact]
    public void A_chosen_precision_target_is_the_only_one_shown()
    {
        var report = Report(Scenario(precision: true, precisionTarget: "Soutien"));

        Assert.Single(report.FindAll("[data-test^=precision-target-]"));
        Assert.Contains("Soutien", report.Find("[data-test=precision-target-2]").TextContent);
    }
}
