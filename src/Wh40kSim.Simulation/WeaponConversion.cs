namespace Wh40kSim.Simulation;

/// <summary>
/// Conversion des attaques d'un pool en touches, blessures et sauvegardes ratées : moyennes par activation où le pool
/// tire sur une unité encore en jeu (s'il tire sur une unité déjà détruite, on ne lance plus ses dés : voir
/// <see cref="WeaponPoolLosses.ProbabilityUnitAlreadyDestroyed"/>). Si l'unité meurt pendant qu'il tire, ses dés restent
/// comptés jusqu'au bout : les taux décrivent l'arme, pas l'ordre de tir.
/// </summary>
/// <param name="MeanAttacks">Attaques, bonus de Blast et de Rapid Fire compris.</param>
/// <param name="MeanBlastAttacks">Attaques ajoutées par Blast.</param>
/// <param name="MeanRapidFireAttacks">Attaques ajoutées par Rapid Fire.</param>
/// <param name="MeanHits">Touches, Lethal Hits et touches supplémentaires de Sustained Hits comprises.</param>
/// <param name="MeanSustainedHits">Touches supplémentaires de Sustained Hits.</param>
/// <param name="MeanLethalHits">Touches Lethal Hits : blessures sans jet de blessure.</param>
/// <param name="MeanWounds">Blessures, Lethal Hits et Devastating Wounds comprises.</param>
/// <param name="MeanDevastatingWounds">Blessures Devastating Wounds : mortelles, sans jet de sauvegarde.</param>
/// <param name="MeanFailedSaves">Sauvegardes ratées, Devastating Wounds comprises (aucune sauvegarde possible).</param>
public sealed record WeaponConversion(
    double MeanAttacks,
    double MeanBlastAttacks,
    double MeanRapidFireAttacks,
    double MeanHits,
    double MeanSustainedHits,
    double MeanLethalHits,
    double MeanWounds,
    double MeanDevastatingWounds,
    double MeanFailedSaves)
{
    /// <summary>Touches par attaque (peut dépasser 100 % avec Sustained Hits).</summary>
    public double HitRate => Ratio(MeanHits, MeanAttacks);

    /// <summary>Blessures par touche (Lethal Hits comprises).</summary>
    public double WoundRate => Ratio(MeanWounds, MeanHits);

    /// <summary>Sauvegardes ratées par blessure (Devastating Wounds comprises).</summary>
    public double FailedSaveRate => Ratio(MeanFailedSaves, MeanWounds);

    /// <summary>
    /// Blessures gagnées grâce à Lethal Hits, estimées : chaque touche Lethal aurait échoué au jet de blessure dans la
    /// proportion observée sur les touches jetées par ce pool.
    /// </summary>
    public double LethalHitsGain
    {
        get
        {
            double rolledHits = MeanHits - MeanLethalHits;
            double rolledWounds = MeanWounds - MeanLethalHits;
            return rolledHits > 0 ? MeanLethalHits * (1 - rolledWounds / rolledHits) : 0;
        }
    }

    /// <summary>
    /// Sauvegardes évitées grâce à Devastating Wounds, estimées : chaque blessure Devastating aurait été sauvegardée dans
    /// la proportion observée sur les sauvegardes jetées contre ce pool.
    /// </summary>
    public double DevastatingWoundsGain
    {
        get
        {
            double savesRolled = MeanWounds - MeanDevastatingWounds;
            double failed = MeanFailedSaves - MeanDevastatingWounds;
            return savesRolled > 0 ? MeanDevastatingWounds * (1 - failed / savesRolled) : 0;
        }
    }

    private static double Ratio(double part, double total) => total > 0 ? part / total : 0;
}
