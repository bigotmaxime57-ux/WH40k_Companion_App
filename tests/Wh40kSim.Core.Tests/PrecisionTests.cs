using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Engine;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Tests;

/// <summary>
/// Pertes « grâce à Precision » : attaque allouée au groupe visé par Precision alors que, sans Precision, elle serait
/// allée sur un autre groupe (garde du corps en vie, ou PERSONNAGE placé devant) ; figurine attribuée à l'attaque qui
/// l'achève. Aucun dé supplémentaire n'est consommé.
/// </summary>
public class PrecisionTests
{
    // Torrent F8 contre E4 : blessure sur 3+ ; aucune sauvegarde (7+) ; dégâts fixes : aucun dé de dégâts
    private static WeaponProfile Weapon(int attacks, int damage, bool precision = true, bool devastating = false) => new()
    {
        Attacks = attacks,
        Strength = 8,
        Damage = DiceExpression.Parse(damage.ToString()),
        Abilities = new WeaponAbilities { Torrent = true, Precision = precision, DevastatingWounds = devastating },
    };

    private static ModelGroup Guards(int count = 5) => new() { Name = "Garde", Count = count, Wounds = 1, Save = 7 };

    private static ModelGroup Character(string name, int wounds = 3, int save = 7) =>
        new() { Name = name, IsCharacter = true, Count = 1, Wounds = wounds, Save = save };

    [Fact]
    public void Losses_on_the_character_while_bodyguards_live_are_due_to_precision()
    {
        var defender = new DefenderUnit { Groups = [Guards(), Character("Leader")] };
        var resolver = new AttackResolver(Build.Scenario(Weapon(2, damage: 2), defender));

        // 2 blessures, 2 sauvegardes ratées : 2 PV puis 1 PV (le leader meurt), les gardes sont intacts
        var dice = new ScriptedDiceRoller(3, 3, 2, 2);
        resolver.Run(dice);

        Assert.Equal(1, resolver.Target.KilledInGroup(1));
        Assert.Equal(1, resolver.Target.PrecisionKilledInGroup(1));
        Assert.Equal(3, resolver.Target.PrecisionDamageInGroup(1));
        Assert.Equal(0, resolver.Target.KilledInGroup(0));
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Once_the_target_is_dead_the_rest_of_the_pool_is_not_due_to_precision()
    {
        var defender = new DefenderUnit { Groups = [Guards(), Character("Leader")] };
        var resolver = new AttackResolver(Build.Scenario(Weapon(3, damage: 3), defender));

        // La première attaque tue le leader (Precision), les deux suivantes vont sur les gardes (ordre normal)
        resolver.Run(new ScriptedDiceRoller(3, 3, 3, 2, 2, 2));

        Assert.Equal(1, resolver.Target.PrecisionKilledInGroup(1));
        Assert.Equal(2, resolver.Target.KilledInGroup(0));
        Assert.Equal(0, resolver.Target.PrecisionKilledInGroup(0));
        Assert.Equal(0, resolver.Target.PrecisionDamageInGroup(0));
    }

    [Fact]
    public void Without_bodyguards_the_character_would_have_been_hit_anyway()
    {
        var defender = new DefenderUnit { Groups = [Character("Leader")] };
        var resolver = new AttackResolver(Build.Scenario(Weapon(2, damage: 2), defender));

        resolver.Run(new ScriptedDiceRoller(3, 3, 2, 2));

        Assert.Equal(1, resolver.Target.KilledInGroup(0));
        Assert.Equal(0, resolver.Target.PrecisionKilledInGroup(0));
        Assert.Equal(0, resolver.Target.PrecisionDamageInGroup(0));
    }

    [Fact]
    public void Reaching_a_chosen_character_behind_another_one_is_due_to_precision()
    {
        var defender = new DefenderUnit { Groups = [Character("Leader"), Character("Soutien")] };
        var context = new AttackContext { PrecisionTarget = "Soutien" };
        var resolver = new AttackResolver(Build.Scenario(Weapon(1, damage: 3), defender, context: context));

        resolver.Run(new ScriptedDiceRoller(3, 2));

        Assert.Equal(1, resolver.Target.KilledInGroup(1));
        Assert.Equal(1, resolver.Target.PrecisionKilledInGroup(1));
        Assert.Equal(0, resolver.Target.KilledInGroup(0));
    }

    [Fact]
    public void Model_is_credited_to_precision_only_when_a_precision_attack_finishes_it()
    {
        // Leader 4 PV : Precision lui retire 2 PV (garde en vie), puis le garde meurt, puis le leader est achevé
        // par la seconde arme, sans Precision : 2 PV grâce à Precision, mais la figurine ne lui est pas attribuée
        var defender = new DefenderUnit { Groups = [Guards(count: 1), Character("Leader", wounds: 4)] };
        var scenario = new Scenario
        {
            Attacker = new AttackerUnit
            {
                Weapons =
                [
                    new WeaponGroup { ModelCount = 1, Weapon = Weapon(1, damage: 2) with { Name = "Précise" } },
                    new WeaponGroup { ModelCount = 1, Weapon = Weapon(3, damage: 2, precision: false) with { Name = "Normale" } },
                ],
            },
            Defender = defender,
        };
        var resolver = new AttackResolver(scenario);

        // Pool Precision : 1 blessure, 1 sauvegarde ratée ; pool normal : 3 blessures, 3 sauvegardes ratées
        // (la première tue le garde, les deux suivantes achèvent le leader)
        resolver.Run(new ScriptedDiceRoller(3, 2, 3, 3, 3, 2, 2, 2));

        Assert.True(resolver.Target.IsDestroyed);
        Assert.Equal(2, resolver.Target.PrecisionDamageInGroup(1));
        Assert.Equal(0, resolver.Target.PrecisionKilledInGroup(1));
    }

    [Fact]
    public void Devastating_wounds_routed_by_precision_are_counted()
    {
        var defender = new DefenderUnit { Groups = [Guards(), Character("Leader")] };
        var resolver = new AttackResolver(Build.Scenario(Weapon(1, damage: 3, devastating: true), defender));

        // Blessure critique (6) : 3 blessures mortelles sur le leader
        resolver.Run(new ScriptedDiceRoller(6));

        Assert.Equal(1, resolver.Target.PrecisionKilledInGroup(1));
        Assert.Equal(3, resolver.Target.PrecisionDamageInGroup(1));
    }

    [Fact]
    public void Weapons_without_precision_never_count()
    {
        var defender = new DefenderUnit { Groups = [Guards(count: 1), Character("Leader")] };
        var resolver = new AttackResolver(Build.Scenario(Weapon(3, damage: 3, precision: false), defender));

        resolver.Run(new ScriptedDiceRoller(3, 3, 3, 2, 2, 2));

        Assert.Equal(1, resolver.Target.KilledInGroup(1));
        Assert.Equal(0, resolver.Target.PrecisionKilledInGroup(1));
        Assert.Equal(0, resolver.Target.PrecisionDamageInGroup(1));
    }

    [Fact]
    public void Counters_are_reset_between_simulations()
    {
        var defender = new DefenderUnit { Groups = [Guards(), Character("Leader")] };
        var resolver = new AttackResolver(Build.Scenario(Weapon(1, damage: 3), defender));

        resolver.Run(new ScriptedDiceRoller(3, 2));
        resolver.Run(new ScriptedDiceRoller(1));

        Assert.Equal(0, resolver.Target.PrecisionKilledInGroup(1));
        Assert.Equal(0, resolver.Target.PrecisionDamageInGroup(1));
    }

    [Fact]
    public void Precision_targets_are_the_chosen_character_or_every_character()
    {
        var defender = new DefenderUnit { Groups = [Guards(), Character("Leader"), Character("Soutien")] };
        var precise = Build.Scenario(Weapon(1, damage: 1), defender);

        Assert.Equal([1, 2], precise.PrecisionTargets);
        Assert.Equal([2], (precise with { Context = new AttackContext { PrecisionTarget = "Soutien" } }).PrecisionTargets);
        Assert.Empty(Build.Scenario(Weapon(1, damage: 1, precision: false), defender).PrecisionTargets);
    }
}
