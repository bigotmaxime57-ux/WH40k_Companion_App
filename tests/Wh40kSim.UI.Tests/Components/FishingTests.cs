using Bunit;
using Wh40kSim.Core.Model;
using Wh40kSim.UI.Components;
using Wh40kSim.UI.Forms;

namespace Wh40kSim.UI.Tests.Components;

/// <summary>Pêche aux critiques sur la carte d'une arme : interrupteurs affichés seulement quand ils ont un sens (Core).</summary>
public class FishingTests : ComponentTestContext
{
    private static readonly AttackerBoosts FullRerolls = new() { HitReroll = RerollPolicy.Failed, WoundReroll = RerollPolicy.Failed };

    private static WeaponForm Weapon(WeaponAbilities abilities) => new(new WeaponGroup { Weapon = new WeaponProfile { Abilities = abilities } });

    private IRenderedComponent<WeaponEditor> RenderEditor(WeaponForm weapon, AttackerBoosts boosts, AttackContext? context = null) =>
        Render<WeaponEditor>(p => p.Add(c => c.Weapon, weapon).Add(c => c.Boosts, boosts).Add(c => c.Context, context ?? new AttackContext()));

    [Fact]
    public void A_weapon_with_critical_effects_on_both_rolls_gets_two_independent_switches()
    {
        var weapon = Weapon(new WeaponAbilities { LethalHits = true, DevastatingWounds = true });
        var editor = RenderEditor(weapon, FullRerolls);

        editor.Find("input[data-test=fish-wounds]").Change(true);

        Assert.True(weapon.FishForCriticalWounds);
        Assert.False(weapon.FishForCriticalHits);
        Assert.True(weapon.ToWeaponGroup().Weapon.FishForCriticalWounds);

        editor.Find("input[data-test=fish-hits]").Change(true);
        Assert.True(weapon.FishForCriticalHits);
    }

    [Fact]
    public void Without_a_full_reroll_no_switch_is_offered()
    {
        var editor = RenderEditor(Weapon(new WeaponAbilities { LethalHits = true, DevastatingWounds = true }),
            new AttackerBoosts { HitReroll = RerollPolicy.Ones, WoundReroll = RerollPolicy.Ones });

        Assert.Empty(editor.FindAll("[data-test=weapon-fishing]"));
    }

    [Fact]
    public void Without_a_critical_effect_no_switch_is_offered()
    {
        var editor = RenderEditor(Weapon(new WeaponAbilities { CriticalHitOn = 5 }), FullRerolls);

        Assert.Empty(editor.FindAll("[data-test=weapon-fishing]"));
    }

    [Fact]
    public void Twin_linked_offers_fishing_for_devastating_wounds_without_boosts()
    {
        var editor = RenderEditor(Weapon(new WeaponAbilities { TwinLinked = true, DevastatingWounds = true, LethalHits = true }), new AttackerBoosts());

        Assert.NotEmpty(editor.FindAll("input[data-test=fish-wounds]"));
        Assert.Empty(editor.FindAll("input[data-test=fish-hits]"));
    }

    [Fact]
    public void No_hit_fishing_in_overwatch()
    {
        var editor = RenderEditor(Weapon(new WeaponAbilities { LethalHits = true }), FullRerolls, new AttackContext { SnapShooting = true });

        Assert.Empty(editor.FindAll("[data-test=weapon-fishing]"));
    }

    [Fact]
    public void Reroll_lists_offer_three_choices()
    {
        var boosts = Render<AttackerBoostsEditor>(p => p.Add(c => c.Boosts, new AttackerBoostsForm(new AttackerBoosts { HitReroll = RerollPolicy.Failed })));

        Assert.Contains("Relance complète", boosts.Markup);
        Assert.DoesNotContain("Tout sauf", boosts.Markup);
    }
}
