using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Engine;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;

namespace Wh40kSim.Core.Tests;

/// <summary>
/// Excédent : après la destruction de l'unité, la séquence continue contre des figurines virtuelles du premier groupe
/// non-PERSONNAGE, avec des dés séparés. Les dés principaux doivent être consommés exactement comme sans excédent.
/// </summary>
public class ExcessTests
{
    private static WeaponProfile Torrent(int attacks, int strength = 8, string damage = "1", WeaponAbilities? extra = null) => new()
    {
        Attacks = attacks,
        Strength = strength,
        Damage = DiceExpression.Parse(damage),
        Abilities = (extra ?? new WeaponAbilities()) with { Torrent = true },
    };

    private static DefenderUnit GuardAndLeader(int leaderWounds = 1, int leaderToughness = 4) => new()
    {
        Groups =
        [
            new ModelGroup { Name = "Garde", Count = 1, Toughness = 4, Wounds = 1, Save = 7 },
            new ModelGroup { Name = "Leader", IsCharacter = true, Count = 1, Toughness = leaderToughness, Wounds = leaderWounds, Save = 7 },
        ],
    };

    [Fact]
    public void Remaining_attacks_of_the_pool_continue_on_virtual_models_with_the_excess_dice()
    {
        var resolver = new AttackResolver(Build.Scenario(Torrent(4, damage: "D3"), GuardAndLeader(leaderWounds: 3)));

        // Principaux : 4 blessures, 4 sauvegardes ratées, dégâts 1 (garde mort) puis 3 (leader mort)
        var dice = new ScriptedDiceRoller(2, 2, 2, 2, 3, 3, 3, 3, 1, 3);
        // Excédent : dégâts 2 (1 seul PV retiré, l'excédent par figurine est perdu) puis 1
        var excessDice = new ScriptedDiceRoller(2, 1);
        resolver.Run(dice, excessDice);

        Assert.True(resolver.Target.IsDestroyed);
        Assert.Equal(4, resolver.Target.DamageDealt);
        Assert.Equal(2, resolver.Target.ExcessModelsKilled);
        Assert.Equal(2, resolver.Target.ExcessDamage);
        Assert.Equal(0, dice.Remaining);
        Assert.Equal(0, excessDice.Remaining);
    }

    [Fact]
    public void Without_excess_dice_the_remaining_attacks_are_lost_as_before()
    {
        var resolver = new AttackResolver(Build.Scenario(Torrent(4, damage: "D3"), GuardAndLeader(leaderWounds: 3)));

        // Mêmes dés principaux que le test précédent : même résultat réel, rien au-delà
        var dice = new ScriptedDiceRoller(2, 2, 2, 2, 3, 3, 3, 3, 1, 3);
        resolver.Run(dice);

        Assert.Equal(4, resolver.Target.DamageDealt);
        Assert.Equal(0, resolver.Target.ExcessModelsKilled);
        Assert.Equal(0, resolver.Target.ExcessDamage);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Later_pools_are_resolved_against_the_bodyguard_profile()
    {
        // Pool 1 (F8) détruit l'unité ; pool 2 (F4) contre le profil du garde (E4 : 4+), pas du leader (E8 : 6+)
        var scenario = new Scenario
        {
            Attacker = new AttackerUnit
            {
                Weapons =
                [
                    new WeaponGroup { Weapon = Torrent(2) },
                    new WeaponGroup { Weapon = Torrent(1, strength: 4) },
                ],
            },
            Defender = GuardAndLeader(leaderToughness: 8),
        };
        var resolver = new AttackResolver(scenario);

        var dice = new ScriptedDiceRoller(2, 2, 1, 1);
        // Excédent : blessure 4 (réussie contre E4), sauvegarde 1
        var excessDice = new ScriptedDiceRoller(4, 1);
        resolver.Run(dice, excessDice);

        Assert.Equal(2, resolver.Target.ModelsKilled);
        Assert.Equal(1, resolver.Target.ExcessModelsKilled);
        Assert.Equal(0, dice.Remaining);
        Assert.Equal(0, excessDice.Remaining);
    }

    [Fact]
    public void Precision_has_no_target_once_the_unit_is_destroyed()
    {
        var weapon = Torrent(3, extra: new WeaponAbilities { Precision = true });
        var resolver = new AttackResolver(Build.Scenario(weapon, GuardAndLeader()));

        // Precision : le leader d'abord, puis le garde, puis une figurine virtuelle
        resolver.Run(new ScriptedDiceRoller(2, 2, 2, 1, 1, 1), new ScriptedDiceRoller());

        Assert.Equal(1, resolver.Target.KilledInGroup(0));
        Assert.Equal(1, resolver.Target.KilledInGroup(1));
        Assert.Equal(1, resolver.Target.ExcessModelsKilled);
    }

    [Fact]
    public void Excess_counters_are_reset_between_runs()
    {
        var resolver = new AttackResolver(Build.Scenario(Torrent(3), GuardAndLeader()));

        resolver.Run(new ScriptedDiceRoller(2, 2, 2, 1, 1, 1), new ScriptedDiceRoller());
        resolver.Run(new ScriptedDiceRoller(2, 2, 2, 1, 1, 6), new ScriptedDiceRoller());

        Assert.Equal(2, resolver.Target.ModelsKilled);
        Assert.Equal(1, resolver.Target.ExcessModelsKilled);
    }

    [Fact]
    public void Excess_profile_is_the_first_bodyguard_group_not_the_leader()
    {
        var defender = new DefenderUnit
        {
            Groups =
            [
                new ModelGroup { Name = "Gardes", Count = 5, Wounds = 1, Save = 4 },
                new ModelGroup { Name = "Élite", Count = 2, Wounds = 2, Save = 3 },
                new ModelGroup { Name = "Leader", IsCharacter = true, Wounds = 5, Save = 2 },
            ],
        };

        Assert.Equal("Gardes", defender.ExcessProfile?.Name);
    }

    [Fact]
    public void Excess_profile_falls_back_to_the_first_group_without_bodyguards()
    {
        var lone = new DefenderUnit { Groups = [new ModelGroup { Name = "Personnage", IsCharacter = true, Wounds = 5 }] };

        Assert.Equal("Personnage", lone.ExcessProfile?.Name);
        Assert.Null(new DefenderUnit().ExcessProfile);
    }

    [Fact]
    public void Excess_profile_is_not_written_to_json()
    {
        string json = ScenarioJson.Serialize(new Scenario { Defender = GuardAndLeader() });

        Assert.DoesNotContain("excessProfile", json);
    }
}
