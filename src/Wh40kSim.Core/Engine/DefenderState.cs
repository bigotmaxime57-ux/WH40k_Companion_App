using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Engine;

/// <summary>
/// État de l'unité ciblée pendant une simulation : figurines restantes par groupe
/// et PV restants de la figurine « en tête » de chaque groupe (celle qui encaisse).
/// </summary>
public sealed class DefenderState
{
    private readonly IReadOnlyList<ModelGroup> _groups;
    private readonly int[] _alive;
    private readonly int[] _frontWounds;

    public DefenderState(DefenderUnit unit)
    {
        _groups = unit.Groups;
        _alive = new int[_groups.Count];
        _frontWounds = new int[_groups.Count];
        TotalModels = unit.TotalModels;
        Reset();
    }

    public int GroupCount => _groups.Count;

    public int TotalModels { get; }

    public int ModelsAlive { get; private set; }

    public int ModelsKilled => TotalModels - ModelsAlive;

    /// <summary>PV effectivement retirés (les dégâts excédentaires perdus ne comptent pas).</summary>
    public int DamageDealt { get; private set; }

    public bool IsDestroyed => ModelsAlive == 0;

    public int AliveInGroup(int group) => _alive[group];

    public int KilledInGroup(int group) => _groups[group].Count - _alive[group];

    public int WoundsLeftOnFrontModel(int group) => _frontWounds[group];

    /// <summary>Premier groupe (dans l'ordre d'allocation) ayant encore des figurines, -1 si l'unité est détruite.</summary>
    public int FirstAliveGroup()
    {
        for (int g = 0; g < _alive.Length; g++)
        {
            if (_alive[g] > 0)
            {
                return g;
            }
        }
        return -1;
    }

    /// <summary>
    /// Endurance utilisée pour les jets de blessure (règle 05.02.01) : la plus haute parmi les gardes du corps
    /// (non-PERSONNAGES) encore en jeu ; s'il n'y en a plus, la plus haute parmi les figurines restantes.
    /// </summary>
    public int EffectiveToughness()
    {
        int bodyguard = 0, any = 0;
        for (int g = 0; g < _groups.Count; g++)
        {
            if (_alive[g] == 0)
            {
                continue;
            }
            any = Math.Max(any, _groups[g].Toughness);
            if (!_groups[g].IsCharacter)
            {
                bodyguard = Math.Max(bodyguard, _groups[g].Toughness);
            }
        }
        return bodyguard > 0 ? bodyguard : any;
    }

    internal void Reset()
    {
        for (int g = 0; g < _groups.Count; g++)
        {
            _alive[g] = _groups[g].Count;
            _frontWounds[g] = _groups[g].Wounds;
        }
        ModelsAlive = TotalModels;
        DamageDealt = 0;
    }

    /// <summary>
    /// Groupe d'allocation courant : le groupe imposé (Precision) tant qu'il a des figurines,
    /// sinon le premier groupe encore en jeu. -1 si l'unité est détruite.
    /// </summary>
    public int CurrentGroup(int forcedGroup)
    {
        return forcedGroup >= 0 && _alive[forcedGroup] > 0 ? forcedGroup : FirstAliveGroup();
    }

    /// <summary>
    /// Applique les dégâts d'UNE attaque (ou les blessures mortelles d'UNE blessure critique Devastating Wounds)
    /// à la figurine en tête du groupe : réduction de dégâts (attaques uniquement), puis Feel No Pain point par point.
    /// Une seule figurine peut être affectée : l'excédent est perdu.
    /// </summary>
    internal void ApplyDamage(int group, int damage, bool mortalWounds, IDiceRoller dice)
    {
        if (damage <= 0 || _alive[group] == 0)
        {
            return;
        }

        var profile = _groups[group];
        if (!mortalWounds)
        {
            damage = Math.Max(1, damage - profile.DamageReduction);
        }

        int lost = 0;
        for (int i = 0; i < damage; i++)
        {
            if (profile.FeelNoPain is int fnp && dice.D6() >= fnp)
            {
                continue;
            }
            lost++;
        }

        if (lost == 0)
        {
            return;
        }

        int applied = Math.Min(lost, _frontWounds[group]);
        _frontWounds[group] -= applied;
        DamageDealt += applied;

        if (_frontWounds[group] == 0)
        {
            _alive[group]--;
            ModelsAlive--;
            _frontWounds[group] = profile.Wounds;
        }
    }
}
