using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Engine;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Tests;

/// <summary>
/// Pertes par pool d'attaques (rapport par arme) : PV retirés, figurines achevées et dégâts bruts de la séquence réelle,
/// unité déjà détruite quand le pool tire. Aucun dé supplémentaire n'est consommé.
/// </summary>
public class PoolAttributionTests
{
    // Torrent F8 contre E4 : blessure sur 3+ ; aucune sauvegarde (7+) ; dégâts fixes : aucun dé de dégâts
    private static WeaponProfile Weapon(string name, int attacks, int damage) => new()
    {
        Name = name,
        Attacks = attacks,
        Strength = 8,
        Damage = DiceExpression.Parse(damage.ToString()),
        Abilities = new WeaponAbilities { Torrent = true },
    };

    private static Scenario Scenario(int guards, params WeaponProfile[] weapons) => new()
    {
        Attacker = new AttackerUnit { Weapons = weapons.Select(w => new WeaponGroup { Weapon = w }).ToList() },
        Defender = new DefenderUnit { Groups = [new ModelGroup { Name = "Gardes", Count = guards, Wounds = 2, Save = 7 }] },
    };

    [Fact]
    public void Losses_raw_damage_and_kills_are_attributed_to_the_pool_that_dealt_them()
    {
        // Canon (D3 fixe) : 2 attaques → 2 gardes tués (2 PV chacun, 1 PV perdu à chaque fois) ;
        // fusil (D1) : 3 attaques → le dernier garde (2 PV), la 3e attaque n'a plus de cible
        var resolver = new AttackResolver(Scenario(3, Weapon("Canon", 2, damage: 3), Weapon("Fusil", 3, damage: 1)));

        var dice = new ScriptedDiceRoller(3, 3, 2, 2, 3, 3, 3, 2, 2, 2);
        resolver.Run(dice);

        var target = resolver.Target;
        Assert.Equal(2, target.PoolCount);
        Assert.Equal((4, 2, 6), (target.DamageByPool(0), target.KillsByPool(0), target.RawDamageByPool(0)));
        Assert.Equal((2, 1, 2), (target.DamageByPool(1), target.KillsByPool(1), target.RawDamageByPool(1)));
        Assert.Equal(target.DamageDealt, target.DamageByPool(0) + target.DamageByPool(1));
        Assert.Equal(target.ModelsKilled, target.KillsByPool(0) + target.KillsByPool(1));
        Assert.False(target.PoolFiredOnDestroyedUnit(0));
        Assert.False(target.PoolFiredOnDestroyedUnit(1));
        // Gaspillage : 1 PV perdu par attaque du canon, rien pour le fusil (D1)
        Assert.Equal((2, 0), (target.WasteByPool(0), target.WasteByPool(1)));
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void A_pool_that_starts_after_the_unit_is_destroyed_is_flagged_and_rolls_nothing()
    {
        // Le canon tue l'unique garde ; le fusil tire sur une unité déjà détruite
        var resolver = new AttackResolver(Scenario(1, Weapon("Canon", 1, damage: 3), Weapon("Fusil", 3, damage: 1)));

        var dice = new ScriptedDiceRoller(3, 2);
        resolver.Run(dice);

        Assert.False(resolver.Target.PoolFiredOnDestroyedUnit(0));
        Assert.True(resolver.Target.PoolFiredOnDestroyedUnit(1));
        Assert.Equal(0, resolver.Target.DamageByPool(1));
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Raw_damage_of_a_pool_stops_at_the_destruction_of_the_unit()
    {
        // 3 attaques à 3 dégâts contre un seul garde : la 1re le tue, les deux suivantes vont sur l'excédent
        var resolver = new AttackResolver(Scenario(1, Weapon("Canon", 3, damage: 3)));

        resolver.Run(new ScriptedDiceRoller(3, 3, 3, 2, 2, 2), new ScriptedDiceRoller());

        Assert.Equal(3, resolver.Target.RawDamageByPool(0));   // séquence réelle seulement
        Assert.Equal(9, resolver.Target.RawDamage);            // le total de l'étape 5 compte aussi l'excédent
        Assert.Equal(2, resolver.Target.DamageByPool(0));
        // Gaspillage du pool, excédent compris : 1 PV perdu sur le garde, puis 1 sur chaque figurine virtuelle à 2 PV
        Assert.Equal(3, resolver.Target.WasteByPool(0));
        Assert.Equal(resolver.Target.RawDamage - resolver.Target.DamageDealt - resolver.Target.ExcessDamage, resolver.Target.WasteByPool(0));
    }
}
