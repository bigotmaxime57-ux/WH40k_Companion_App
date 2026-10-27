using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Engine;

/// <summary>
/// Résout une fois la séquence d'attaque complète de l'attaquant contre le défenseur (règles 05, 11e édition).
/// <list type="bullet">
/// <item>Les armes aux attaques identiques sont regroupées en un seul pool (ex. bolters + pistolets bolter) :
/// leurs dés sont lancés ensemble, y compris les sauvegardes.</item>
/// <item>Le couvert ne modifie plus la PA : il dégrade de 1 la CT des armes de tir (CT bornée entre 2+ et 6+).</item>
/// <item>Les sauvegardes d'un pool sont jetées ensemble et résolues du plus petit au plus grand résultat,
/// sur le groupe d'allocation courant, jusqu'à sa destruction.</item>
/// <item>L'Endurance est recalculée avant chaque pool (05.02.01).</item>
/// </list>
/// <para>Excédent (optionnel) : avec des dés d'excédent, la séquence continue après la destruction de l'unité contre
/// des figurines virtuelles (<see cref="DefenderState.ExcessProfile"/>). Tout ce qui suit la destruction est tiré sur
/// ces dés : les dés principaux sont consommés exactement comme sans excédent, le résultat réel est donc identique.</para>
/// Une instance n'est pas thread-safe : en créer une par thread.
/// </summary>
public sealed class AttackResolver
{
    private enum RollOutcome
    {
        Fail,
        Success,
        Critical,
    }

    private readonly Scenario _scenario;
    private readonly DefenderState _target;
    private readonly IReadOnlyList<AttackPool> _pools;
    private readonly int[] _saveRollCounts = new int[7]; // index 1..6 = nombre de dés ayant fait cette valeur
    private IDiceRoller _dice = null!;
    private IDiceRoller? _excessDice;

    public AttackResolver(Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.EnsureValid();
        _scenario = scenario;
        _target = new DefenderState(scenario.Defender);
        _pools = AttackPool.Build(scenario.Attacker.Weapons);
    }

    /// <summary>État du défenseur après le dernier appel à <see cref="Run"/>.</summary>
    public DefenderState Target => _target;

    /// <summary>Pools d'attaques identiques, dans l'ordre de résolution.</summary>
    public IReadOnlyList<AttackPool> Pools => _pools;

    /// <summary>Excédent calculé pendant le run en cours (dés d'excédent fournis).</summary>
    private bool TracksExcess => _excessDice is not null;

    /// <summary>Dés à utiliser maintenant : ceux de l'excédent une fois l'unité détruite.</summary>
    private IDiceRoller Dice => TracksExcess && _target.IsDestroyed ? _excessDice! : _dice;

    /// <summary>Remet le défenseur à neuf puis résout tous les pools d'attaques, dans l'ordre.</summary>
    /// <param name="dice">Dés de la séquence réelle.</param>
    /// <param name="excessDice">
    /// Dés de l'excédent : si fournis, la séquence continue après la destruction de l'unité et
    /// <see cref="DefenderState.ExcessModelsKilled"/> / <see cref="DefenderState.ExcessDamage"/> sont renseignés.
    /// </param>
    public void Run(IDiceRoller dice, IDiceRoller? excessDice = null)
    {
        ArgumentNullException.ThrowIfNull(dice);
        _dice = dice;
        _excessDice = excessDice;
        _target.Reset();
        int initialModels = _target.ModelsAlive;

        foreach (var pool in _pools)
        {
            if (_target.IsDestroyed && !TracksExcess)
            {
                return;
            }
            ResolvePool(pool, initialModels);
        }
    }

    private void ResolvePool(AttackPool pool, int initialModels)
    {
        var weapon = pool.Profile;
        int attacks = CountAttacks(pool, initialModels);
        ResolveHits(weapon, attacks, out int hits, out int autoWounds);
        ResolveWounds(weapon, hits, out int normalWounds, out int devastatingWounds);

        // Precision : le choix se fait au début de l'étape d'allocation (05.03), une fois par pool
        int precisionGroup = weapon.Abilities.Precision ? SelectPrecisionGroup() : -1;

        ResolveSaves(weapon, normalWounds + autoWounds, precisionGroup);
        ResolveDevastatingWounds(weapon, devastatingWounds, precisionGroup);
    }

    /// <summary>Groupe PERSONNAGE visé par Precision : celui nommé dans le contexte, sinon le premier encore en jeu.</summary>
    private int SelectPrecisionGroup()
    {
        var groups = _scenario.Defender.Groups;
        string? wanted = _scenario.Context.PrecisionTarget;
        for (int g = 0; g < groups.Count; g++)
        {
            if (groups[g].IsCharacter && _target.AliveInGroup(g) > 0 && (wanted is null || groups[g].Name == wanted))
            {
                return g;
            }
        }
        return -1;
    }

    /// <summary>
    /// Groupe qui encaisse la prochaine attaque : le groupe d'allocation courant ; une fois l'unité détruite,
    /// les figurines virtuelles de l'excédent si on le calcule, sinon -1 (attaques perdues).
    /// </summary>
    private int AllocationGroup(int precisionGroup)
    {
        int g = _target.CurrentGroup(precisionGroup);
        return g < 0 && TracksExcess ? _target.ExcessGroup : g;
    }

    // --- 1. Nombre d'attaques -------------------------------------------------------------

    private int CountAttacks(AttackPool pool, int initialModels)
    {
        int attacks = 0;
        foreach (var member in pool.Members)
        {
            var abilities = member.Weapon.Abilities;
            int bonusPerModel = 0;
            if (abilities.Blast)
            {
                bonusPerModel += initialModels / 5;
            }
            if (abilities.RapidFire > 0 && _scenario.Context.HalfRange)
            {
                bonusPerModel += abilities.RapidFire;
            }

            for (int model = 0; model < member.ModelCount; model++)
            {
                // A modifiée, puis jamais inférieure à 1 (02.02.01) ; Blast et Rapid Fire s'ajoutent ensuite
                attacks += Math.Max(1, member.Weapon.Attacks.Roll(Dice) + _scenario.Context.AttacksModifier) + bonusPerModel;
            }
        }
        return attacks;
    }

    // --- 2. Jets de touche ----------------------------------------------------------------

    private void ResolveHits(WeaponProfile weapon, int attacks, out int hits, out int autoWounds)
    {
        var abilities = weapon.Abilities;
        hits = 0;
        autoWounds = 0;

        if (abilities.Torrent)
        {
            hits = attacks;
            return;
        }

        var context = _scenario.Context;
        var defender = _scenario.Defender;
        int skill = EffectiveSkill(weapon);

        int modifier = context.HitModifier + defender.HitModifier;
        if (weapon.UsesRemainedStationary && context.RemainedStationary)
        {
            modifier += 1;
        }
        modifier = Math.Clamp(modifier, -1, 1);

        for (int i = 0; i < attacks; i++)
        {
            int roll = Dice.D6();
            var outcome = Evaluate(roll, skill, modifier, abilities.CriticalHitOn);
            if (ShouldReroll(context.HitReroll, roll, outcome))
            {
                roll = Dice.D6();
                outcome = Evaluate(roll, skill, modifier, abilities.CriticalHitOn);
            }

            if (outcome == RollOutcome.Fail)
            {
                continue;
            }

            if (outcome == RollOutcome.Critical)
            {
                if (!abilities.SustainedHits.IsZero)
                {
                    hits += abilities.SustainedHits.Roll(Dice);
                }
                if (abilities.LethalHits)
                {
                    autoWounds++;
                    continue;
                }
            }

            hits++;
        }
    }

    // --- 3. Jets de blessure --------------------------------------------------------------

    private void ResolveWounds(WeaponProfile weapon, int hits, out int normalWounds, out int devastatingWounds)
    {
        var abilities = weapon.Abilities;
        var context = _scenario.Context;
        normalWounds = 0;
        devastatingWounds = 0;

        int target = WoundTable.RequiredRoll(EffectiveStrength(weapon), _target.EffectiveToughness());

        int modifier = context.WoundModifier + _scenario.Defender.WoundModifier;
        if (weapon.UsesCharged && context.Charged)
        {
            modifier += 1;
        }
        modifier = Math.Clamp(modifier, -1, 1);

        // Twin-linked = relance des échecs ; on garde la relance la plus large disponible
        var reroll = (RerollPolicy)Math.Max((int)context.WoundReroll, abilities.TwinLinked ? (int)RerollPolicy.Failed : 0);

        for (int i = 0; i < hits; i++)
        {
            int roll = Dice.D6();
            var outcome = Evaluate(roll, target, modifier, abilities.CriticalWoundOn);
            if (ShouldReroll(reroll, roll, outcome))
            {
                roll = Dice.D6();
                outcome = Evaluate(roll, target, modifier, abilities.CriticalWoundOn);
            }

            if (outcome == RollOutcome.Fail)
            {
                continue;
            }

            if (outcome == RollOutcome.Critical && abilities.DevastatingWounds)
            {
                devastatingWounds++;
            }
            else
            {
                normalWounds++;
            }
        }
    }

    // --- 4. Sauvegardes et dégâts (05.03 / 05.04) -----------------------------------------

    private void ResolveSaves(WeaponProfile weapon, int wounds, int precisionGroup)
    {
        if (wounds == 0)
        {
            return;
        }

        // Tous les jets de sauvegarde sont lancés d'un coup puis traités du plus petit au plus grand.
        Array.Clear(_saveRollCounts);
        for (int i = 0; i < wounds; i++)
        {
            _saveRollCounts[Dice.D6()]++;
        }

        int ap = EffectiveAp(weapon);

        for (int face = 1; face <= 6; face++)
        {
            for (int n = _saveRollCounts[face]; n > 0; n--)
            {
                // Groupe d'allocation courant : le PERSONNAGE visé par Precision tant qu'il vit, sinon le premier en jeu
                int g = AllocationGroup(precisionGroup);
                if (g < 0)
                {
                    return; // unité détruite : les attaques restantes sont perdues
                }

                var group = _target.Profile(g);
                // 1 naturel = dégâts ; résultat ≥ InSv = échec de l'attaque ; résultat + PA ≥ Sv = échec de l'attaque
                bool saved = face != 1
                             && ((group.InvulnerableSave is int invuln && face >= invuln) || face + ap >= group.Save);
                if (!saved)
                {
                    _target.ApplyDamage(g, RollDamage(weapon), mortalWounds: false, Dice);
                }
            }
        }
    }

    // --- 5. Devastating Wounds ------------------------------------------------------------

    /// <summary>
    /// Devastating Wounds (24.10) : chaque blessure critique inflige D blessures mortelles, après les dégâts normaux.
    /// Une seule figurine par blessure critique (excédent perdu), Feel No Pain applicable, pas de réduction de dégâts.
    /// Avec Precision (24.28.01), elles vont d'abord sur le PERSONNAGE visé s'il est encore en jeu.
    /// </summary>
    private void ResolveDevastatingWounds(WeaponProfile weapon, int count, int precisionGroup)
    {
        for (int i = 0; i < count; i++)
        {
            int g = AllocationGroup(precisionGroup);
            if (g < 0)
            {
                return;
            }
            _target.ApplyDamage(g, RollDamage(weapon), mortalWounds: true, Dice);
        }
    }

    // --- Caractéristiques modifiées (02.02.01) ---------------------------------------------
    // Modifications de caractéristiques : sans plafond (contrairement aux modificateurs aux jets), mais chaque
    // caractéristique garde ses bornes. Convention : +1 = mieux pour l'attaquant.

    /// <summary>
    /// CT / CC après boosts de l'attaquant et du défenseur, et couvert (11e : CT dégradée de 1, armes de tir seulement,
    /// sauf Ignores Cover) ; ni 1+ ni 7+ (bornée entre 2+ et 6+).
    /// </summary>
    private int EffectiveSkill(WeaponProfile weapon)
    {
        int improvement = _scenario.Context.SkillModifier + _scenario.Defender.SkillModifier;
        if (weapon.Type == WeaponType.Ranged && _scenario.Defender.InCover && !weapon.Abilities.IgnoresCover)
        {
            improvement -= 1;
        }
        return Math.Clamp(weapon.Skill - improvement, 2, 6);
    }

    /// <summary>Force après modification ; jamais inférieure à 1.</summary>
    private int EffectiveStrength(WeaponProfile weapon) => Math.Max(1, weapon.Strength + _scenario.Context.StrengthModifier);

    /// <summary>
    /// PA (valeur négative ou nulle, ex. -2) après boosts de l'attaquant et du défenseur ; ne dépasse jamais 0.
    /// La PA du profil est lue comme négative même si elle est saisie positive.
    /// </summary>
    private int EffectiveAp(WeaponProfile weapon) =>
        Math.Min(0, -Math.Abs(weapon.Ap) - (_scenario.Context.ApModifier + _scenario.Defender.ApModifier));

    // --- Utilitaires ----------------------------------------------------------------------

    private int RollDamage(WeaponProfile weapon)
    {
        // D modifié, puis jamais inférieur à 1 (02.02.01) ; Melta s'ajoute ensuite
        int damage = Math.Max(1, weapon.Damage.Roll(Dice) + _scenario.Context.DamageModifier);
        if (weapon.Abilities.Melta > 0 && _scenario.Context.HalfRange)
        {
            damage += weapon.Abilities.Melta;
        }
        return damage;
    }

    /// <summary>1 naturel = échec, résultat critique (non modifié) = réussite, sinon résultat modifié ≥ cible.</summary>
    private static RollOutcome Evaluate(int roll, int target, int modifier, int criticalOn)
    {
        if (roll == 1)
        {
            return RollOutcome.Fail;
        }
        if (roll >= criticalOn)
        {
            return RollOutcome.Critical;
        }
        return roll + modifier >= target ? RollOutcome.Success : RollOutcome.Fail;
    }

    private static bool ShouldReroll(RerollPolicy policy, int roll, RollOutcome outcome) => policy switch
    {
        RerollPolicy.Ones => roll == 1,
        RerollPolicy.Failed => outcome == RollOutcome.Fail,
        RerollPolicy.NonCritical => outcome != RollOutcome.Critical,
        _ => false,
    };
}
