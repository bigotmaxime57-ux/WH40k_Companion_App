using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Engine;

/// <summary>
/// État de l'unité ciblée pendant une simulation : figurines restantes par groupe
/// et PV restants de la figurine « en tête » de chaque groupe (celle qui encaisse).
/// <para>Excédent : une fois l'unité détruite, le moteur peut continuer contre des figurines virtuelles du profil
/// <see cref="ExcessProfile"/>, en nombre illimité, regroupées dans le pseudo-groupe <see cref="ExcessGroup"/>.
/// Leurs pertes sont comptées à part (<see cref="ExcessModelsKilled"/>, <see cref="ExcessDamage"/>) et ne modifient
/// jamais l'état réel.</para>
/// </summary>
public sealed class DefenderState
{
    private readonly IReadOnlyList<ModelGroup> _groups;
    private readonly int[] _alive;
    private readonly int[] _frontWounds;
    private readonly int _damageModifier; // boost de dégâts du défenseur (DefenderUnit.DamageModifier)
    private int _excessFrontWounds;

    public DefenderState(DefenderUnit unit)
    {
        _groups = unit.Groups;
        _damageModifier = unit.DamageModifier;
        _alive = new int[_groups.Count];
        _frontWounds = new int[_groups.Count];
        TotalModels = unit.TotalModels;
        ExcessProfile = unit.ExcessProfile ?? new ModelGroup();
        Reset();
    }

    public int GroupCount => _groups.Count;

    public int TotalModels { get; }

    public int ModelsAlive { get; private set; }

    public int ModelsKilled => TotalModels - ModelsAlive;

    /// <summary>PV effectivement retirés (les dégâts excédentaires perdus ne comptent pas).</summary>
    public int DamageDealt { get; private set; }

    public bool IsDestroyed => ModelsAlive == 0;

    /// <summary>Indice du pseudo-groupe des figurines virtuelles de l'excédent (juste après les vrais groupes).</summary>
    public int ExcessGroup => _groups.Count;

    /// <summary>Profil des figurines virtuelles de l'excédent (voir <see cref="DefenderUnit.ExcessProfile"/>).</summary>
    public ModelGroup ExcessProfile { get; }

    /// <summary>Figurines virtuelles tuées après la destruction de l'unité.</summary>
    public int ExcessModelsKilled { get; private set; }

    /// <summary>PV retirés aux figurines virtuelles après la destruction de l'unité.</summary>
    public int ExcessDamage { get; private set; }

    public int AliveInGroup(int group) => _alive[group];

    public int KilledInGroup(int group) => _groups[group].Count - _alive[group];

    public int WoundsLeftOnFrontModel(int group) => _frontWounds[group];

    /// <summary>Profil d'un groupe, y compris le pseudo-groupe <see cref="ExcessGroup"/>.</summary>
    public ModelGroup Profile(int group) => group == ExcessGroup ? ExcessProfile : _groups[group];

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
    /// (non-PERSONNAGES) encore en jeu ; s'il n'y en a plus, la plus haute parmi les figurines restantes ;
    /// une fois l'unité détruite, celle des figurines virtuelles de l'excédent.
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
        return bodyguard > 0 ? bodyguard : any > 0 ? any : ExcessProfile.Toughness;
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
        _excessFrontWounds = ExcessProfile.Wounds;
        ExcessModelsKilled = 0;
        ExcessDamage = 0;
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
    /// à la figurine en tête du groupe : boost de dégâts du défenseur et réduction de dégâts du groupe (attaques
    /// uniquement, minimum 1), puis Feel No Pain point par point. Une seule figurine peut être affectée : l'excédent est
    /// perdu. Le groupe peut être <see cref="ExcessGroup"/>.
    /// </summary>
    internal void ApplyDamage(int group, int damage, bool mortalWounds, IDiceRoller dice)
    {
        bool excess = group == ExcessGroup;
        if (damage <= 0 || (!excess && _alive[group] == 0))
        {
            return;
        }

        var profile = Profile(group);
        if (!mortalWounds)
        {
            damage = Math.Max(1, damage + _damageModifier - profile.DamageReduction);
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

        if (excess)
        {
            int excessApplied = Math.Min(lost, _excessFrontWounds);
            _excessFrontWounds -= excessApplied;
            ExcessDamage += excessApplied;
            if (_excessFrontWounds == 0)
            {
                ExcessModelsKilled++;
                _excessFrontWounds = profile.Wounds;
            }
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
