using Wh40kSim.Core.Engine;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Tests;

/// <summary>
/// Relances par arme : la relance complète relance les échecs, ou tout sauf les critiques pour une arme en « pêche aux
/// 6 », seulement si l'arme a l'effet critique correspondant. Dés imposés, dans l'ordre : touche, (relance), blessure,
/// (relance), sauvegarde ; figurines à 1 PV sans sauvegarde, CT 3+, F4 contre E4. Un dé de trop ou de moins fait
/// échouer le test : il vérifie aussi quelles relances sont tirées.
/// </summary>
public class FishingTests
{
    private static readonly AttackerBoosts FullHitReroll = new() { HitReroll = RerollPolicy.Failed };

    private static WeaponProfile Weapon(WeaponAbilities abilities, bool fishHits = false, bool fishWounds = false, string name = "Arme") =>
        new() { Name = name, Skill = 3, Strength = 4, Abilities = abilities, FishForCriticalHits = fishHits, FishForCriticalWounds = fishWounds };

    private static Scenario Scenario(AttackerBoosts boosts, params WeaponProfile[] weapons) => new()
    {
        Attacker = new AttackerUnit { Boosts = boosts, Weapons = weapons.Select(w => new WeaponGroup { Weapon = w }).ToList() },
        Defender = Build.Horde(),
    };

    private static AttackResolver Run(Scenario scenario, params int[] rolls)
    {
        var resolver = new AttackResolver(scenario);
        var dice = new ScriptedDiceRoller(rolls);
        resolver.Run(dice);
        Assert.Equal(0, dice.Remaining);
        return resolver;
    }

    [Fact]
    public void Fishing_is_chosen_weapon_by_weapon()
    {
        var lethal = new WeaponAbilities { LethalHits = true };
        var scenario = Scenario(FullHitReroll, Weapon(lethal, fishHits: true, name: "Pêcheur"), Weapon(lethal, name: "Prudent"));

        // Pêcheur : 4 (touche ordinaire) relancé, 6 critique, blessure automatique, sauvegarde 1.
        // Prudent : 4 gardé (seuls les échecs sont relancés), blessure 4, sauvegarde 1.
        var resolver = Run(scenario, 4, 6, 1, 4, 4, 1);

        Assert.Equal(2, resolver.Target.ModelsKilled);
        Assert.Equal(2, resolver.Pools.Count);
    }

    [Fact]
    public void Fishing_still_rerolls_failures()
    {
        var scenario = Scenario(FullHitReroll, Weapon(new WeaponAbilities { LethalHits = true }, fishHits: true));

        // 1 relancé, 6 critique : blessure automatique, sauvegarde 1
        Assert.Equal(1, Run(scenario, 1, 6, 1).Target.ModelsKilled);
    }

    [Fact]
    public void Fishing_is_ignored_without_a_full_reroll()
    {
        var scenario = Scenario(new AttackerBoosts { HitReroll = RerollPolicy.Ones }, Weapon(new WeaponAbilities { LethalHits = true }, fishHits: true));

        // 4 gardé, blessure 4, sauvegarde 1
        Assert.Equal(1, Run(scenario, 4, 4, 1).Target.ModelsKilled);
    }

    [Fact]
    public void Fishing_is_ignored_without_a_critical_effect()
    {
        // Choix resté coché après le retrait de Lethal Hits : la relance complète ne relance que les échecs
        var scenario = Scenario(FullHitReroll, Weapon(new WeaponAbilities { CriticalHitOn = 5 }, fishHits: true));

        Assert.Equal(1, Run(scenario, 4, 4, 1).Target.ModelsKilled);
    }

    [Fact]
    public void Twin_linked_opens_fishing_for_devastating_wounds()
    {
        // Sans boost : Twin-linked suffit. Touche 3, blessure 4 (ordinaire) relancée, 6 critique : mortelle, pas de sauvegarde
        var weapon = Weapon(new WeaponAbilities { TwinLinked = true, DevastatingWounds = true }, fishWounds: true);

        Assert.Equal(1, Run(Scenario(new AttackerBoosts(), weapon), 3, 4, 6).Target.ModelsKilled);
    }

    [Fact]
    public void Without_devastating_wounds_twin_linked_only_rerolls_failures()
    {
        var weapon = Weapon(new WeaponAbilities { TwinLinked = true }, fishWounds: true);

        // Touche 3, blessure 4 gardée, sauvegarde 1
        Assert.Equal(1, Run(Scenario(new AttackerBoosts(), weapon), 3, 4, 1).Target.ModelsKilled);
    }

    [Fact]
    public void Fishing_choice_is_part_of_the_pool_identity()
    {
        var lethal = new WeaponAbilities { LethalHits = true };

        Assert.False(AttackPool.MakeIdenticalAttacks(Weapon(lethal, fishHits: true), Weapon(lethal)));
        Assert.False(AttackPool.MakeIdenticalAttacks(Weapon(lethal, fishWounds: true), Weapon(lethal)));
        Assert.True(AttackPool.MakeIdenticalAttacks(Weapon(lethal, fishHits: true), Weapon(lethal, fishHits: true)));
    }

    [Fact]
    public void Fishing_conditions_come_from_the_core()
    {
        var snap = new AttackContext { SnapShooting = true };
        var normal = new AttackContext();
        var fullWound = new AttackerBoosts { WoundReroll = RerollPolicy.Failed };

        Assert.True(Weapon(new WeaponAbilities { SustainedHits = 1 }).CanFishForCriticalHits(FullHitReroll, normal));
        Assert.True(Weapon(new WeaponAbilities { LethalHits = true }).CanFishForCriticalHits(FullHitReroll, normal));
        Assert.False(Weapon(new WeaponAbilities { LethalHits = true }).CanFishForCriticalHits(new AttackerBoosts { HitReroll = RerollPolicy.Ones }, normal));
        Assert.False(Weapon(new WeaponAbilities { LethalHits = true }).CanFishForCriticalHits(FullHitReroll, snap));
        Assert.False(Weapon(new WeaponAbilities { LethalHits = true, Torrent = true }).CanFishForCriticalHits(FullHitReroll, normal));
        Assert.False(Weapon(new WeaponAbilities { CriticalHitOn = 4 }).CanFishForCriticalHits(FullHitReroll, normal));

        Assert.True(Weapon(new WeaponAbilities { DevastatingWounds = true }).CanFishForCriticalWounds(fullWound));
        Assert.True(Weapon(new WeaponAbilities { DevastatingWounds = true, TwinLinked = true }).CanFishForCriticalWounds(new AttackerBoosts()));
        Assert.False(Weapon(new WeaponAbilities { DevastatingWounds = true }).CanFishForCriticalWounds(new AttackerBoosts { WoundReroll = RerollPolicy.Ones }));
        Assert.False(Weapon(new WeaponAbilities { LethalHits = true }).CanFishForCriticalWounds(fullWound));
    }
}
