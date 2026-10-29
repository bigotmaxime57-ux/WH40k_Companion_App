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
    private readonly int[] _precisionKilled; // figurines achevées par une attaque « grâce à Precision », par groupe
    private readonly int[] _precisionDamage; // PV retirés par ces attaques, par groupe
    private readonly int[] _precisionWeaponDamage; // PV retirés par des armes Precision (quelle que soit l'allocation), par groupe
    private readonly int _damageModifier; // boost de dégâts du défenseur (DefenderBoosts.Damage)
    private int _excessFrontWounds;

    // Par pool d'attaques (voir TrackPools), séquence réelle seulement : PV retirés, figurines achevées, dégâts bruts,
    // et unité déjà détruite quand le pool commence
    private int[] _poolDamage = [];
    private int[] _poolKills = [];
    private int[] _poolRawDamage = [];
    private int[] _poolWaste = [];
    private bool[] _poolOnDestroyedUnit = [];
    private int _currentPool = -1;

    public DefenderState(DefenderUnit unit)
    {
        _groups = unit.Groups;
        _damageModifier = unit.Boosts.Damage;
        _alive = new int[_groups.Count];
        _frontWounds = new int[_groups.Count];
        _precisionKilled = new int[_groups.Count];
        _precisionDamage = new int[_groups.Count];
        _precisionWeaponDamage = new int[_groups.Count];
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

    /// <summary>
    /// Dégâts bruts : ce que chaque attaque non sauvegardée (ou chaque salve de blessures mortelles) aurait retiré
    /// sans plafond par figurine, après boost de dégâts du défenseur, réduction de dégâts et Feel No Pain. Compte
    /// aussi les attaques contre les figurines virtuelles de l'excédent : la mesure ne s'arrête à la destruction de
    /// l'unité que si l'excédent n'est pas suivi (résolution sans dés d'excédent).
    /// </summary>
    public int RawDamage { get; private set; }

    public int AliveInGroup(int group) => _alive[group];

    public int KilledInGroup(int group) => _groups[group].Count - _alive[group];

    /// <summary>
    /// PV retirés au groupe : figurines tuées, plus ceux de la figurine en tête si elle est entamée (après une
    /// figurine tuée, la suivante repart avec tous ses PV, d'où un terme nul). La somme sur les groupes vaut
    /// <see cref="DamageDealt"/>.
    /// </summary>
    public int DamageInGroup(int group) =>
        KilledInGroup(group) * _groups[group].Wounds + (_groups[group].Wounds - _frontWounds[group]);

    /// <summary>
    /// Figurines du groupe achevées par une attaque « grâce à Precision » : allouée au groupe visé par Precision alors
    /// que, sans Precision, elle serait allée sur un autre groupe (garde du corps en vie, ou PERSONNAGE placé devant).
    /// </summary>
    public int PrecisionKilledInGroup(int group) => _precisionKilled[group];

    /// <summary>PV retirés au groupe par les attaques « grâce à Precision » (voir <see cref="PrecisionKilledInGroup"/>).</summary>
    public int PrecisionDamageInGroup(int group) => _precisionDamage[group];

    /// <summary>
    /// PV retirés au groupe par des armes Precision (pools dont le profil a le mot-clé), où que l'attaque ait été
    /// allouée ; le reste de <see cref="DamageInGroup"/> vient des autres armes.
    /// </summary>
    public int PrecisionWeaponDamageInGroup(int group) => _precisionWeaponDamage[group];

    public int WoundsLeftOnFrontModel(int group) => _frontWounds[group];

    /// <summary>Nombre de pools d'attaques suivis (ceux du résolveur), 0 si l'état est utilisé sans résolveur.</summary>
    public int PoolCount => _poolDamage.Length;

    /// <summary>PV retirés à l'unité (séquence réelle) par les attaques du pool, dans l'ordre de résolution.</summary>
    public int DamageByPool(int pool) => _poolDamage[pool];

    /// <summary>Figurines achevées par une attaque du pool (une figurine compte pour l'attaque qui l'achève).</summary>
    public int KillsByPool(int pool) => _poolKills[pool];

    /// <summary>
    /// Dégâts bruts du pool (voir <see cref="RawDamage"/>), jusqu'à la destruction de l'unité seulement : ce qui suit
    /// (excédent) n'est pas compté ici. Bruts − PV retirés = ce que le pool perd par figurine.
    /// </summary>
    public int RawDamageByPool(int pool) => _poolRawDamage[pool];

    /// <summary>L'unité était déjà détruite quand le pool a commencé à tirer : ses attaques ne pouvaient plus rien.</summary>
    public bool PoolFiredOnDestroyedUnit(int pool) => _poolOnDestroyedUnit[pool];

    /// <summary>
    /// PV perdus par les attaques du pool parce qu'elles dépassent les PV restants de leur figurine, y compris contre les
    /// figurines virtuelles de l'excédent : la somme sur les pools est le gaspillage de la séquence (<see cref="RawDamage"/>
    /// − <see cref="DamageDealt"/> − <see cref="ExcessDamage"/>).
    /// </summary>
    public int WasteByPool(int pool) => _poolWaste[pool];

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
            _precisionKilled[g] = 0;
            _precisionDamage[g] = 0;
            _precisionWeaponDamage[g] = 0;
        }
        Array.Clear(_poolDamage);
        Array.Clear(_poolKills);
        Array.Clear(_poolRawDamage);
        Array.Clear(_poolWaste);
        Array.Clear(_poolOnDestroyedUnit);
        _currentPool = -1;
        ModelsAlive = TotalModels;
        DamageDealt = 0;
        _excessFrontWounds = ExcessProfile.Wounds;
        ExcessModelsKilled = 0;
        ExcessDamage = 0;
        RawDamage = 0;
    }

    /// <summary>Prépare le suivi par pool (appelé une fois par le résolveur, qui connaît ses pools).</summary>
    internal void TrackPools(int count)
    {
        _poolDamage = new int[count];
        _poolKills = new int[count];
        _poolRawDamage = new int[count];
        _poolWaste = new int[count];
        _poolOnDestroyedUnit = new bool[count];
    }

    /// <summary>Le pool commence à tirer : ses pertes lui seront attribuées ; on note si l'unité est déjà détruite.</summary>
    internal void BeginPool(int pool)
    {
        _currentPool = pool;
        _poolOnDestroyedUnit[pool] = IsDestroyed;
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
    /// perdu. Le groupe peut être <see cref="ExcessGroup"/>. <paramref name="viaPrecision"/> : attaque « grâce à
    /// Precision » (voir <see cref="PrecisionKilledInGroup"/>), comptée à part ; <paramref name="precisionWeapon"/> :
    /// attaque d'une arme Precision (voir <see cref="PrecisionWeaponDamageInGroup"/>) ; aucun dé de plus.
    /// </summary>
    internal void ApplyDamage(int group, int damage, bool mortalWounds, IDiceRoller dice, bool viaPrecision = false, bool precisionWeapon = false)
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

        RawDamage += lost;

        if (excess)
        {
            int excessApplied = Math.Min(lost, _excessFrontWounds);
            _excessFrontWounds -= excessApplied;
            ExcessDamage += excessApplied;
            if (_currentPool >= 0)
            {
                _poolWaste[_currentPool] += lost - excessApplied;
            }
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
        if (_currentPool >= 0)
        {
            _poolRawDamage[_currentPool] += lost;
            _poolDamage[_currentPool] += applied;
            _poolWaste[_currentPool] += lost - applied;
        }
        if (viaPrecision)
        {
            _precisionDamage[group] += applied;
        }
        if (precisionWeapon)
        {
            _precisionWeaponDamage[group] += applied;
        }

        if (_frontWounds[group] == 0)
        {
            _alive[group]--;
            ModelsAlive--;
            _frontWounds[group] = profile.Wounds;
            if (viaPrecision)
            {
                _precisionKilled[group]++;
            }
            if (_currentPool >= 0)
            {
                _poolKills[_currentPool]++;
            }
        }
    }
}
