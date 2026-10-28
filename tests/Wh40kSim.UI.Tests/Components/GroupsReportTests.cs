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
        Assert.Contains("wh-subcard-character", cards[1].ClassName);
        Assert.DoesNotContain("wh-subcard-character", cards[0].ClassName);
        Assert.Equal(3, report.FindAll("[data-test^=group-losses-] [data-test=histogram]").Count);
    }

    [Fact]
    public void A_single_model_group_shows_wounds_instead_of_models()
    {
        var report = Report(Scenario(precision: false));

        // Gardes : figurines tuées ; leader (une figurine) : pas de tuile des figurines, histogramme des PV (0 à 4)
        Assert.Single(report.FindAll("[data-test=group-losses-0] [data-test=group-killed]"));
        Assert.Empty(report.FindAll("[data-test=group-losses-1] [data-test=group-killed]"));
        Assert.Contains("/ 4", report.Find("[data-test=group-losses-1] [data-test=group-damage]").TextContent);
    }

    [Fact]
    public void Bodyguards_are_reported_when_they_protect_a_character()
    {
        var report = Report(Scenario(precision: false));

        Assert.NotNull(report.Find("[data-test=bodyguards]"));
        Assert.Contains("/ 5", report.Find("[data-test=bodyguards-killed]").TextContent);
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
        Assert.Equal(2, report.FindAll("[data-test^=precision-target-]").Count);
        var share = report.Find("[data-test=precision-target-1] [data-test=precision-share]");
        Assert.Contains("Grâce à Precision", share.TextContent);
        Assert.Equal(2, share.QuerySelectorAll(".wh-tile-stat").Length);
    }

    [Fact]
    public void A_chosen_precision_target_is_the_only_one_shown()
    {
        var report = Report(Scenario(precision: true, precisionTarget: "Soutien"));

        Assert.Single(report.FindAll("[data-test^=precision-target-]"));
        Assert.Contains("Soutien", report.Find("[data-test=precision-target-2]").TextContent);
    }
}
