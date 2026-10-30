using Wh40kSim.UI.Services;

namespace Wh40kSim.Web.Client.Services;

/// <summary>
/// Fichiers dans le navigateur : ouverture par le sélecteur de fichier, enregistrement par un téléchargement sous le
/// nom proposé (décision de l'utilisateur : toujours un téléchargement, pas de boîte « Enregistrer sous »).
/// </summary>
public sealed class BrowserScenarioFileService(BrowserScripts scripts) : IScenarioFileService
{
    public Task<OpenedFile?> OpenAsync(CancellationToken cancellationToken = default) => scripts.OpenFileAsync(cancellationToken);

    /// <returns>Le nom proposé : le navigateur télécharge toujours, l'utilisateur ne peut pas annuler.</returns>
    public async Task<string?> SaveAsync(string content, string suggestedFileName, CancellationToken cancellationToken = default)
    {
        await scripts.DownloadFileAsync(suggestedFileName, content, cancellationToken);
        return suggestedFileName;
    }
}
