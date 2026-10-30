using Wh40kSim.Core.Model;
using static Wh40kSim.ConsoleApp.ConsoleInput;

namespace Wh40kSim.ConsoleApp;

/// <summary>Saisie guidée d'un scénario complet dans la console.</summary>
internal static class ScenarioWizard
{
    private const string AbilitiesHelp =
        "Mots-clés, séparés par des virgules (ex : lethal, sustained 1, dev, twin, torrent, blast, rapid 1, melta 2, heavy, lance, ignores cover, precision, anti 4+, crit hit 5+)";

    public static Scenario Build()
    {
        var attacker = ReadAttacker();
        var defender = ReadDefender();
        var context = ReadContext(attacker, defender);
        return new Scenario { Attacker = attacker, Defender = defender, Context = context };
    }

    private static AttackerUnit ReadAttacker()
    {
        WriteTitle("Attaquant");
        string name = ReadString("Nom de l'unité", "Attaquant");
        int profileCount = ReadInt("Nombre de profils d'armes différents", 1, 1, 20);

        var weapons = new List<WeaponGroup>();
        for (int i = 1; i <= profileCount; i++)
        {
            Console.WriteLine($"-- Profil d'arme {i} --");
            string weaponName = ReadString("Nom de l'arme", $"Arme {i}");
            bool melee = ReadString("Type (T = tir, M = mêlée)", "T").Trim().ToUpperInvariant().StartsWith('M');
            int models = ReadInt("Nombre de figurines équipées", 1, 1, 100);
            var attacks = ReadDice("Attaques (ex : 2, D6, D3+1)", 1);
            var abilities = ReadAbilities(AbilitiesHelp);
            int skill = abilities.Torrent ? 2 : ReadInt(melee ? "CC (ex : 3 pour 3+)" : "CT (ex : 3 pour 3+)", 3, 2, 6);
            int strength = ReadInt("Force", 4, 1, 30);
            int ap = ReadInt("PA (ex : -1)", 0, -6, 6);
            var damage = ReadDice("Dégâts (ex : 1, D3, D6+1)", 1);

            weapons.Add(new WeaponGroup
            {
                ModelCount = models,
                Weapon = new WeaponProfile
                {
                    Name = weaponName,
                    Type = melee ? WeaponType.Melee : WeaponType.Ranged,
                    Attacks = attacks,
                    Skill = skill,
                    Strength = strength,
                    Ap = ap,
                    Damage = damage,
                    Abilities = abilities,
                },
            });
        }

        // Boosts les plus courants ; les autres (CT, F, PA, D…) se saisissent dans le fichier JSON ou l'interface
        var boosts = new AttackerBoosts
        {
            HitRoll = ReadInt("Modificateur aux jets de touche de l'attaquant", 0, -3, 3),
            WoundRoll = ReadInt("Modificateur aux jets de blessure de l'attaquant", 0, -3, 3),
            HitReroll = ReadReroll("Relance des touches"),
            WoundReroll = ReadReroll("Relance des blessures"),
        };

        // Pêche aux critiques, arme par arme, seulement quand elle a un sens (conditions portées par le Core ; l'overwatch,
        // demandé plus tard, la rend sans effet à la touche)
        for (int i = 0; i < weapons.Count; i++)
        {
            var weapon = weapons[i].Weapon;
            bool hits = weapon.CanFishForCriticalHits(boosts, new AttackContext())
                        && ReadBool($"{weapon.Name} : pêche aux critiques à la touche (relancer aussi les touches non critiques)", false);
            bool wounds = weapon.CanFishForCriticalWounds(boosts)
                          && ReadBool($"{weapon.Name} : pêche aux critiques à la blessure (relancer aussi les blessures non critiques)", false);
            weapons[i] = weapons[i] with { Weapon = weapon with { FishForCriticalHits = hits, FishForCriticalWounds = wounds } };
        }

        return new AttackerUnit { Name = name, Boosts = boosts, Weapons = weapons };
    }

    private static DefenderUnit ReadDefender()
    {
        WriteTitle("Défenseur");
        string name = ReadString("Nom de l'unité", "Défenseur");
        bool inCover = ReadBool("À couvert", false);
        int hitModifier = ReadInt("Modificateur aux jets de touche subis (ex : -1 pour Stealth)", 0, -3, 3);
        int woundModifier = ReadInt("Modificateur aux jets de blessure subis", 0, -3, 3);
        int groupCount = ReadInt("Nombre de groupes de figurines (profils défensifs différents)", 1, 1, 10);
        Console.WriteLine("Les groupes sont saisis dans l'ordre d'allocation : le groupe 1 encaisse en premier.");
        Console.WriteLine("Un groupe par PERSONNAGE (après les autres), un groupe par combinaison PV/Sv/InSv pour les autres figurines.");

        var groups = new List<ModelGroup>();
        int toughness = 4;
        for (int i = 1; i <= groupCount; i++)
        {
            Console.WriteLine($"-- Groupe {i} --");
            string groupName = ReadString("Nom du groupe", i == 1 ? "Gardes du corps" : $"Groupe {i}");
            bool isCharacter = ReadBool("PERSONNAGE (leader)", false);
            groups.Add(new ModelGroup
            {
                Name = groupName,
                IsCharacter = isCharacter,
                Count = isCharacter ? 1 : ReadInt("Nombre de figurines", 5, 1, 100),
                // Endurance proposée par défaut : celle du groupe précédent
                Toughness = toughness = ReadInt("Endurance", toughness, 1, 30),
                Wounds = ReadInt("PV par figurine", 1, 1, 50),
                Save = ReadInt("Sauvegarde (ex : 3 pour 3+, 7 si aucune)", 3, 2, 7),
                InvulnerableSave = ReadOptionalThreshold("Invulnérable"),
                FeelNoPain = ReadOptionalThreshold("Insensible à la douleur"),
                DamageReduction = ReadInt("Réduction de dégâts", 0, 0, 5),
            });
        }

        return new DefenderUnit
        {
            Name = name,
            Boosts = new DefenderBoosts { InCover = inCover, HitRoll = hitModifier, WoundRoll = woundModifier },
            Groups = groups,
        };
    }

    private static AttackContext ReadContext(AttackerUnit attacker, DefenderUnit defender)
    {
        WriteTitle("Situation");

        // On ne pose les questions de situation que si une arme est concernée (règles portées par le Core)
        bool snapShooting = ReadBool("Tir en overwatch", false);
        var scenario = new Scenario { Attacker = attacker, Defender = defender, Context = new AttackContext { SnapShooting = snapShooting } };
        bool halfRange = scenario.HalfRangeMatters && ReadBool("À demi-portée", false);
        bool stationary = scenario.RemainedStationaryMatters && ReadBool("Restée immobile", false);
        bool charged = scenario.ChargedMatters && ReadBool("A chargé ce tour", false);

        var characters = defender.Groups.Where(g => g.IsCharacter).Select(g => g.Name).ToList();
        string? precisionTarget = null;
        if (scenario.PrecisionTargetMatters)
        {
            precisionTarget = ReadParsedName("PERSONNAGE visé par Precision (" + string.Join(" / ", characters) + ")", characters);
        }

        return new AttackContext
        {
            HalfRange = halfRange,
            RemainedStationary = stationary,
            Charged = charged,
            SnapShooting = snapShooting,
            PrecisionTarget = precisionTarget,
        };
    }

    private static string ReadParsedName(string label, List<string> allowed)
    {
        while (true)
        {
            string name = ReadString(label, allowed[0]);
            var match = allowed.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
            WriteError("Nom de PERSONNAGE inconnu.");
        }
    }
}
