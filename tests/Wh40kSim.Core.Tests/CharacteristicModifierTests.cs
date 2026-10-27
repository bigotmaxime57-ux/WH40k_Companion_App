using Wh40kSim.Core.Engine;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;

namespace Wh40kSim.Core.Tests;

/// <summary>
/// Modifications de caractéristiques (02.02.01) : elles changent le profil avant les jets, sans plafond, chaque
/// caractéristique restant dans ses bornes ; à distinguer des modificateurs aux jets (plafonnés à ±1).
/// Dés imposés : touche, blessure, sauvegarde (une figurine à 1 PV, sans sauvegarde sauf mention).
/// </summary>
public class CharacteristicModifierTests
{
    private static readonly DefenderUnit OneModel = new() { Groups = [new ModelGroup { Count = 1, Toughness = 4, Wounds = 1, Save = 7 }] };

    private static WeaponProfile Weapon(int skill = 4, int strength = 8, WeaponType type = WeaponType.Ranged, int ap = 0, int damage = 1) =>
        new() { Type = type, Skill = skill, Strength = strength, Ap = ap, Damage = damage };

    private static DefenderState Run(WeaponProfile weapon, DefenderUnit defender, AttackerBoosts boosts, params int[] rolls)
    {
        var resolver = new AttackResolver(Build.Scenario(weapon, defender, boosts: boosts));
        var dice = new ScriptedDiceRoller(rolls);
        resolver.Run(dice);
        Assert.Equal(0, dice.Remaining);
        return resolver.Target;
    }

    // --- CT / CC ------------------------------------------------------------------------

    [Fact]
    public void Skill_improvement_lowers_the_required_roll()
    {
        // CT 4+ améliorée de 1 : un 3 touche (puis blessure 2, sauvegarde 1)
        var target = Run(Weapon(skill: 4), OneModel, new AttackerBoosts { Skill = 1 }, 3, 2, 1);

        Assert.Equal(1, target.ModelsKilled);
    }

    [Fact]
    public void Without_modification_the_same_roll_misses()
    {
        var target = Run(Weapon(skill: 4), OneModel, new AttackerBoosts(), 3);

        Assert.Equal(0, target.ModelsKilled);
    }

    [Theory]
    [InlineData(4, 0)] // CT 3+ dégradée deux fois (couvert + aptitude du défenseur saisie côté attaquant) : 5+, un 4 rate
    [InlineData(5, 1)]
    public void Skill_modifications_stack_without_cap_with_cover(int hitRoll, int killed)
    {
        var defender = OneModel with { Boosts = new DefenderBoosts { InCover = true } };
        int[] rolls = killed == 0 ? [hitRoll] : [hitRoll, 2, 1];

        var target = Run(Weapon(skill: 3), defender, new AttackerBoosts { Skill = -1 }, rolls);

        Assert.Equal(killed, target.ModelsKilled);
    }

    [Fact]
    public void Roll_modifiers_stay_capped_at_minus_one_unlike_skill_modifications()
    {
        // Deux modificateurs au jet de -1 : plafonnés à -1 ; CT 3+, un 4 donne 3 et touche
        var defender = OneModel with { Boosts = new DefenderBoosts { HitRoll = -1 } };
        var target = Run(Weapon(skill: 3), defender, new AttackerBoosts { HitRoll = -1 }, 4, 2, 1);

        Assert.Equal(1, target.ModelsKilled);
    }

    [Theory]
    [InlineData(5, 2, 1)]  // 3+ améliorée de 5 : bornée à 2+, un 2 touche
    [InlineData(-5, 5, 0)] // 3+ dégradée de 5 : bornée à 6+, un 5 rate
    public void Skill_stays_between_2_and_6(int modifier, int hitRoll, int killed)
    {
        int[] rolls = killed == 0 ? [hitRoll] : [hitRoll, 2, 1];

        var target = Run(Weapon(skill: 3), OneModel, new AttackerBoosts { Skill = modifier }, rolls);

        Assert.Equal(killed, target.ModelsKilled);
    }

    [Fact]
    public void Skill_modification_applies_to_melee_but_cover_does_not()
    {
        var melee = Weapon(skill: 3, type: WeaponType.Melee);

        // Couvert seul : CC 3+ inchangée, un 3 touche
        Assert.Equal(1, Run(melee, OneModel with { Boosts = new DefenderBoosts { InCover = true } }, new AttackerBoosts(), 3, 2, 1).ModelsKilled);
        // CC dégradée de 1 : 4+, un 3 rate
        Assert.Equal(0, Run(melee, OneModel, new AttackerBoosts { Skill = -1 }, 3).ModelsKilled);
    }

    // --- Attaques ------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 4)]  // A1 + 1, deux figurines : 4 attaques
    [InlineData(-3, 2)] // A1 - 3 : jamais moins de 1 attaque par figurine
    public void Attacks_modification_applies_per_model_and_never_drops_below_one(int modifier, int attacks)
    {
        var torrent = Weapon() with { Abilities = new WeaponAbilities { Torrent = true } };
        var horde = new DefenderUnit { Groups = [new ModelGroup { Count = 10, Wounds = 1, Save = 7 }] };
        var scenario = Build.Scenario(torrent, horde, models: 2, boosts: new AttackerBoosts { Attacks = modifier });
        var resolver = new AttackResolver(scenario);

        // Torrent : chaque attaque touche ; blessures toutes réussies (2), sauvegardes toutes ratées (1)
        var dice = new ScriptedDiceRoller([.. Enumerable.Repeat(2, attacks), .. Enumerable.Repeat(1, attacks)]);
        resolver.Run(dice);

        Assert.Equal(attacks, resolver.Target.ModelsKilled);
        Assert.Equal(0, dice.Remaining);
    }

    // --- Force ---------------------------------------------------------------------------

    [Fact]
    public void Strength_modification_changes_the_wound_roll()
    {
        // F4 contre E4 (4+) devient F8 (2+) : un 2 blesse
        Assert.Equal(1, Run(Weapon(skill: 2, strength: 4), OneModel, new AttackerBoosts { Strength = 4 }, 2, 2, 1).ModelsKilled);
    }

    [Fact]
    public void Strength_never_drops_below_one()
    {
        // F4 - 10 : F1 contre E4 (6+), un 5 ne blesse pas
        Assert.Equal(0, Run(Weapon(skill: 2, strength: 4), OneModel, new AttackerBoosts { Strength = -10 }, 2, 5).ModelsKilled);
    }

    // --- PA ------------------------------------------------------------------------------

    [Fact]
    public void Ap_improvement_makes_saves_harder()
    {
        // PA0 améliorée de 1 : PA-1 ; sauvegarde 3+, un 3 devient 2 et rate
        var armoured = OneModel with { Groups = [new ModelGroup { Count = 1, Wounds = 1, Save = 3 }] };

        Assert.Equal(1, Run(Weapon(skill: 2), armoured, new AttackerBoosts { Ap = 1 }, 2, 2, 3).ModelsKilled);
    }

    [Fact]
    public void Ap_worsening_makes_saves_easier()
    {
        // PA-1 dégradée de 1 (aptitude du défenseur, saisie côté attaquant) : PA0 ; un 3 sauve contre 3+
        var armoured = new DefenderUnit { Groups = [new ModelGroup { Count = 1, Wounds = 1, Save = 3 }] };

        Assert.Equal(0, Run(Weapon(skill: 2, ap: -1), armoured, new AttackerBoosts { Ap = -1 }, 2, 2, 3).ModelsKilled);
    }

    [Fact]
    public void Ap_never_goes_above_zero()
    {
        // PA0 dégradée de 1 : reste 0 (une PA+1 aurait fait sauver un 2 contre 3+)
        var armoured = new DefenderUnit { Groups = [new ModelGroup { Count = 1, Wounds = 1, Save = 3 }] };

        Assert.Equal(1, Run(Weapon(skill: 2), armoured, new AttackerBoosts { Ap = -1 }, 2, 2, 2).ModelsKilled);
    }

    // --- Dégâts --------------------------------------------------------------------------

    [Fact]
    public void Damage_modification_adds_damage_per_attack()
    {
        var twoWounds = new DefenderUnit { Groups = [new ModelGroup { Count = 1, Wounds = 2, Save = 7 }] };

        var target = Run(Weapon(skill: 2), twoWounds, new AttackerBoosts { Damage = 1 }, 2, 2, 1);

        Assert.Equal(1, target.ModelsKilled);
        Assert.Equal(2, target.DamageDealt);
    }

    [Fact]
    public void Damage_never_drops_below_one()
    {
        var twoWounds = new DefenderUnit { Groups = [new ModelGroup { Count = 1, Wounds = 2, Save = 7 }] };

        var target = Run(Weapon(skill: 2), twoWounds, new AttackerBoosts { Damage = -3 }, 2, 2, 1);

        Assert.Equal(1, target.DamageDealt);
    }

    // --- Boosts du défenseur (cumulés avec ceux de l'attaquant) ---------------------------

    [Theory]
    [InlineData(3, 0)] // CT 3+ : attaquant +1, défenseur -1, couvert -1 → 4+, un 3 rate
    [InlineData(4, 1)]
    public void Defender_skill_boost_stacks_with_the_attacker_and_cover(int hitRoll, int killed)
    {
        var defender = OneModel with { Boosts = new DefenderBoosts { InCover = true, Skill = -1 } };
        int[] rolls = killed == 0 ? [hitRoll] : [hitRoll, 2, 1];

        var target = Run(Weapon(skill: 3), defender, new AttackerBoosts { Skill = 1 }, rolls);

        Assert.Equal(killed, target.ModelsKilled);
    }

    [Fact]
    public void Defender_ap_boost_makes_saves_easier_and_never_pushes_ap_above_zero()
    {
        var armoured = new DefenderUnit { Boosts = new DefenderBoosts { Ap = -1 }, Groups = [new ModelGroup { Count = 1, Wounds = 1, Save = 3 }] };

        // PA-1 dégradée de 1 : PA0 ; un 3 sauve contre 3+
        Assert.Equal(0, Run(Weapon(skill: 2, ap: -1), armoured, new AttackerBoosts(), 2, 2, 3).ModelsKilled);
        // PA0 dégradée de 1 : reste 0 ; un 2 rate contre 3+
        Assert.Equal(1, Run(Weapon(skill: 2), armoured, new AttackerBoosts(), 2, 2, 2).ModelsKilled);
    }

    [Fact]
    public void Attacker_and_defender_damage_boosts_interact()
    {
        // D1, +2 dégâts (attaquant), -1 dégât (défenseur) : 2 dégâts, la figurine à 3 PV survit
        var defender = new DefenderUnit { Boosts = new DefenderBoosts { Damage = -1 }, Groups = [new ModelGroup { Count = 1, Wounds = 3, Save = 7 }] };

        var target = Run(Weapon(skill: 2), defender, new AttackerBoosts { Damage = 2 }, 2, 2, 1);

        Assert.Equal(2, target.DamageDealt);
        Assert.Equal(0, target.ModelsKilled);
    }

    [Fact]
    public void Defender_damage_boost_stacks_with_damage_reduction_and_never_drops_below_one()
    {
        var defender = new DefenderUnit { Boosts = new DefenderBoosts { Damage = -1 }, Groups = [new ModelGroup { Count = 1, Wounds = 5, Save = 7, DamageReduction = 1 }] };

        var target = Run(Weapon(skill: 2, damage: 3), defender, new AttackerBoosts(), 2, 2, 1);

        Assert.Equal(1, target.DamageDealt);
    }

    [Fact]
    public void Defender_damage_boost_does_not_apply_to_mortal_wounds()
    {
        // Devastating Wounds : la blessure critique (6) inflige 2 blessures mortelles, sans le -1 du défenseur
        var weapon = Weapon(skill: 2, damage: 2) with { Abilities = new WeaponAbilities { DevastatingWounds = true } };
        var defender = new DefenderUnit { Boosts = new DefenderBoosts { Damage = -1 }, Groups = [new ModelGroup { Count = 1, Wounds = 2, Save = 7 }] };

        Assert.Equal(1, Run(weapon, defender, new AttackerBoosts(), 2, 6).ModelsKilled);
    }

    // --- JSON ----------------------------------------------------------------------------

    [Fact]
    public void Boosts_default_to_neutral_and_round_trip_in_json()
    {
        var empty = ScenarioJson.Deserialize("""{ "attacker": {}, "defender": {} }""");
        Assert.Equal(new AttackerBoosts(), empty.Attacker.Boosts);
        Assert.Equal(new DefenderBoosts(), empty.Defender.Boosts);

        var attackerBoosts = new AttackerBoosts
        {
            Skill = 1, HitRoll = 1, HitReroll = RerollPolicy.Ones, Strength = 3, WoundRoll = -1, WoundReroll = RerollPolicy.Failed,
            Attacks = 2, Ap = -1, Damage = 1,
        };
        var defenderBoosts = new DefenderBoosts { InCover = true, Skill = -1, HitRoll = -1, WoundRoll = 1, Ap = -1, Damage = -1 };
        var scenario = new Scenario
        {
            Attacker = new AttackerUnit { Boosts = attackerBoosts },
            Defender = new DefenderUnit { Boosts = defenderBoosts },
        };

        var copy = ScenarioJson.Deserialize(ScenarioJson.Serialize(scenario));

        Assert.Equal(attackerBoosts, copy.Attacker.Boosts);
        Assert.Equal(defenderBoosts, copy.Defender.Boosts);
    }

    [Fact]
    public void Json_puts_boosts_with_their_unit_and_keeps_the_situation_apart()
    {
        string json = ScenarioJson.Serialize(new Scenario());

        Assert.Contains("\"boosts\"", json);
        // La situation ne contient plus que ses conditions ; un ancien champ y est refusé (JSON strict)
        Assert.Throws<System.Text.Json.JsonException>(() => ScenarioJson.Deserialize("""{ "context": { "hitReroll": "Ones" } }"""));
        Assert.Throws<System.Text.Json.JsonException>(() => ScenarioJson.Deserialize("""{ "defender": { "inCover": true } }"""));
    }
}
