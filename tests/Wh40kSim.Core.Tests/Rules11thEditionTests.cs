using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Engine;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Tests;

/// <summary>Règles 05 (séquence d'attaque) et 02.02.01 (modificateurs) de la 11e édition.</summary>
public class Rules11thEditionTests
{
    private static WeaponProfile Torrent(string name, int strength = 8, int ap = 0) => new()
    {
        Name = name,
        Attacks = 1,
        Strength = strength,
        Ap = ap,
        Abilities = new WeaponAbilities { Torrent = true },
    };

    private static Scenario TwoWeapons(WeaponProfile first, WeaponProfile second, DefenderUnit defender) => new()
    {
        Attacker = new AttackerUnit
        {
            Weapons =
            [
                new WeaponGroup { Weapon = first },
                new WeaponGroup { Weapon = second },
            ],
        },
        Defender = defender,
    };

    private static DefenderUnit BodyguardAndLeader(int bodyguardSave = 4, int leaderSave = 2, int? leaderToughness = null) => new()
    {
        Toughness = 4,
        Groups =
        [
            new ModelGroup { Name = "Garde", Count = 1, Wounds = 1, Save = bodyguardSave },
            new ModelGroup { Name = "Leader", IsCharacter = true, Count = 1, Wounds = 1, Save = leaderSave, Toughness = leaderToughness },
        ],
    };

    [Fact]
    public void Weapons_making_identical_attacks_share_one_pool_and_their_saves_are_sorted_together()
    {
        // Comme bolter + pistolet bolter : noms et A différents, attaques identiques
        var bolter = Torrent("Bolter") with { Attacks = 2 };
        var resolver = new AttackResolver(TwoWeapons(bolter, Torrent("Pistolet"), BodyguardAndLeader()));

        Assert.Single(resolver.Pools);

        // 3 blessures (2+), puis 3 sauvegardes lancées ensemble : 3, 1, 6 → triées 1, 3, 6
        // 1 → garde mort ; 3 → leader Sv 2+ réussie ; 6 → réussie
        var dice = new ScriptedDiceRoller(2, 2, 2, 3, 1, 6);
        resolver.Run(dice);

        Assert.Equal(1, resolver.Target.ModelsKilled);
        Assert.Equal(1, resolver.Target.AliveInGroup(1));
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Weapons_with_different_attacks_are_resolved_as_separate_pools()
    {
        var resolver = new AttackResolver(TwoWeapons(Torrent("PA0"), Torrent("PA-1", ap: -1), BodyguardAndLeader()));
        Assert.Equal(2, resolver.Pools.Count);
    }

    [Fact]
    public void Toughness_switches_to_the_leader_once_the_bodyguards_are_destroyed()
    {
        // Gardes E4, leader E5 ; arme F5 : 3+ contre les gardes, 4+ contre le leader seul
        var defender = BodyguardAndLeader(bodyguardSave: 7, leaderSave: 7, leaderToughness: 5);
        var resolver = new AttackResolver(TwoWeapons(Torrent("A", strength: 5), Torrent("B", strength: 5, ap: -1), defender));

        // Pool A : blessure 3 (réussie contre E4), sauvegarde 1 → garde mort
        // Pool B : blessure 3 (ratée contre E5)
        var dice = new ScriptedDiceRoller(3, 1, 3);
        resolver.Run(dice);

        Assert.Equal(1, resolver.Target.ModelsKilled);
        Assert.Equal(1, resolver.Target.AliveInGroup(1));
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Attached_unit_uses_the_bodyguard_toughness_even_if_the_leader_is_tougher()
    {
        var state = new DefenderState(BodyguardAndLeader(leaderToughness: 6));
        Assert.Equal(4, state.EffectiveToughness());
    }

    [Fact]
    public void Random_attacks_and_damage_never_go_below_one()
    {
        var weapon = new WeaponProfile
        {
            Skill = 3,
            Strength = 4,
            Attacks = DiceExpression.Parse("D3-2"),
            Damage = DiceExpression.Parse("D3-2"),
        };
        var resolver = new AttackResolver(Build.Scenario(weapon, Build.Horde()));

        // Attaques : D3-2 = -1 → 1 ; touche 3 ; blessure 4 ; sauvegarde 1 ; dégâts D3-2 = -1 → 1
        var dice = new ScriptedDiceRoller(1, 3, 4, 1, 1);
        resolver.Run(dice);

        Assert.Equal(1, resolver.Target.ModelsKilled);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Character_group_cannot_be_allocated_before_a_bodyguard_group()
    {
        var defender = new DefenderUnit
        {
            Groups =
            [
                new ModelGroup { Name = "Leader", IsCharacter = true, Count = 1, Save = 2 },
                new ModelGroup { Name = "Garde", Count = 5, Save = 4 },
            ],
        };
        Assert.Throws<ArgumentException>(() => new AttackResolver(Build.Scenario(new WeaponProfile(), defender)));
    }

    [Fact]
    public void Character_group_holds_exactly_one_model()
    {
        var defender = new DefenderUnit { Groups = [new ModelGroup { Name = "Leaders", IsCharacter = true, Count = 2 }] };
        Assert.Throws<ArgumentException>(() => new AttackResolver(Build.Scenario(new WeaponProfile(), defender)));
    }

    [Fact]
    public void Non_character_models_with_same_wounds_and_saves_form_a_single_group()
    {
        var defender = new DefenderUnit
        {
            Groups =
            [
                new ModelGroup { Name = "A", Count = 3, Wounds = 2, Save = 3 },
                new ModelGroup { Name = "B", Count = 2, Wounds = 2, Save = 3 },
            ],
        };
        Assert.Throws<ArgumentException>(() => new AttackResolver(Build.Scenario(new WeaponProfile(), defender)));
    }

    // --- Precision (24.28) et Devastating Wounds (24.10) ----------------------------------

    private static WeaponProfile Precise(int attacks = 1, int strength = 8, bool devastating = false) => Torrent("Précise", strength) with
    {
        Attacks = attacks,
        Abilities = new WeaponAbilities { Torrent = true, Precision = true, DevastatingWounds = devastating },
    };

    [Fact]
    public void Precision_makes_the_character_the_current_allocation_group()
    {
        var resolver = new AttackResolver(Build.Scenario(Precise(), BodyguardAndLeader(bodyguardSave: 7, leaderSave: 7)));

        resolver.Run(new ScriptedDiceRoller(2, 1));

        Assert.Equal(0, resolver.Target.KilledInGroup(0));
        Assert.Equal(1, resolver.Target.KilledInGroup(1));
    }

    [Fact]
    public void Precision_falls_back_to_the_normal_order_once_the_character_is_destroyed()
    {
        var resolver = new AttackResolver(Build.Scenario(Precise(attacks: 2), BodyguardAndLeader(bodyguardSave: 7, leaderSave: 7)));

        // Sauvegardes 1, 1 : la première tue le leader, la seconde revient au garde
        resolver.Run(new ScriptedDiceRoller(2, 2, 1, 1));

        Assert.True(resolver.Target.IsDestroyed);
    }

    [Fact]
    public void Precision_can_target_a_named_character()
    {
        var defender = new DefenderUnit
        {
            Toughness = 4,
            Groups =
            [
                new ModelGroup { Name = "Garde", Count = 3, Wounds = 1, Save = 7 },
                new ModelGroup { Name = "Leader 1", IsCharacter = true, Count = 1, Wounds = 1, Save = 7 },
                new ModelGroup { Name = "Leader 2", IsCharacter = true, Count = 1, Wounds = 1, Save = 6 },
            ],
        };
        var context = new AttackContext { PrecisionTarget = "Leader 2" };
        var resolver = new AttackResolver(Build.Scenario(Precise(), defender, context: context));

        resolver.Run(new ScriptedDiceRoller(2, 1));

        Assert.Equal(1, resolver.Target.KilledInGroup(2));
        Assert.Equal(4, resolver.Target.ModelsAlive);
    }

    [Fact]
    public void Unknown_precision_target_is_rejected()
    {
        var context = new AttackContext { PrecisionTarget = "Personne" };
        Assert.Throws<ArgumentException>(() => new AttackResolver(Build.Scenario(Precise(), BodyguardAndLeader(), context: context)));
    }

    [Fact]
    public void Devastating_wounds_with_precision_hit_the_character_first()
    {
        // F4 contre E4 : le 6 est une blessure critique → blessure mortelle sur le leader, sans sauvegarde
        var resolver = new AttackResolver(Build.Scenario(Precise(strength: 4, devastating: true), BodyguardAndLeader(leaderSave: 2)));

        var dice = new ScriptedDiceRoller(6);
        resolver.Run(dice);

        Assert.Equal(1, resolver.Target.KilledInGroup(1));
        Assert.Equal(0, resolver.Target.KilledInGroup(0));
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Devastating_mortal_wounds_ignore_damage_reduction_but_not_feel_no_pain()
    {
        var defender = new DefenderUnit
        {
            Toughness = 4,
            Groups = [new ModelGroup { Count = 1, Wounds = 4, Save = 2, DamageReduction = 1, FeelNoPain = 5 }],
        };
        var weapon = Torrent("Dévastatrice", strength: 4) with
        {
            Damage = 3,
            Abilities = new WeaponAbilities { Torrent = true, DevastatingWounds = true },
        };
        var resolver = new AttackResolver(Build.Scenario(weapon, defender));

        // Blessure 6 → 3 blessures mortelles (pas de -1) ; FNP 5 (ignorée), 1, 2 → 2 PV perdus
        var dice = new ScriptedDiceRoller(6, 5, 1, 2);
        resolver.Run(dice);

        Assert.Equal(2, resolver.Target.DamageDealt);
        Assert.Equal(0, dice.Remaining);
    }
}
