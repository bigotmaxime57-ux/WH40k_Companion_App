using System.Text.Json.Serialization;

namespace Wh40kSim.Core.Model;

/// <summary>Tout ce qu'il faut pour simuler : attaquant, défenseur et situation.</summary>
public sealed record Scenario
{
    public AttackerUnit Attacker { get; init; } = new();

    public DefenderUnit Defender { get; init; } = new();

    public AttackContext Context { get; init; } = new();

    /// <summary>
    /// Le choix du PERSONNAGE visé par Precision change quelque chose : au moins une arme Precision et plusieurs
    /// PERSONNAGES (avec un seul, c'est forcément lui).
    /// </summary>
    [JsonIgnore]
    public bool PrecisionTargetMatters =>
        UsesPrecision && Defender.Groups.Count(g => g.IsCharacter) > 1;

    /// <summary>Au moins une arme de l'attaquant a Precision.</summary>
    [JsonIgnore]
    public bool UsesPrecision => Attacker.Weapons.Any(w => w.Weapon.Abilities.Precision);

    /// <summary>
    /// Groupes PERSONNAGES que Precision peut viser (indices dans l'ordre d'allocation) : la cible choisie si elle est
    /// nommée (seule visée), sinon tous les PERSONNAGES (chaque pool vise le premier encore en jeu). Vide sans arme
    /// Precision ou sans PERSONNAGE.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<int> PrecisionTargets =>
        UsesPrecision
            ? Enumerable.Range(0, Defender.Groups.Count)
                .Where(g => Defender.Groups[g].IsCharacter
                            && (Context.PrecisionTarget is null || Defender.Groups[g].Name == Context.PrecisionTarget))
                .ToList()
            : [];

    /// <summary>Renvoie la liste des incohérences (vide si le scénario est valide).</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Attacker.Weapons.Count == 0)
        {
            errors.Add("L'attaquant doit avoir au moins un profil d'arme.");
        }
        if (Attacker.Points < 0)
        {
            errors.Add("Les points de l'attaquant ne peuvent pas être négatifs.");
        }

        foreach (var group in Attacker.Weapons)
        {
            var weapon = group.Weapon;
            string label = $"Arme '{weapon.Name}'";
            if (group.ModelCount < 1) errors.Add($"{label} : le nombre de figurines doit être d'au moins 1.");
            if (!weapon.Abilities.Torrent && (weapon.Skill < 2 || weapon.Skill > 6)) errors.Add($"{label} : CT/CC doit être entre 2 et 6.");
            if (weapon.Strength < 1) errors.Add($"{label} : la Force doit être d'au moins 1.");
            if (weapon.Abilities.CriticalHitOn is < 2 or > 6) errors.Add($"{label} : la touche critique doit être entre 2 et 6.");
            if (weapon.Abilities.CriticalWoundOn is < 2 or > 6) errors.Add($"{label} : la blessure critique doit être entre 2 et 6.");
            if (weapon.Abilities.RapidFire < 0 || weapon.Abilities.Melta < 0) errors.Add($"{label} : Rapid Fire et Melta ne peuvent pas être négatifs.");
        }

        if (Defender.Groups.Count == 0)
        {
            errors.Add("Le défenseur doit avoir au moins un groupe de figurines.");
        }

        foreach (var group in Defender.Groups)
        {
            string label = $"Groupe '{group.Name}'";
            if (group.Count < 1) errors.Add($"{label} : au moins 1 figurine.");
            if (group.Wounds < 1) errors.Add($"{label} : au moins 1 PV.");
            if (group.Save is < 2 or > 7) errors.Add($"{label} : la sauvegarde doit être entre 2 et 7 (7 = aucune).");
            if (group.InvulnerableSave is < 2 or > 6) errors.Add($"{label} : l'invulnérable doit être entre 2 et 6.");
            if (group.FeelNoPain is < 2 or > 6) errors.Add($"{label} : l'insensible à la douleur doit être entre 2 et 6.");
            if (group.DamageReduction < 0) errors.Add($"{label} : la réduction de dégâts ne peut pas être négative.");
            if (group.Toughness < 1) errors.Add($"{label} : l'Endurance doit être d'au moins 1.");
            if (group.IsCharacter && group.Count != 1) errors.Add($"{label} : un groupe PERSONNAGE contient exactement une figurine.");
            if (group.Points < 0) errors.Add($"{label} : les points ne peuvent pas être négatifs.");
        }

        if (Context.PrecisionTarget is string precisionTarget
            && !Defender.Groups.Any(g => g.IsCharacter && g.Name == precisionTarget))
        {
            errors.Add($"Cible de Precision '{precisionTarget}' : aucun groupe PERSONNAGE de ce nom.");
        }

        // 05.03 : aucun PERSONNAGE avant un groupe non-PERSONNAGE
        if (AllocationOrder.FindViolation(Defender.Groups.Select(g => g.IsCharacter).ToList()) is var (character, bodyguard))
        {
            errors.Add($"Le PERSONNAGE '{Defender.Groups[character].Name}' ne peut pas être alloué avant le groupe '{Defender.Groups[bodyguard].Name}'.");
        }

        // 05.03 : les figurines non-PERSONNAGE aux mêmes PV/Sv/InSv forment un seul groupe
        var regular = Defender.Groups.Where(g => !g.IsCharacter).ToList();
        for (int i = 0; i < regular.Count; i++)
        {
            for (int j = i + 1; j < regular.Count; j++)
            {
                if (regular[i].Wounds == regular[j].Wounds && regular[i].Save == regular[j].Save && regular[i].InvulnerableSave == regular[j].InvulnerableSave)
                {
                    errors.Add($"Les groupes '{regular[i].Name}' et '{regular[j].Name}' ont les mêmes PV, Sv et InSv : ils forment un seul groupe.");
                }
            }
        }

        return errors;
    }

    public void EnsureValid()
    {
        var errors = Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException("Scénario invalide :" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => "  - " + e)));
        }
    }
}
