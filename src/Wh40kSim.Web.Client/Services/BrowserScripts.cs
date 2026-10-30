using Microsoft.JSInterop;
using Wh40kSim.UI.Services;

namespace Wh40kSim.Web.Client.Services;

/// <summary>Accès au module JavaScript du client (wwwroot/wh40ksim-web.js), chargé à la première utilisation.</summary>
public sealed class BrowserScripts(IJSRuntime js)
{
    private const string ModulePath = "./wh40ksim-web.js";
    private Task<IJSObjectReference>? _module;

    private Task<IJSObjectReference> Module => _module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

    /// <summary>Sélecteur de fichier du navigateur ; null si l'utilisateur annule.</summary>
    public async Task<OpenedFile?> OpenFileAsync(CancellationToken cancellationToken = default) =>
        await (await Module).InvokeAsync<OpenedFile?>("openFile", cancellationToken);

    /// <summary>Téléchargement d'un fichier sous le nom proposé.</summary>
    public async Task DownloadFileAsync(string fileName, string content, CancellationToken cancellationToken = default) =>
        await (await Module).InvokeVoidAsync("downloadFile", cancellationToken, fileName, content);

    public async Task<bool> PrefersDarkAsync() => await (await Module).InvokeAsync<bool>("prefersDark");

    public async Task HideBootAsync() => await (await Module).InvokeVoidAsync("hideBoot");
}
