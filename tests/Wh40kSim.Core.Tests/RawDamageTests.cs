using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Engine;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Tests;

/// <summary>
/// Dégâts bruts : dégâts de chaque attaque non sauvegardée comptés en entier (après réduction de dégâts et Feel No
/// Pain), sans plafond par figurine ni arrêt à la destruction de l'unité. Aucun dé supplémentaire n'est consommé.
/// </summary>
public class RawDamageTests
{
    private static WeaponProfile Torrent(int attacks, string damage, WeaponAbilities? extra = null) => new()
    {
        Attacks = attacks,
        Strength = 8,
        Damage = DiceExpression.Parse(damage),
        Abilities = (extra ?? new WeaponAbilities()) with { Torrent = true },
    };

    private static DefenderUnit Group(int count, int wounds, int? feelNoPain = null, int damageReduction = 0) => new()
    {
        Groups =
        [
            new ModelGroup
            {
                Name = "Cible", Count = count, Toughness = 4, Wounds = wounds, Save = 7,
                FeelNoPain = feelNoPain, DamageReduction = damageReduction,
            },
        ],
    };

    [Fact]
    public void Damage_beyond_the_wounds_of_the_model_is_counted_in_full()
    {
        var resolver = new AttackResolver(Build.Scenario(Torrent(2, "D3"), Group(count: 5, wounds: 1)));

        // 2 blessures, 2 sauvegardes ratées, dégâts 3 puis 2 : 2 PV retirés, 5 dégâts bruts
        var dice = new ScriptedDiceRoller(2, 2, 1, 1, 3, 2);
        resolver.Run(dice);

        Assert.Equal(2, resolver.Target.DamageDealt);
        Assert.Equal(5, resolver.Target.RawDamage);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Damage_reduction_is_applied_before_counting()
    {
        var resolver = new AttackResolver(Build.Scenario(Torrent(1, "3"), Group(count: 1, wounds: 10, damageReduction: 1)));

        resolver.Run(new ScriptedDiceRoller(2, 1));

        Assert.Equal(2, resolver.Target.DamageDealt);
        Assert.Equal(2, resolver.Target.RawDamage);
    }

    [Fact]
    public void Wounds_saved_by_feel_no_pain_are_not_counted()
    {
        var resolver = new AttackResolver(Build.Scenario(Torrent(1, "3"), Group(count: 3, wounds: 1, feelNoPain: 5)));

        // Blessure, sauvegarde ratée, FNP : 5 (sauvé), 2, 1 : 2 dégâts perdus, 1 seul PV retiré
        var dice = new ScriptedDiceRoller(2, 1, 5, 2, 1);
        resolver.Run(dice);

        Assert.Equal(1, resolver.Target.DamageDealt);
        Assert.Equal(2, resolver.Target.RawDamage);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Devastating_wounds_mortal_wounds_are_counted_in_full()
    {
        var weapon = Torrent(1, "D3", new WeaponAbilities { DevastatingWounds = true });
        var resolver = new AttackResolver(Build.Scenario(weapon, Group(count: 5, wounds: 1)));

        // Blessure critique (6) : pas de sauvegarde, 3 blessures mortelles sur une seule figurine
        var dice = new ScriptedDiceRoller(6, 3);
        resolver.Run(dice);

        Assert.Equal(1, resolver.Target.DamageDealt);
        Assert.Equal(3, resolver.Target.RawDamage);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Counting_continues_on_virtual_models_after_the_unit_is_destroyed()
    {
        var resolver = new AttackResolver(Build.Scenario(Torrent(3, "D3"), Group(count: 1, wounds: 1)));

        // Principaux : 3 blessures, 3 sauvegardes ratées, dégâts 2 (unité détruite)
        // Excédent : dégâts 3 puis 1 contre des figurines virtuelles
        var dice = new ScriptedDiceRoller(2, 2, 2, 1, 1, 1, 2);
        var excessDice = new ScriptedDiceRoller(3, 1);
        resolver.Run(dice, excessDice);

        Assert.Equal(1, resolver.Target.DamageDealt);
        Assert.Equal(2, resolver.Target.ExcessDamage);
        Assert.Equal(6, resolver.Target.RawDamage);
        Assert.Equal(0, dice.Remaining);
        Assert.Equal(0, excessDice.Remaining);
    }

    [Fact]
    public void Raw_damage_is_reset_between_runs()
    {
        var resolver = new AttackResolver(Build.Scenario(Torrent(1, "D3"), Group(count: 5, wounds: 1)));

        resolver.Run(new ScriptedDiceRoller(2, 1, 3));
        resolver.Run(new ScriptedDiceRoller(2, 1, 2));

        Assert.Equal(2, resolver.Target.RawDamage);
    }
}
