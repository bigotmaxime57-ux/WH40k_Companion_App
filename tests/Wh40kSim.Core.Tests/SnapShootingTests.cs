using System.Text.Json;
using System.Text.RegularExpressions;
using Wh40kSim.Core.Engine;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;

namespace Wh40kSim.Core.Tests;

/// <summary>
/// Snap shooting (15.09, utilisé par l'Overwatch 15.08) : seules les armes de tir tirent, chaque attaque ne touche que
/// sur un 6 non modifié, sans relance, et ces touches ne sont pas critiques ; Torrent touche toujours. Dés imposés :
/// touche, blessure, sauvegarde (une figurine à 1 PV, sans sauvegarde sauf mention). Un dé de trop ou de moins fait
/// échouer le test : il vérifie aussi qu'aucune relance ni touche bonus n'est tirée.
/// </summary>
public class SnapShootingTests
{
    private static readonly DefenderUnit OneModel = new() { Groups = [new ModelGroup { Count = 1, Toughness = 4, Wounds = 1, Save = 7 }] };

    private static readonly AttackContext Snap = new() { SnapShooting = true };

    private static WeaponProfile Weapon(WeaponAbilities? abilities = null, int skill = 2, WeaponType type = WeaponType.Ranged, string name = "Arme") =>
        new() { Name = name, Type = type, Skill = skill, Strength = 4, Abilities = abilities ?? new WeaponAbilities() };

    private static DefenderState Run(Scenario scenario, params int[] rolls)
    {
        var resolver = new AttackResolver(scenario);
        var dice = new ScriptedDiceRoller(rolls);
        resolver.Run(dice);
        Assert.Equal(0, dice.Remaining);
        return resolver.Target;
    }

    private static Scenario Mixed(AttackContext context) => new()
    {
        Attacker = new AttackerUnit
        {
            Weapons =
            [
                new WeaponGroup { Weapon = Weapon(name: "Épée", type: WeaponType.Melee) },
                new WeaponGroup { Weapon = Weapon(name: "Bolter") },
            ],
        },
        Defender = OneModel,
        Context = context,
    };

    // --- Jet de touche ------------------------------------------------------------------

    [Fact]
    public void A_five_misses_whatever_the_skill_and_boosts_and_is_not_rerolled()
    {
        // CT 2+, CT améliorée, +1 au jet, relance des échecs : un 5 rate quand même, sans relance
        var boosts = new AttackerBoosts { Skill = 3, HitRoll = 1, HitReroll = RerollPolicy.Failed };
        var defender = OneModel with { Boosts = new DefenderBoosts { HitRoll = 1, Skill = 1 } };

        var target = Run(Build.Scenario(Weapon(), defender, context: Snap, boosts: boosts), 5);

        Assert.Equal(0, target.ModelsKilled);
    }

    [Fact]
    public void A_six_hits_whatever_the_skill_and_cover()
    {
        // CT 6+ dégradée (couvert, -2) : le 6 touche quand même (blessure 4, sauvegarde 1)
        var defender = OneModel with { Boosts = new DefenderBoosts { InCover = true, Skill = -2 } };

        var target = Run(Build.Scenario(Weapon(skill: 6), defender, context: Snap), 6, 4, 1);

        Assert.Equal(1, target.ModelsKilled);
    }

    [Fact]
    public void A_six_is_not_a_critical_hit()
    {
        // Lethal Hits : le 6 n'est pas une blessure automatique (jet de blessure lancé) ; Sustained Hits : pas de touche
        // en plus (un dé de blessure seulement)
        var weapon = Weapon(new WeaponAbilities { LethalHits = true, SustainedHits = 2 });

        var target = Run(Build.Scenario(weapon, OneModel, context: Snap), 6, 3);

        Assert.Equal(0, target.ModelsKilled);
    }

    [Fact]
    public void Critical_hit_on_x_plus_does_not_make_lower_rolls_hit()
    {
        var weapon = Weapon(new WeaponAbilities { CriticalHitOn = 2, LethalHits = true });

        var target = Run(Build.Scenario(weapon, OneModel, context: Snap), 5);

        Assert.Equal(0, target.ModelsKilled);
    }

    [Fact]
    public void Torrent_still_hits_automatically()
    {
        // Aucun dé de touche : blessure 4, sauvegarde 1
        var weapon = Weapon(new WeaponAbilities { Torrent = true });

        var target = Run(Build.Scenario(weapon, OneModel, context: Snap), 4, 1);

        Assert.Equal(1, target.ModelsKilled);
    }

    // --- Suite de la séquence inchangée -------------------------------------------------

    [Fact]
    public void Devastating_wounds_still_trigger_on_a_critical_wound()
    {
        // Touche 6, blessure critique 6 : blessure mortelle, sans jet de sauvegarde (Sv 2+)
        var defender = new DefenderUnit { Groups = [new ModelGroup { Count = 1, Toughness = 4, Wounds = 1, Save = 2 }] };
        var weapon = Weapon(new WeaponAbilities { DevastatingWounds = true });

        var target = Run(Build.Scenario(weapon, defender, context: Snap), 6, 6);

        Assert.Equal(1, target.ModelsKilled);
    }

    [Fact]
    public void Wound_boosts_still_apply()
    {
        // F4 contre E4 (4+), +1 au jet de blessure : un 3 blesse (sauvegarde 1)
        var target = Run(Build.Scenario(Weapon(), OneModel, context: Snap, boosts: new AttackerBoosts { WoundRoll = 1 }), 6, 3, 1);

        Assert.Equal(1, target.ModelsKilled);
    }

    [Fact]
    public void Rapid_fire_at_half_range_still_adds_attacks()
    {
        var weapon = Weapon(new WeaponAbilities { RapidFire = 1 });
        var context = Snap with { HalfRange = true };

        // 2 attaques : 5 (rate) et 6, blessure 4, sauvegarde 1
        var target = Run(Build.Scenario(weapon, Build.Horde(), context: context), 5, 6, 4, 1);

        Assert.Equal(1, target.ModelsKilled);
    }

    [Fact]
    public void Lance_ignores_charging()
    {
        // « A chargé » saisi dans le JSON : sans effet en snap shooting, un 3 ne blesse pas (4+)
        var weapon = Weapon(new WeaponAbilities { Lance = true });
        var context = Snap with { Charged = true };

        var target = Run(Build.Scenario(weapon, OneModel, context: context), 6, 3);

        Assert.Equal(0, target.ModelsKilled);
    }

    // --- Armes de mêlée -----------------------------------------------------------------

    [Fact]
    public void Melee_weapons_do_not_fire()
    {
        var resolver = new AttackResolver(Mixed(Snap));

        // Seul le bolter tire : touche 6, blessure 4, sauvegarde 1
        var dice = new ScriptedDiceRoller(6, 4, 1);
        resolver.Run(dice);

        Assert.Equal(0, dice.Remaining);
        Assert.Equal(1, resolver.Target.ModelsKilled);
        Assert.Equal("Bolter", Assert.Single(resolver.Pools).Profile.Name);
    }

    [Fact]
    public void Melee_weapons_raise_a_warning_but_the_scenario_stays_valid()
    {
        var scenario = Mixed(Snap);

        Assert.Empty(scenario.Validate());
        string warning = Assert.Single(scenario.Warnings());
        Assert.Contains("'Épée'", warning);
        Assert.Empty(Mixed(new AttackContext()).Warnings());
    }

    [Fact]
    public void Without_any_ranged_weapon_the_scenario_cannot_be_simulated()
    {
        var scenario = Build.Scenario(Weapon(type: WeaponType.Melee), OneModel, context: Snap);

        Assert.Contains(scenario.Validate(), e => e.Contains("aucune arme de tir"));
        Assert.Empty(scenario.Warnings());
        Assert.Empty(Build.Scenario(Weapon(type: WeaponType.Melee), OneModel).Validate());
    }

    // --- Situation ----------------------------------------------------------------------

    [Fact]
    public void Only_half_range_still_matters()
    {
        var weapon = Weapon(new WeaponAbilities { Heavy = true, Lance = true, Melta = 2 });

        var normal = Build.Scenario(weapon, OneModel);
        var snap = Build.Scenario(weapon, OneModel, context: Snap);

        Assert.True(normal.RemainedStationaryMatters && normal.ChargedMatters && normal.HalfRangeMatters);
        Assert.False(snap.RemainedStationaryMatters);
        Assert.False(snap.ChargedMatters);
        Assert.True(snap.HalfRangeMatters);
    }

    [Fact]
    public void Situation_of_melee_weapons_does_not_matter_when_they_do_not_fire()
    {
        var scenario = new Scenario
        {
            Attacker = new AttackerUnit
            {
                Weapons =
                [
                    new WeaponGroup { Weapon = Weapon(new WeaponAbilities { Melta = 2, Precision = true }, type: WeaponType.Melee) },
                    new WeaponGroup { Weapon = Weapon() },
                ],
            },
            Defender = OneModel,
            Context = Snap,
        };

        Assert.False(scenario.HalfRangeMatters);
        Assert.False(scenario.UsesPrecision);
    }

    // --- JSON ---------------------------------------------------------------------------

    [Fact]
    public void Snap_shooting_round_trips_through_json_and_defaults_to_false()
    {
        var scenario = Build.Scenario(Weapon(), OneModel, context: Snap);

        Assert.True(ScenarioJson.Deserialize(ScenarioJson.Serialize(scenario)).Context.SnapShooting);
        Assert.Contains("\"snapShooting\": true", ScenarioJson.Serialize(scenario));

        string withoutField = Regex.Replace(ScenarioJson.Serialize(Build.Scenario(Weapon(), OneModel)), "\"snapShooting\": false,?", "");
        Assert.DoesNotContain("snapShooting", withoutField);
        Assert.False(ScenarioJson.Deserialize(withoutField).Context.SnapShooting);
    }

    [Fact]
    public void Misspelled_field_is_refused()
    {
        string json = ScenarioJson.Serialize(Build.Scenario(Weapon(), OneModel)).Replace("\"snapShooting\"", "\"overwatch\"");

        Assert.Throws<JsonException>(() => ScenarioJson.Deserialize(json));
    }
}
