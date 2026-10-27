using System.Text;
using CommunityToolkit.Maui.Storage;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;
using Wh40kSim.UI.Services;

namespace Wh40kSim.Desktop;

/// <summary>
/// Ouverture et enregistrement des scénarios avec les boîtes de dialogue de Windows : <see cref="IFilePicker"/> (MAUI)
/// pour ouvrir, qui ne permet pas de choisir le dossier de départ, et <see cref="IFileSaver"/> (CommunityToolkit.Maui)
/// pour enregistrer, qui part du dernier dossier utilisé, à défaut du dossier scenarios/ du dépôt s'il est trouvé.
/// </summary>
public sealed class DesktopScenarioFileService(IFilePicker filePicker, IFileSaver fileSaver) : IScenarioFileService
{
    private static readonly FilePickerFileType JsonFiles = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.WinUI] = [".json"],
    });

    private string? _lastDirectory = FindScenariosDirectory();

    public async Task<OpenedScenario?> OpenAsync(CancellationToken cancellationToken = default)
    {
        var file = await filePicker.PickAsync(new PickOptions { FileTypes = JsonFiles });
        if (file is null)
        {
            return null;
        }

        string json;
        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            json = await reader.ReadToEndAsync(cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException(ex.Message, ex);
        }

        // Une JsonException (fichier invalide, ancien format) remonte telle quelle : la page l'affiche
        var scenario = ScenarioJson.Deserialize(json);
        _lastDirectory = Path.GetDirectoryName(file.FullPath);
        return new OpenedScenario(file.FileName, scenario);
    }

    public async Task<string?> SaveAsync(Scenario scenario, string suggestedFileName, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ScenarioJson.Serialize(scenario)));
        var result = await fileSaver.SaveAsync(_lastDirectory ?? "", suggestedFileName, stream, cancellationToken);

        if (result.IsCancelled)
        {
            return null;
        }
        if (!result.IsSuccessful || result.FilePath is null)
        {
            throw new IOException(result.Exception?.Message ?? "Enregistrement impossible.", result.Exception);
        }

        _lastDirectory = Path.GetDirectoryName(result.FilePath);
        return Path.GetFileName(result.FilePath);
    }

    /// <summary>Dossier "scenarios" à la racine de la solution (lancement depuis le dépôt), sinon null.</summary>
    private static string? FindScenariosDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Wh40kSim.sln")))
        {
            directory = directory.Parent;
        }

        string? scenarios = directory is null ? null : Path.Combine(directory.FullName, "scenarios");
        return Directory.Exists(scenarios) ? scenarios : null;
    }
}
