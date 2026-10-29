using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Engine;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Tests;

/// <summary>
/// Conversion des attaques par pool (rapport par arme) : attaques, touches, blessures, sauvegardes ratées, avec la part
/// de Blast, Sustained Hits, Lethal Hits et Devastating Wounds. Aucun dé supplémentaire n'est consommé.
/// </summary>
public class PoolConversionTests
{
    private static Scenario Scenario(int models, params WeaponProfile[] weapons) => new()
    {
        Attacker = new AttackerUnit { Weapons = weapons.Select(w => new WeaponGroup { Weapon = w }).ToList() },
        Defender = new DefenderUnit { Groups = [new ModelGroup { Name = "Cibles", Count = models, Wounds = 1, Save = 7 }] },
    };

    [Fact]
    public void Each_step_and_each_keyword_is_counted()
    {
        // 2 attaques + Blast contre 10 figurines (+2) ; touche sur 3+, blessure F4 contre E4 sur 4+, aucune sauvegarde
        var weapon = new WeaponProfile
        {
            Name = "Lance-grenades",
            Attacks = 2,
            Skill = 3,
            Strength = 4,
            Abilities = new WeaponAbilities { Blast = true, SustainedHits = 1, LethalHits = true, DevastatingWounds = true },
        };
        var resolver = new AttackResolver(Scenario(10, weapon));

        // Touches : 6 (critique : +1 Sustained, Lethal = blessure sans jet), 3, 1 (raté), 4 → 3 touches à jeter + 1 Lethal
        // Blessures : 6 (Devastating), 4, 2 (ratée) ; sauvegardes (blessure normale + Lethal) : 3, 5, toutes ratées
        var dice = new ScriptedDiceRoller(6, 3, 1, 4, 6, 4, 2, 3, 5);
        resolver.Run(dice);

        var c = resolver.ConversionOf(0);
        Assert.True(c.Fired);
        Assert.Equal((4, 2, 0), (c.Attacks, c.BlastAttacks, c.RapidFireAttacks));
        Assert.Equal((4, 1, 1), (c.Hits, c.SustainedHits, c.LethalHits));
        Assert.Equal((3, 1), (c.Wounds, c.DevastatingWounds));
        Assert.Equal(3, c.FailedSaves);   // 2 sauvegardes ratées + 1 Devastating sans sauvegarde
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void A_pool_that_fires_on_a_destroyed_unit_counts_nothing()
    {
        var canon = new WeaponProfile { Name = "Canon", Attacks = 1, Strength = 8, Damage = 3, Abilities = new WeaponAbilities { Torrent = true } };
        var rifle = new WeaponProfile { Name = "Fusil", Attacks = 3, Strength = 8, Abilities = new WeaponAbilities { Torrent = true } };
        var resolver = new AttackResolver(Scenario(1, canon, rifle));

        resolver.Run(new ScriptedDiceRoller(3, 2));

        Assert.True(resolver.ConversionOf(0).Fired);
        Assert.Equal((1, 1, 1, 1), (resolver.ConversionOf(0).Attacks, resolver.ConversionOf(0).Hits,
            resolver.ConversionOf(0).Wounds, resolver.ConversionOf(0).FailedSaves));
        Assert.False(resolver.ConversionOf(1).Fired);
        Assert.Equal(0, resolver.ConversionOf(1).Attacks);
    }
}
