using Wh40kSim.UI.Services;
using Wh40kSim.UI.Theme;

namespace Wh40kSim.Desktop;

/// <summary>
/// Préférences de l'utilisateur dans les préférences de l'application MAUI (<see cref="IPreferences"/>), gardées d'une
/// ouverture à l'autre. <see cref="GetTheme"/> est synchrone : la fenêtre lit le thème avant le premier affichage.
/// </summary>
public sealed class DesktopPreferenceService(IPreferences preferences) : IPreferenceService
{
    private const string ThemeKey = "theme";
    private const string WindowMaximizedKey = "window-maximized";

    /// <summary>La fenêtre était agrandie (plein écran) à la dernière fermeture : on la rouvre agrandie.</summary>
    public bool WindowMaximized
    {
        get => preferences.Get(WindowMaximizedKey, false);
        set => preferences.Set(WindowMaximizedKey, value);
    }

    /// <summary>Thème mémorisé, ou null s'il n'y en a pas (ou si la valeur enregistrée n'est plus un thème connu).</summary>
    public ThemeChoice? GetTheme() =>
        Enum.TryParse(preferences.Get(ThemeKey, ""), out ThemeChoice theme) && Enum.IsDefined(theme) ? theme : null;

    public Task<ThemeChoice?> GetThemeAsync() => Task.FromResult(GetTheme());

    public Task SetThemeAsync(ThemeChoice theme)
    {
        preferences.Set(ThemeKey, theme.ToString());
        return Task.CompletedTask;
    }
}
