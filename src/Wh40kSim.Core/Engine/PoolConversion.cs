namespace Wh40kSim.Core.Engine;

/// <summary>
/// Conversion des attaques d'un pool pendant une séquence : attaques, touches, blessures, sauvegardes ratées, avec la
/// part des mots-clés qui en ajoutent ou en sautent une étape. Compté seulement si le pool commence à tirer sur une
/// unité encore en jeu (<see cref="Fired"/>) ; si l'unité meurt pendant qu'il tire, ses dés restent comptés jusqu'au
/// bout (figurines virtuelles de l'excédent comprises). Aucun dé n'est consommé pour ce suivi.
/// </summary>
public sealed class PoolConversion
{
    /// <summary>Le pool a tiré sur une unité encore en jeu : les compteurs ci-dessous sont renseignés.</summary>
    public bool Fired { get; internal set; }

    /// <summary>Attaques du pool, bonus de Blast et de Rapid Fire compris.</summary>
    public int Attacks { get; internal set; }

    /// <summary>Attaques ajoutées par Blast (+1 par tranche de 5 figurines au début de l'activation).</summary>
    public int BlastAttacks { get; internal set; }

    /// <summary>Attaques ajoutées par Rapid Fire (à demi-portée).</summary>
    public int RapidFireAttacks { get; internal set; }

    /// <summary>Touches : touches réussies, touches Lethal et touches supplémentaires de Sustained Hits comprises.</summary>
    public int Hits { get; internal set; }

    /// <summary>Touches supplémentaires de Sustained Hits.</summary>
    public int SustainedHits { get; internal set; }

    /// <summary>Touches critiques Lethal Hits : blessures automatiques, sans jet de blessure.</summary>
    public int LethalHits { get; internal set; }

    /// <summary>Blessures : réussies au jet, Lethal Hits et Devastating Wounds comprises.</summary>
    public int Wounds { get; internal set; }

    /// <summary>Blessures critiques Devastating Wounds : blessures mortelles, sans jet de sauvegarde.</summary>
    public int DevastatingWounds { get; internal set; }

    /// <summary>Sauvegardes ratées (1 naturel compris), plus les Devastating Wounds qui n'en permettent aucune.</summary>
    public int FailedSaves { get; internal set; }

    internal void Reset()
    {
        Fired = false;
        Attacks = BlastAttacks = RapidFireAttacks = 0;
        Hits = SustainedHits = LethalHits = 0;
        Wounds = DevastatingWounds = FailedSaves = 0;
    }
}
