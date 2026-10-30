using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Engine;

/// <summary>
/// Ensemble d'armes faisant des attaques identiques (exemple des règles : 2 bolters A2 + 1 pistolet bolter A1
/// forment un seul pool de 5 dés d'attaque). Le nombre d'attaques peut différer ; le reste du profil est identique.
/// </summary>
public sealed class AttackPool
{
    private AttackPool(WeaponProfile profile)
    {
        Profile = profile;
    }

    /// <summary>Profil commun (celui de la première arme du pool) utilisé pour touche, blessure, sauvegarde et dégâts.</summary>
    public WeaponProfile Profile { get; }

    public List<WeaponGroup> Members { get; } = [];

    /// <summary>Regroupe les armes aux attaques identiques, dans l'ordre de première apparition.</summary>
    public static IReadOnlyList<AttackPool> Build(IEnumerable<WeaponGroup> weapons)
    {
        var pools = new List<AttackPool>();
        foreach (var group in weapons)
        {
            var pool = pools.FirstOrDefault(p => MakeIdenticalAttacks(p.Profile, group.Weapon));
            if (pool is null)
            {
                pool = new AttackPool(group.Weapon);
                pools.Add(pool);
            }
            pool.Members.Add(group);
        }
        return pools;
    }

    /// <summary>
    /// Deux armes font des attaques identiques si tout ce qui sert après le décompte des attaques est égal, choix de
    /// « pêche aux critiques » compris (une seule stratégie de relance par groupe de dés). Le nom, la caractéristique A et les
    /// règles qui n'agissent que sur le nombre d'attaques (Blast, Rapid Fire) sont ignorés.
    /// </summary>
    public static bool MakeIdenticalAttacks(WeaponProfile a, WeaponProfile b) =>
        a.Type == b.Type
        && (a.Abilities.Torrent || a.Skill == b.Skill)
        && a.Strength == b.Strength
        && Math.Abs(a.Ap) == Math.Abs(b.Ap)
        && a.Damage == b.Damage
        && a.FishForCriticalHits == b.FishForCriticalHits
        && a.FishForCriticalWounds == b.FishForCriticalWounds
        && AttackAbilities(a) == AttackAbilities(b);

    private static WeaponAbilities AttackAbilities(WeaponProfile weapon) =>
        weapon.Abilities with { Blast = false, RapidFire = 0 };
}
