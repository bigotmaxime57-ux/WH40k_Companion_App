using System.Text.RegularExpressions;
using System.Xml.Linq;
using Wh40kSim.UI.Keywords;
using Wh40kSim.UI.Theme;

namespace Wh40kSim.UI.Tests;

/// <summary>
/// Les textes de l'interface viennent de Resources/Strings.resx. Une clé absente ne provoque aucune erreur : l'interface
/// afficherait simplement la clé. On vérifie donc que chaque clé utilisée existe, et que le fichier n'en garde pas
/// d'inutiles (il servira de modèle aux traductions de la phase 7).
/// </summary>
public class StringsTests
{
    private static readonly string UiProject = Path.Combine(RepositoryRoot(), "src", "Wh40kSim.UI");

    /// <summary>Clés écrites telles quelles dans le code (L["…"]), plus celles construites à partir du catalogue de mots-clés et des thèmes.</summary>
    private static HashSet<string> UsedKeys()
    {
        var keys = Directory.EnumerateFiles(UiProject, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".razor") || f.EndsWith(".cs")) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\bL\[""(?<key>[A-Za-z_]+)""").Select(m => m.Groups["key"].Value))
            .ToHashSet();
        foreach (var keyword in WeaponKeywords.All)
        {
            keys.UnionWith([keyword.NameKey, keyword.HelpKey, keyword.CategoryKey]);
        }
        keys.UnionWith(Enum.GetValues<ThemeChoice>().Select(Wh40kTheme.NameKey));
        return keys;
    }

    private static HashSet<string> ResourceKeys() =>
        XDocument.Load(Path.Combine(UiProject, "Resources", "Strings.resx"))
            .Root!.Elements("data").Select(d => (string)d.Attribute("name")!)
            .ToHashSet();

    [Fact]
    public void Every_key_used_by_the_interface_exists()
    {
        var missing = UsedKeys().Except(ResourceKeys()).Order();

        Assert.Empty(missing);
    }

    [Fact]
    public void Every_resource_key_is_used()
    {
        var unused = ResourceKeys().Except(UsedKeys()).Order();

        Assert.Empty(unused);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Wh40kSim.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Racine de la solution introuvable.");
    }
}
