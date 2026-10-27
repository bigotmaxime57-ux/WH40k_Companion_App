namespace Wh40kSim.UI.Services;

/// <summary>Fichier lu : son nom (proposé de nouveau à l'enregistrement) et son contenu, du JSON non encore interprété.</summary>
public sealed record OpenedFile(string FileName, string Content);

/// <summary>
/// Ouvre et enregistre des fichiers JSON (scénarios complets, attaquants seuls, défenseurs seuls). Implémenté par
/// l'hôte : boîtes de dialogue natives en desktop, upload / download en web. L'hôte ne fait que choisir le fichier et
/// en lire ou écrire le contenu ; l'interface le convertit avec <see cref="Core.Serialization.ScenarioJson"/>.
/// </summary>
public interface IScenarioFileService
{
    /// <summary>Demande un fichier JSON à l'utilisateur et lit son contenu.</summary>
    /// <returns>Le fichier lu, ou null si l'utilisateur a annulé.</returns>
    /// <exception cref="System.IO.IOException">Si le fichier ne peut pas être lu.</exception>
    Task<OpenedFile?> OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>Demande où enregistrer (en proposant <paramref name="suggestedFileName"/>) et écrit <paramref name="content"/>.</summary>
    /// <returns>Le nom du fichier écrit, ou null si l'utilisateur a annulé.</returns>
    /// <exception cref="System.IO.IOException">Si le fichier ne peut pas être écrit.</exception>
    Task<string?> SaveAsync(string content, string suggestedFileName, CancellationToken cancellationToken = default);
}
