using Wh40kSim.Core.Engine;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Tests;

/// <summary>
/// Tests pas à pas avec des dés imposés. Ordre des jets dans le moteur :
/// attaques aléatoires → touches (+ relances) → blessures (+ relances) → toutes les sauvegardes
/// → pour chaque sauvegarde ratée (dans l'ordre croissant) : dégâts aléatoires puis FNP.
/// </summary>
public class AttackResolverTests
{
    // Arme Torrent à attaques fixes : aucun jet de touche, on ne scripte que blessures et sauvegardes
    private static WeaponProfile TorrentWeapon(int attacks, int strength = 8, int ap = 0, int damage = 1, WeaponAbilities? extra = null) => new()
    {
        Attacks = attacks,
        Strength = strength,
        Ap = ap,
        Damage = damage,
        Abilities = (extra ?? new WeaponAbilities()) with { Torrent = true },
    };

    [Fact]
    public void Saves_are_sorted_and_allocated_to_the_first_group_before_the_leader()
    {
        var defender = new DefenderUnit
        {
            Groups =
            [
                new ModelGroup { Name = "Gardes", Count = 3, Wounds = 1, Save = 4 },
                new ModelGroup { Name = "Leader", IsCharacter = true, Count = 1, Wounds = 3, Save = 2, InvulnerableSave = 4 },
            ],
        };
        var resolver = new AttackResolver(Build.Scenario(TorrentWeapon(4), defender));

        // Blessures : 4 réussites (F8 vs E4 = 2+). Sauvegardes : 1, 6, 6, 3 → triées 1, 3, 6, 6 pour les gardes (4+)
        var dice = new ScriptedDiceRoller(6, 6, 6, 6, 1, 6, 6, 3);
        resolver.Run(dice);

        Assert.Equal(2, resolver.Target.KilledInGroup(0));
        Assert.Equal(0, resolver.Target.KilledInGroup(1));
        Assert.Equal(2, resolver.Target.DamageDealt);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Remaining_save_rolls_flow_to_the_next_group_once_the_first_is_wiped()
    {
        var defender = new DefenderUnit
        {
            Groups =
            [
                new ModelGroup { Name = "Garde", Count = 1, Wounds = 1, Save = 4 },
                new ModelGroup { Name = "Leader", IsCharacter = true, Count = 1, Wounds = 2, Save = 3 },
            ],
        };
        var resolver = new AttackResolver(Build.Scenario(TorrentWeapon(4, ap: -1), defender));

        // Sauvegardes 5, 1, 3, 2 → triées 1, 2, 3, 5 :
        // 1 → garde mort ; 2 et 3 → leader (3+ PA-1 = 4+) raté deux fois → mort ; 5 → plus personne
        var dice = new ScriptedDiceRoller(2, 2, 2, 2, 5, 1, 3, 2);
        resolver.Run(dice);

        Assert.True(resolver.Target.IsDestroyed);
        Assert.Equal(3, resolver.Target.DamageDealt);
    }

    [Fact]
    public void Invulnerable_save_is_used_when_better_than_the_modified_armour_save()
    {
        var defender = new DefenderUnit
        {
            Groups =
            [
                new ModelGroup { Name = "Garde", Count = 1, Wounds = 1, Save = 3 },
                new ModelGroup { Name = "Leader", IsCharacter = true, Count = 1, Wounds = 1, Save = 2, InvulnerableSave = 4 },
            ],
        };
        var resolver = new AttackResolver(Build.Scenario(TorrentWeapon(2, ap: -3), defender));

        // Sauvegardes 4 puis 5 : 4 → garde (3+ PA-3 = 6+) raté ; 5 → leader (4++) réussi
        resolver.Run(new ScriptedDiceRoller(2, 2, 4, 5));

        Assert.Equal(1, resolver.Target.KilledInGroup(0));
        Assert.Equal(0, resolver.Target.KilledInGroup(1));
    }

    [Fact]
    public void Excess_damage_is_lost_and_does_not_spill_to_the_next_model()
    {
        var defender = new DefenderUnit { Groups = [new ModelGroup { Count = 3, Wounds = 1, Save = 7 }] };
        var resolver = new AttackResolver(Build.Scenario(TorrentWeapon(1, damage: 3), defender));

        resolver.Run(new ScriptedDiceRoller(2, 1));

        Assert.Equal(1, resolver.Target.ModelsKilled);
        Assert.Equal(1, resolver.Target.DamageDealt);
    }

    [Fact]
    public void Devastating_wounds_bypass_saves()
    {
        var defender = new DefenderUnit { Groups = [new ModelGroup { Count = 2, Wounds = 2, Save = 2 }] };
        var weapon = TorrentWeapon(2, strength: 4, damage: 2, extra: new WeaponAbilities { DevastatingWounds = true });
        var resolver = new AttackResolver(Build.Scenario(weapon, defender));

        // Blessures : 6 (critique → dévastatrice) et 5 (normale) ; sauvegarde de la normale : 6 réussie
        var dice = new ScriptedDiceRoller(6, 5, 6);
        resolver.Run(dice);

        Assert.Equal(1, resolver.Target.ModelsKilled);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Damage_reduction_then_feel_no_pain_are_applied()
    {
        var defender = new DefenderUnit
        {
            Groups = [new ModelGroup { Count = 1, Wounds = 4, Save = 7, FeelNoPain = 5, DamageReduction = 1 }],
        };
        var resolver = new AttackResolver(Build.Scenario(TorrentWeapon(1, damage: 3), defender));

        // Blessure 2, sauvegarde 1 ; dégâts 3 - 1 = 2 ; FNP 5 (ignoré) et 1 (subi)
        resolver.Run(new ScriptedDiceRoller(2, 1, 5, 1));

        Assert.Equal(1, resolver.Target.DamageDealt);
        Assert.Equal(0, resolver.Target.ModelsKilled);
    }

    [Fact]
    public void Lethal_hits_skip_the_wound_roll()
    {
        var weapon = new WeaponProfile { Skill = 3, Strength = 1, Abilities = new WeaponAbilities { LethalHits = true } };
        var resolver = new AttackResolver(Build.Scenario(weapon, Build.Horde(toughness: 10)));

        // Touche : 6 critique → blessure auto ; sauvegarde : 1
        var dice = new ScriptedDiceRoller(6, 1);
        resolver.Run(dice);

        Assert.Equal(1, resolver.Target.ModelsKilled);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Sustained_hits_generate_extra_hits()
    {
        var weapon = new WeaponProfile { Skill = 3, Strength = 4, Abilities = new WeaponAbilities { SustainedHits = 2 } };
        var resolver = new AttackResolver(Build.Scenario(weapon, Build.Horde()));

        // Touche 6 → 3 touches ; blessures 4, 4, 1 → 2 blessures ; sauvegardes (aucune) 2, 2
        resolver.Run(new ScriptedDiceRoller(6, 4, 4, 1, 2, 2));

        Assert.Equal(2, resolver.Target.ModelsKilled);
    }

    [Fact]
    public void Cover_worsens_ballistic_skill_by_one_in_11th_edition()
    {
        var weapon = new WeaponProfile { Skill = 3, Strength = 4 };
        var defender = Build.Horde() with { Boosts = new DefenderBoosts { InCover = true } };

        // CT 3+ à couvert = 4+ : un 3 rate
        var missed = new ScriptedDiceRoller(3);
        var resolver = new AttackResolver(Build.Scenario(weapon, defender));
        resolver.Run(missed);
        Assert.Equal(0, resolver.Target.ModelsKilled);
        Assert.Equal(0, missed.Remaining);

        // Ignores Cover : le 3 touche
        var ignoring = new AttackResolver(Build.Scenario(weapon with { Abilities = new WeaponAbilities { IgnoresCover = true } }, defender));
        ignoring.Run(new ScriptedDiceRoller(3, 4, 1));
        Assert.Equal(1, ignoring.Target.ModelsKilled);

        // Le couvert ne concerne pas la mêlée
        var melee = new AttackResolver(Build.Scenario(weapon with { Type = WeaponType.Melee }, defender));
        melee.Run(new ScriptedDiceRoller(3, 4, 1));
        Assert.Equal(1, melee.Target.ModelsKilled);
    }

    [Fact]
    public void Hit_modifiers_are_capped_at_plus_one()
    {
        var weapon = new WeaponProfile { Skill = 4, Strength = 4, Abilities = new WeaponAbilities { Heavy = true } };
        var context = new AttackContext { RemainedStationary = true };
        var resolver = new AttackResolver(Build.Scenario(weapon, Build.Horde(), context: context, boosts: new AttackerBoosts { HitRoll = 1 }));

        // 2 + 1 (plafonné) = 3 < 4 : raté
        resolver.Run(new ScriptedDiceRoller(2));
        Assert.Equal(0, resolver.Target.ModelsKilled);
    }

    [Fact]
    public void Reroll_ones_and_twin_linked_reroll_dice()
    {
        var weapon = new WeaponProfile { Skill = 3, Strength = 4, Abilities = new WeaponAbilities { TwinLinked = true } };
        var resolver = new AttackResolver(Build.Scenario(weapon, Build.Horde(), boosts: new AttackerBoosts { HitReroll = RerollPolicy.Ones }));

        // Touche 1 → relance 3 ; blessure 2 → relance (twin-linked) 4 ; sauvegarde 1
        var dice = new ScriptedDiceRoller(1, 3, 2, 4, 1);
        resolver.Run(dice);

        Assert.Equal(1, resolver.Target.ModelsKilled);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Blast_and_rapid_fire_add_attacks()
    {
        var weapon = new WeaponProfile
        {
            Skill = 3,
            Strength = 4,
            Abilities = new WeaponAbilities { Blast = true, RapidFire = 1 },
        };
        var context = new AttackContext { HalfRange = true };
        var resolver = new AttackResolver(Build.Scenario(weapon, Build.Horde(models: 10), context: context));

        // 1 attaque + 2 (Blast, 10 fig.) + 1 (Rapid Fire) = 4 attaques, toutes ratées
        var dice = new ScriptedDiceRoller(1, 1, 1, 1);
        resolver.Run(dice);

        Assert.Equal(0, dice.Remaining);
    }
}
