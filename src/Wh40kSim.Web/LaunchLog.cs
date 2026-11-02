using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;

namespace Wh40kSim.Web;

/// <summary>
/// Journal des lancements : une ligne lisible par simulation ou comparaison demandée sur le site (ce qui a été demandé,
/// issue, attente, durée), ajoutée au fichier <see cref="SimulationOptions.LaunchLogPath"/> ; rien sans chemin (cas du
/// développement local). Aucune adresse IP. Une écriture impossible est signalée dans les journaux du serveur, sans
/// jamais faire échouer le lancement.
/// </summary>
public sealed class LaunchLog(IOptions<SimulationOptions> options, ILogger<LaunchLog> logger)
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private readonly Lock _lock = new();

    /// <summary>Ajoute une ligne, précédée de la date et l'heure (UTC) ; les parties sont séparées par « | ».</summary>
    public void Write(params string[] parts)
    {
        if (options.Value.LaunchLogPath is not { Length: > 0 } path)
        {
            return;
        }
        string line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z | {string.Join(" | ", parts.Select(Clean))}{Environment.NewLine}";
        try
        {
            lock (_lock)
            {
                if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
                {
                    Directory.CreateDirectory(directory);
                }
                File.AppendAllText(path, line, Encoding.UTF8);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Journal des lancements impossible à écrire ({Path})", path);
        }
    }

    /// <summary>Nom saisi par le visiteur, entre guillemets, sans ce qui casserait la ligne (« | », retours à la ligne).</summary>
    public static string Name(string? name) => $"« {(string.IsNullOrWhiteSpace(name) ? "sans nom" : name.Trim())} »";

    /// <summary>Nombre lisible, à la française (1 000 000).</summary>
    public static string Number(long value) => value.ToString("N0", French).Replace(' ', ' ').Replace(' ', ' ');

    /// <summary>Durée en secondes, une décimale (« 18,2 s »).</summary>
    public static string Seconds(TimeSpan duration) => $"{duration.TotalSeconds.ToString("F1", French)} s";

    public static string Seed(int? seed) => seed is int value ? $"graine {value}" : "graine aléatoire";

    private static string Clean(string part) => part.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
}
