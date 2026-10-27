using Bunit;
using Wh40kSim.Core.Model;
using Wh40kSim.Simulation;
using Wh40kSim.UI.Components;
using Wh40kSim.UI.Forms;

namespace Wh40kSim.UI.Tests.Components;

public class DefenderPanelTests : ComponentTestContext
{
    private static DefenderForm ThreeGroups() => new(new DefenderUnit
    {
        Groups =
        [
            new ModelGroup { Name = "A", Count = 5, Toughness = 4 },
            new ModelGroup { Name = "B", Count = 2, Wounds = 2, Toughness = 5 },
            new ModelGroup { Name = "C", IsCharacter = true, Toughness = 6 },
        ],
    });

    private IRenderedComponent<DefenderPanel> RenderPanel(DefenderForm form) =>
        Render<DefenderPanel>(p => p.Add(c => c.Defender, form));

    [Fact]
    public void Moving_down_swaps_a_group_with_the_next_one()
    {
        var form = ThreeGroups();
        var panel = RenderPanel(form);

        panel.FindAll("[data-test=move-down]")[0].Click();

        Assert.Equal(["B", "A", "C"], form.Groups.Select(g => g.Name));
    }

    [Fact]
    public void Moving_up_swaps_a_group_with_the_previous_one()
    {
        var form = ThreeGroups();
        var panel = RenderPanel(form);

        panel.FindAll("[data-test=move-up]")[2].Click();

        Assert.Equal(["A", "C", "B"], form.Groups.Select(g => g.Name));
    }

    [Fact]
    public void First_group_cannot_move_up_and_last_cannot_move_down()
    {
        var panel = RenderPanel(ThreeGroups());

        Assert.True(panel.FindAll("[data-test=move-up]")[0].HasAttribute("disabled"));
        Assert.False(panel.FindAll("[data-test=move-up]")[1].HasAttribute("disabled"));
        Assert.True(panel.FindAll("[data-test=move-down]")[2].HasAttribute("disabled"));
    }

    [Fact]
    public void New_group_takes_the_toughness_of_the_previous_one()
    {
        var form = ThreeGroups();
        var panel = RenderPanel(form);

        panel.Find("[data-test=add-group]").Click();

        Assert.Equal(4, form.Groups.Count);
        Assert.Equal(6, form.Groups[^1].Toughness);
        Assert.Equal(new ModelGroup().Wounds, form.Groups[^1].Wounds);
    }

    [Fact]
    public void Changes_are_reported_to_the_page()
    {
        int changes = 0;
        var panel = Render<DefenderPanel>(p => p
            .Add(c => c.Defender, ThreeGroups())
            .Add(c => c.Changed, () => changes++));

        panel.FindAll("[data-test=remove-group]")[1].Click();
        panel.FindAll("input[data-test=toughness]")[0].Change("7");

        Assert.Equal(2, changes);
    }
}

public class AttackerPanelTests : ComponentTestContext
{
    [Fact]
    public void Duplicating_inserts_an_independent_copy_after_the_original()
    {
        var form = new AttackerForm(new AttackerUnit
        {
            Weapons = [new WeaponGroup { Weapon = new WeaponProfile { Name = "Fusil" } }, new WeaponGroup { Weapon = new WeaponProfile { Name = "Épée" } }],
        });
        var panel = Render<AttackerPanel>(p => p.Add(c => c.Attacker, form));

        panel.FindAll("[data-test=duplicate-weapon]")[0].Click();

        Assert.Equal(["Fusil", "Fusil (copie)", "Épée"], form.Weapons.Select(w => w.Name));
        form.Weapons[1].Abilities.Torrent = true;
        Assert.False(form.Weapons[0].Abilities.Torrent);
    }

    [Fact]
    public void Weapons_can_be_added_and_removed()
    {
        var form = new AttackerForm();
        var panel = Render<AttackerPanel>(p => p.Add(c => c.Attacker, form));

        panel.Find("[data-test=add-weapon]").Click();
        panel.Find("[data-test=add-weapon]").Click();
        // Les boutons sont dans des infobulles MudBlazor qui finissent leur rendu de façon asynchrone : on attend que
        // les deux cartes soient stables avant de cliquer (sinon, rarement, le clic visait un bouton déjà remplacé)
        panel.WaitForAssertion(() => Assert.Equal(2, panel.FindAll("[data-test=remove-weapon]").Count));
        panel.FindAll("[data-test=remove-weapon]")[0].Click();

        panel.WaitForAssertion(() => Assert.Single(form.Weapons));
    }
}

public class ContextPanelTests : ComponentTestContext
{
    private static Scenario WithCharacters(int characters, bool precision) => new()
    {
        Attacker = new AttackerUnit { Weapons = [new WeaponGroup { Weapon = new WeaponProfile { Abilities = new WeaponAbilities { Precision = precision } } }] },
        Defender = new DefenderUnit
        {
            Groups = [new ModelGroup { Name = "Gardes", Count = 5 }, .. Enumerable.Range(1, characters).Select(i => new ModelGroup { Name = $"Leader {i}", IsCharacter = true })],
        },
    };

    private IRenderedComponent<ContextPanel> RenderPanel(Scenario reference, ContextForm? context = null) =>
        Render<ContextPanel>(p => p.Add(c => c.Context, context ?? new ContextForm()).Add(c => c.Reference, reference));

    [Theory]
    [InlineData(2, true, true)]
    [InlineData(1, true, false)]
    [InlineData(2, false, false)]
    public void Precision_target_is_offered_only_when_it_matters(int characters, bool precision, bool shown)
    {
        var panel = RenderPanel(WithCharacters(characters, precision));

        Assert.Equal(shown, panel.FindAll("[data-test=precision-target]").Count > 0);
    }

    [Fact]
    public void A_chosen_precision_target_stays_visible_so_it_can_be_removed()
    {
        var panel = RenderPanel(WithCharacters(1, precision: false), new ContextForm { PrecisionTarget = "Ancien leader" });

        Assert.NotEmpty(panel.FindAll("[data-test=precision-target]"));
    }

    [Fact]
    public void Characteristic_modifications_are_separate_from_roll_modifiers()
    {
        var context = new ContextForm();
        var panel = RenderPanel(new Scenario(), context);

        panel.Find("input[data-test=skill-modifier]").Change("2");

        Assert.Equal(2, context.SkillModifier);
        Assert.Equal(0, context.HitModifier);
        Assert.Contains("Modifications de caractéristiques", panel.Markup);
    }

    [Fact]
    public void Situation_options_follow_the_weapons()
    {
        var reference = new Scenario
        {
            Attacker = new AttackerUnit
            {
                Weapons =
                [
                    new WeaponGroup { Weapon = new WeaponProfile { Abilities = new WeaponAbilities { Heavy = true } } },
                    new WeaponGroup { Weapon = new WeaponProfile { Type = WeaponType.Melee, Abilities = new WeaponAbilities { Lance = true } } },
                ],
            },
        };

        var panel = RenderPanel(reference);

        Assert.Empty(panel.FindAll("[data-test=half-range]"));
        Assert.NotEmpty(panel.FindAll("[data-test=remained-stationary]"));
        Assert.NotEmpty(panel.FindAll("[data-test=charged]"));
    }
}

public class ResultsViewTests : ComponentTestContext
{
    private static SimulationResult Run(int defenders) => Simulator.Run(new Scenario
    {
        Attacker = new AttackerUnit { Weapons = [new WeaponGroup { ModelCount = 10, Weapon = new WeaponProfile { Attacks = 2, Skill = 3 } }] },
        Defender = new DefenderUnit { Groups = [new ModelGroup { Name = "Gardes", Count = defenders }] },
    }, 5_000, seed: 1);

    [Fact]
    public void Extended_view_is_offered_only_when_the_unit_can_be_destroyed()
    {
        Assert.Empty(Render<ResultsView>(p => p.Add(c => c.Result, Run(defenders: 100))).FindAll("[data-test=extended]"));
        Assert.NotEmpty(Render<ResultsView>(p => p.Add(c => c.Result, Run(defenders: 2))).FindAll("[data-test=extended]"));
    }

    [Fact]
    public void Extended_view_shows_values_beyond_the_unit_and_the_excess_profile()
    {
        var result = Run(defenders: 2);
        var view = Render<ResultsView>(p => p.Add(c => c.Result, result));
        int realRows = view.Find("[data-test=distribution]").QuerySelectorAll("tbody tr").Length;

        view.Find("input[data-test=extended]").Change(true);

        int extendedRows = view.Find("[data-test=distribution]").QuerySelectorAll("tbody tr").Length;
        Assert.True(extendedRows > realRows);
        Assert.Contains("Gardes", view.Find("[data-test=excess-explanation]").TextContent);
    }

    /// <summary>Une attaque Torrent contre une seule figurine à 1 PV sans sauvegarde : P(détruite) = P(blesser).</summary>
    private static SimulationResult OneShot(int strength, int toughness, bool twinLinked) => Simulator.Run(new Scenario
    {
        Attacker = new AttackerUnit
        {
            Weapons = [new WeaponGroup { Weapon = new WeaponProfile { Strength = strength, Abilities = new WeaponAbilities { Torrent = true, TwinLinked = twinLinked } } }],
        },
        Defender = new DefenderUnit { Groups = [new ModelGroup { Toughness = toughness }] },
    }, 20_000, seed: 1);

    [Theory]
    [InlineData(1, 10, false, "wh-tile-bad")]     // blessure sur 6+ : 17 %
    [InlineData(8, 4, false, "wh-tile-neutral")]  // blessure sur 2+ : 83 %
    [InlineData(8, 4, true, "wh-tile-good")]      // 2+ avec relance des échecs : 97 %
    public void Wipe_tile_is_red_below_50_percent_and_green_above_84_percent(int strength, int toughness, bool twinLinked, string tone)
    {
        var view = Render<ResultsView>(p => p.Add(c => c.Result, OneShot(strength, toughness, twinLinked)));

        Assert.Contains(tone, view.Find("[data-test=wipe]").ClassList);
    }

    [Fact]
    public void Numbers_use_the_french_format()
    {
        var view = Render<ResultsView>(p => p.Add(c => c.Result, Run(defenders: 2)));

        Assert.Matches(@"\d+,\d %", view.Find("[data-test=wipe]").TextContent);
    }
}
