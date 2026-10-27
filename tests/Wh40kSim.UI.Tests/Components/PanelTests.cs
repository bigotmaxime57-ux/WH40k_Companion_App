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

        panel.ClickUntil("[data-test=move-down]", () => form.Groups[0].Name == "B");

        Assert.Equal(["B", "A", "C"], form.Groups.Select(g => g.Name));
    }

    [Fact]
    public void Moving_up_swaps_a_group_with_the_previous_one()
    {
        var form = ThreeGroups();
        var panel = RenderPanel(form);

        // Le premier bouton « monter » actif est celui de B (A est en tête, C est un PERSONNAGE)
        panel.ClickUntil("[data-test=move-up]:not([disabled])", () => form.Groups[0].Name == "B");

        Assert.Equal(["B", "A", "C"], form.Groups.Select(g => g.Name));
    }

    [Fact]
    public void Moves_stay_in_the_list_and_never_put_a_character_before_a_bodyguard()
    {
        var panel = RenderPanel(ThreeGroups());
        var up = panel.FindAll("[data-test=move-up]");
        var down = panel.FindAll("[data-test=move-down]");

        Assert.True(up[0].HasAttribute("disabled"));    // A est déjà en tête
        Assert.False(up[1].HasAttribute("disabled"));   // B peut passer devant A
        Assert.True(up[2].HasAttribute("disabled"));    // C (PERSONNAGE) ne peut pas passer devant B
        Assert.True(down[1].HasAttribute("disabled"));  // B ne peut pas passer derrière C
        Assert.True(down[2].HasAttribute("disabled"));  // C est déjà en dernier
    }

    [Fact]
    public void Checking_character_sends_the_group_after_the_bodyguards()
    {
        var form = ThreeGroups();
        var panel = RenderPanel(form);

        panel.FindAll("input[data-test=is-character]")[0].Change(true);

        Assert.Equal(["B", "A", "C"], form.Groups.Select(g => g.Name));
    }

    [Fact]
    public void Unchecking_character_puts_the_group_back_before_the_characters()
    {
        var form = new DefenderForm(new DefenderUnit
        {
            Groups = [new ModelGroup { Name = "A", Count = 5 }, new ModelGroup { Name = "L1", IsCharacter = true }, new ModelGroup { Name = "L2", IsCharacter = true, Wounds = 3 }],
        });
        var panel = RenderPanel(form);

        panel.FindAll("input[data-test=is-character]")[2].Change(false);

        Assert.Equal(["A", "L2", "L1"], form.Groups.Select(g => g.Name));
    }

    [Fact]
    public void New_group_is_added_before_the_characters_with_the_last_bodyguard_toughness()
    {
        var form = ThreeGroups();
        var panel = RenderPanel(form);

        panel.Find("[data-test=add-group]").Click();

        Assert.Equal(4, form.Groups.Count);
        Assert.Equal("C", form.Groups[^1].Name);
        Assert.Equal(5, form.Groups[2].Toughness);
        Assert.Equal(new ModelGroup().Wounds, form.Groups[2].Wounds);
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

        // Rendu asynchrone de MudBlazor (infobulles de la carte) : clics via ClickUntil, voir ComponentTestContext
        panel.ClickUntil("[data-test=add-weapon]", () => form.Weapons.Count == 1);
        panel.ClickUntil("[data-test=add-weapon]", () => form.Weapons.Count == 2);
        panel.ClickUntil("[data-test=remove-weapon]", () => form.Weapons.Count == 1);

        Assert.Single(form.Weapons);
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
    public void Without_conditions_the_situation_says_so()
    {
        var panel = RenderPanel(new Scenario());

        Assert.NotNull(panel.Find("[data-test=context-none]"));
        Assert.Empty(panel.FindAll(".mud-input-helper-text"));
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

public class BoostTests : ComponentTestContext
{
    [Fact]
    public void Attacker_boosts_start_collapsed_and_count_active_boosts()
    {
        var context = new ContextForm();
        var boosts = Render<AttackerBoosts>(p => p.Add(c => c.Context, context));
        Assert.Empty(boosts.FindAll("[data-test=boosts-content]"));
        Assert.Empty(boosts.FindAll("[data-test=boosts-count]"));

        boosts.Find("[data-test=toggle-boosts]").Click();
        boosts.Find("input[data-test=skill-modifier]").Change("2");

        Assert.Equal(2, context.SkillModifier);
        Assert.Equal(0, context.HitModifier);
        Assert.Contains("1", boosts.Find("[data-test=boosts-count]").TextContent);
        // Les explications sont derrière la bulle « ? », pas sous les champs
        Assert.NotEmpty(boosts.FindAll("[data-test=help]"));
        Assert.Empty(boosts.FindAll(".mud-input-helper-text"));
    }

    [Fact]
    public void Defender_boosts_start_expanded_when_some_are_set()
    {
        var defender = new DefenderForm(new DefenderUnit { InCover = true, DamageModifier = -1 });

        var boosts = Render<DefenderBoosts>(p => p.Add(c => c.Defender, defender));

        Assert.NotNull(boosts.Find("[data-test=boosts-content]"));
        Assert.Contains("2", boosts.Find("[data-test=boosts-count]").TextContent);
        boosts.Find("input[data-test=defender-damage-modifier]").Change("-2");
        Assert.Equal(-2, defender.DamageModifier);
    }

    [Fact]
    public void Attacker_card_shows_its_boosts_on_the_page()
    {
        var page = Render<SimulatorPage>();

        Assert.NotNull(page.Find("[data-test=attacker-boosts]"));
        Assert.NotNull(page.Find("[data-test=defender-boosts]"));
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
