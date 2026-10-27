using System.Globalization;

namespace Wh40kSim.UI;

/// <summary>
/// Culture de l'interface : textes et format des nombres (« 12,5 % »). Fixée, et non prise sur la machine, en attendant
/// le choix de la langue (phase 4).
/// </summary>
public static class UiCulture
{
    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>À appeler par l'hôte au démarrage, avant d'afficher l'interface.</summary>
    public static void Apply()
    {
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
    }
}
