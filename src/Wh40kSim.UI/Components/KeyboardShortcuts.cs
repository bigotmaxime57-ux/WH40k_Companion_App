using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Wh40kSim.UI.Components;

/// <summary>
/// Raccourcis clavier d'un écran de saisie (Ctrl+Entrée, Ctrl+S, Ctrl+O), écoutés à l'échelle du document par le module
/// JavaScript de Wh40kSim.UI, chargé à la demande ; transmis à <paramref name="onShortcut"/> (« enter », « s », « o »).
/// Sert aussi au défilement sur téléphone. Commun au Quick sim, au Full sim et au comparateur.
/// </summary>
public sealed class KeyboardShortcuts(Func<string, Task> onShortcut) : IAsyncDisposable
{
    private const string ModulePath = "./_content/Wh40kSim.UI/wh40ksim.js";

    private IJSObjectReference? _module;
    private IJSObjectReference? _registration;
    private DotNetObjectReference<KeyboardShortcuts>? _self;

    /// <summary>Charge le module et commence l'écoute ; sans JavaScript, l'écran reste utilisable à la souris.</summary>
    public async Task RegisterAsync(IJSRuntime js)
    {
        try
        {
            _self = DotNetObjectReference.Create(this);
            _module = await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
            if (_module is not null)
            {
                _registration = await _module.InvokeAsync<IJSObjectReference>("registerShortcuts", _self);
            }
        }
        catch (JSException)
        {
            // Raccourcis indisponibles : la page reste utilisable à la souris
        }
    }

    /// <summary>Appelé par le module JavaScript.</summary>
    [JSInvokable]
    public Task OnShortcut(string key) => onShortcut(key);

    /// <summary>Fait défiler la page jusqu'à <paramref name="element"/>, sur téléphone seulement.</summary>
    public async Task ScrollIntoViewOnPhoneAsync(ElementReference element)
    {
        try
        {
            if (_module is not null) await _module.InvokeVoidAsync("scrollIntoViewOnPhone", element);
        }
        catch (JSException)
        {
            // Défilement impossible : le contenu reste accessible plus bas
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_registration is not null) await _registration.InvokeVoidAsync("dispose");
            if (_registration is not null) await _registration.DisposeAsync();
            if (_module is not null) await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // L'interface est déjà fermée : rien à détacher
        }
        _self?.Dispose();
    }
}
