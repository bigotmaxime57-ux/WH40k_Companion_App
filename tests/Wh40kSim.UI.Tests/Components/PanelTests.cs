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
    public void Duplicating_a_group_inserts_an_independent_copy_and_keeps_the_allocation_order()
    {
        var form = new DefenderForm(new DefenderUnit
        {
            Groups =
            [
                new ModelGroup { Name = "Gardes", Count = 5 },
                new ModelGroup { Name = "Leader", IsCharacter = true, Wounds = 4 },
            ],
        });
        var panel = Render<DefenderPanel>(p => p.Add(c => c.Defender, form));

        panel.ClickUntil("[data-test=duplicate-group]", () => form.Groups.Count == 3);
        Assert.Equal(["Gardes", "Gardes (copie)", "Leader"], form.Groups.Select(g => g.Name));
        form.Groups[1].Count = 2;
        Assert.Equal(5, form.Groups[0].Count);

        // Un PERSONNAGE copié reste après les gardes du corps
        panel.FindAll("[data-test=duplicate-group]")[2].Click();
        Assert.Equal(["Gardes", "Gardes (copie)", "Leader", "Leader (copie)"], form.Groups.Select(g => g.Name));
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

    [Fact]
    public void Overwatch_hides_the_conditions_it_makes_useless()
    {
        var weapons = new AttackerUnit
        {
            Weapons = [new WeaponGroup { Weapon = new WeaponProfile { Abilities = new WeaponAbilities { Heavy = true, Lance = true, RapidFire = 1 } } }],
        };
        var reference = new Scenario { Attacker = weapons, Context = new AttackContext { SnapShooting = true } };

        var panel = RenderPanel(reference, new ContextForm { SnapShooting = true });

        Assert.NotEmpty(panel.FindAll("[data-test=half-range]"));
        Assert.Empty(panel.FindAll("[data-test=remained-stationary]"));
        Assert.Empty(panel.FindAll("[data-test=charged]"));
    }

    [Fact]
    public void Overwatch_switch_is_always_offered_and_updates_the_situation()
    {
        var context = new ContextForm();
        int changes = 0;
        var panel = Render<ContextPanel>(p => p
            .Add(c => c.Context, context)
            .Add(c => c.Reference, new Scenario())
            .Add(c => c.Changed, () => changes++));

        panel.Find("input[data-test=snap-shooting]").Change(true);

        Assert.True(context.SnapShooting);
        Assert.True(context.ToAttackContext().SnapShooting);
        Assert.Equal(1, changes);
    }
}

public class BoostTests : ComponentTestContext
{
    [Fact]
    public void Attacker_boosts_start_collapsed_and_count_active_boosts()
    {
        var form = new AttackerBoostsForm();
        var boosts = Render<AttackerBoostsEditor>(p => p.Add(c => c.Boosts, form));
        Assert.Empty(boosts.FindAll("[data-test=boosts-content]"));
        Assert.Empty(boosts.FindAll("[data-test=boosts-count]"));

        boosts.Find("[data-test=toggle-boosts]").Click();
        boosts.Find("input[data-test=skill-modifier]").Change("2");

        Assert.Equal(2, form.Skill);
        Assert.Equal(0, form.HitRoll);
        Assert.Contains("1", boosts.Find("[data-test=boosts-count]").TextContent);
        // Les explications sont derrière la bulle « ? », pas sous les champs
        Assert.NotEmpty(boosts.FindAll("[data-test=help]"));
        Assert.Empty(boosts.FindAll(".mud-input-helper-text"));
    }

    [Fact]
    public void Defender_boosts_start_expanded_when_some_are_set()
    {
        var form = new DefenderBoostsForm(new DefenderBoosts { InCover = true, Damage = -1 });

        var boosts = Render<DefenderBoostsEditor>(p => p.Add(c => c.Boosts, form));

        Assert.NotNull(boosts.Find("[data-test=boosts-content]"));
        Assert.Contains("2", boosts.Find("[data-test=boosts-count]").TextContent);
        boosts.Find("input[data-test=defender-damage-modifier]").Change("-2");
        Assert.Equal(-2, form.Damage);
    }

    [Fact]
    public void Hit_boosts_are_greyed_in_overwatch_and_keep_their_values()
    {
        var attacker = new AttackerBoostsForm(new AttackerBoosts { Skill = 1, Strength = 1 });
        var defender = new DefenderBoostsForm(new DefenderBoosts { InCover = true, Ap = -1 });

        var attackerBoosts = Render<AttackerBoostsEditor>(p => p.Add(c => c.Boosts, attacker).Add(c => c.SnapShooting, true));
        var defenderBoosts = Render<DefenderBoostsEditor>(p => p.Add(c => c.Boosts, defender).Add(c => c.SnapShooting, true));

        Assert.True(attackerBoosts.Find("input[data-test=skill-modifier]").HasAttribute("disabled"));
        Assert.True(attackerBoosts.Find("input[data-test=hit-modifier]").HasAttribute("disabled"));
        Assert.True(defenderBoosts.Find("input[data-test=in-cover]").HasAttribute("disabled"));
        Assert.True(defenderBoosts.Find("input[data-test=defender-hit-modifier]").HasAttribute("disabled"));
        Assert.False(defenderBoosts.Find("input[data-test=defender-damage-modifier]").HasAttribute("disabled"));
        Assert.NotNull(attackerBoosts.Find("[data-test=boosts-snap-shooting]"));
        Assert.Equal(1, attacker.Skill);
        Assert.True(defender.InCover);
    }

    [Fact]
    public void Hit_boosts_are_editable_without_overwatch()
    {
        var boosts = Render<AttackerBoostsEditor>(p => p.Add(c => c.Boosts, new AttackerBoostsForm(new AttackerBoosts { Skill = 1 })));

        Assert.False(boosts.Find("input[data-test=skill-modifier]").HasAttribute("disabled"));
        Assert.Empty(boosts.FindAll("[data-test=boosts-snap-shooting]"));
    }

    [Fact]
    public void Attacker_card_shows_its_boosts_on_the_page()
    {
        var page = Render<SimulatorPage>();

        Assert.NotNull(page.Find("[data-test=attacker-boosts]"));
        Assert.NotNull(page.Find("[data-test=defender-boosts]"));
        // Titre « Armement » entre les boosts et les armes, comme « Groupes d'allocation » côté défenseur
        Assert.Contains("Armement", page.Find("[data-test=weapons-title]").TextContent);
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
        Assert.Empty(view.FindAll("[data-test=beyond]"));
        Assert.Empty(view.FindAll("[data-test=margin-kills]"));

        view.Find("input[data-test=extended]").Change(true);

        int extendedRows = view.Find("[data-test=distribution]").QuerySelectorAll("tbody tr").Length;
        Assert.True(extendedRows > realRows);
        Assert.Contains("Gardes", view.Find("[data-test=excess-explanation]").TextContent);
        // La moyenne des tuiles reste celle des pertes réelles ; la marge s'affiche dessous, « +x,xx (Marge) »
        Assert.Equal(result.Kills.Mean.ToString("F2"), view.Find("[data-test=tile-kills] .wh-tile-value").TextContent);
        Assert.Equal($"+{result.KillMarginWhenWiped:F2} (Marge)", view.Find("[data-test=tile-kills] [data-test=margin-kills]").TextContent.Trim());
        Assert.Equal($"+{result.DamageMarginWhenWiped:F2} (Marge)", view.Find("[data-test=tile-damage] [data-test=margin-damage]").TextContent.Trim());
        // Lignes au-delà de l'unité notées « 2 + n »
        Assert.StartsWith("2 + 1", view.Find("[data-test=beyond]").TextContent.Trim());
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
    public void Overwatch_results_are_labelled()
    {
        var scenario = new Scenario
        {
            Attacker = new AttackerUnit { Weapons = [new WeaponGroup { ModelCount = 10, Weapon = new WeaponProfile { Attacks = 2 } }] },
            Defender = new DefenderUnit { Groups = [new ModelGroup { Name = "Gardes", Count = 2 }] },
            Context = new AttackContext { SnapShooting = true },
        };

        var view = Render<ResultsView>(p => p.Add(c => c.Result, Simulator.Run(scenario, 2_000, seed: 1)));

        Assert.Contains("Overwatch", view.Find("[data-test=overwatch-chip]").TextContent);
        Assert.Empty(Render<ResultsView>(p => p.Add(c => c.Result, Run(defenders: 2))).FindAll("[data-test=overwatch-chip]"));
    }

    [Fact]
    public void Numbers_use_the_french_format()
    {
        var view = Render<ResultsView>(p => p.Add(c => c.Result, Run(defenders: 2)));

        Assert.Matches(@"\d+,\d %", view.Find("[data-test=wipe]").TextContent);
    }
}
