using Microsoft.JSInterop;
using Wh40kSim.UI.Services;
using Wh40kSim.UI.Theme;

namespace Wh40kSim.Web.Client.Services;

/// <summary>
/// Préférences dans le stockage du navigateur (<c>localStorage</c>). La même clé est lue par la page du serveur pour
/// colorer la barre de chargement avant que le client démarre.
/// </summary>
public sealed class BrowserPreferenceService(IJSRuntime js) : IPreferenceService
{
    public const string ThemeKey = "wh40ksim.theme";

    public async Task<ThemeChoice?> GetThemeAsync()
    {
        try
        {
            string? value = await js.InvokeAsync<string?>("localStorage.getItem", ThemeKey);
            return Enum.TryParse(value, ignoreCase: true, out ThemeChoice theme) && Enum.IsDefined(theme) ? theme : null;
        }
        catch (JSException)
        {
            return null; // stockage indisponible (navigation privée stricte…) : thème par défaut
        }
    }

    public async Task SetThemeAsync(ThemeChoice theme)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", ThemeKey, theme.ToString().ToLowerInvariant());
        }
        catch (JSException)
        {
            // Stockage indisponible : le thème vaut pour cette visite seulement
        }
    }
}
