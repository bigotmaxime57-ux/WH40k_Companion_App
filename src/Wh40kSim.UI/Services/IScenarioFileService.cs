using Wh40kSim.Core.Model;

namespace Wh40kSim.UI.Services;

/// <summary>Scénario lu depuis un fichier, avec le nom de ce fichier (proposé de nouveau à l'enregistrement).</summary>
public sealed record OpenedScenario(string FileName, Scenario Scenario);

/// <summary>
/// Ouvre et enregistre des scénarios JSON (format de <see cref="Core.Serialization.ScenarioJson"/>,
/// compatible avec les fichiers du dossier scenarios/). Implémenté par l'hôte :
/// boîtes de dialogue natives en desktop, upload / download en web.
/// </summary>
public interface IScenarioFileService
{
    /// <summary>Demande un fichier à l'utilisateur et le lit.</summary>
    /// <returns>Le scénario lu, ou null si l'utilisateur a annulé.</returns>
    /// <exception cref="System.Text.Json.JsonException">Si le fichier n'est pas un scénario valide.</exception>
    Task<OpenedScenario?> OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>Demande où enregistrer le scénario (en proposant <paramref name="suggestedFileName"/>) et l'écrit.</summary>
    /// <returns>Le nom du fichier écrit, ou null si l'utilisateur a annulé.</returns>
    Task<string?> SaveAsync(Scenario scenario, string suggestedFileName, CancellationToken cancellationToken = default);
}
