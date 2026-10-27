using Wh40kSim.UI.Theme;

namespace Wh40kSim.UI.Services;

/// <summary>
/// Préférences de l'utilisateur, gardées d'une ouverture à l'autre. Implémenté par l'hôte : préférences de
/// l'application en desktop, stockage du navigateur en web.
/// </summary>
public interface IPreferenceService
{
    /// <summary>Thème choisi, ou null si l'utilisateur n'en a encore jamais choisi.</summary>
    Task<ThemeChoice?> GetThemeAsync();

    Task SetThemeAsync(ThemeChoice theme);
}
