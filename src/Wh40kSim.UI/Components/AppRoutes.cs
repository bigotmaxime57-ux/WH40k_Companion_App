namespace Wh40kSim.UI.Components;

/// <summary>
/// Adresses des écrans, relatives à la base de l'application (sans « / » initial). Elles doivent correspondre aux
/// directives <c>@page</c> des écrans (testé).
/// </summary>
public static class AppRoutes
{
    public const string Home = "";

    public const string QuickSim = "quick-sim";

    /// <summary>Saisie du Full sim.</summary>
    public const string FullSim = "full-sim";

    /// <summary>Résultats du Full sim (onglets de graphiques).</summary>
    public const string FullSimResults = "full-sim/results";
}
