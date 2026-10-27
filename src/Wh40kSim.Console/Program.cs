using System.Globalization;
using System.Text;
using System.Text.Json;
using Wh40kSim.ConsoleApp;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;
using Wh40kSim.Simulation;

// Usage :
//   wh40ksim                                   -> saisie interactive
//   wh40ksim scenario.json [-n 1000000] [--seed 42]

Console.OutputEncoding = Encoding.UTF8;

try
{
    return Run(args);
}
catch (Exception ex) when (ex is ArgumentException or FormatException or IOException or JsonException)
{
    ConsoleInput.WriteError("Erreur : " + ex.Message);
    return 1;
}

static int Run(string[] args)
{
    string? scenarioPath = null;
    int? iterations = null;
    int? seed = null;

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-h":
            case "--help":
                PrintHelp();
                return 0;
            case "-n":
            case "--iterations":
                iterations = ParseIntArgument(args, ++i, "--iterations");
                break;
            case "--seed":
                seed = ParseIntArgument(args, ++i, "--seed");
                break;
            default:
                scenarioPath = args[i];
                break;
        }
    }

    Scenario scenario;
    if (scenarioPath is not null)
    {
        scenario = ScenarioJson.Load(scenarioPath);
    }
    else
    {
        Console.WriteLine("Simulateur de dégâts Warhammer 40.000 (11e édition). Entrée = valeur par défaut entre crochets.");
        scenario = ScenarioWizard.Build();

        ConsoleInput.WriteTitle("Simulation");
        SaveScenario(scenario);
    }

    int n = iterations ?? (scenarioPath is null
        ? ConsoleInput.ReadInt("Nombre de simulations", 1_000_000, 1, 100_000_000)
        : 1_000_000);

    Console.WriteLine($"Simulation en cours ({n:N0} itérations)...");
    var result = Simulator.Run(scenario, n, seed);
    ReportPrinter.Print(result);
    return 0;
}

/// <summary>Enregistre le scénario sous un nom dans le dossier des scénarios, après confirmation s'il existe déjà.</summary>
static void SaveScenario(Scenario scenario)
{
    string directory = ScenariosDirectory();
    while (true)
    {
        string name = ConsoleInput.ReadFileName("Nom du scénario à enregistrer (vide pour ignorer)");
        if (name.Length == 0)
        {
            return;
        }

        string path = Path.Combine(directory, name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? name : name + ".json");
        if (File.Exists(path) && !ConsoleInput.ReadBool($"Le scénario '{Path.GetFileName(path)}' existe déjà. L'écraser", false))
        {
            continue;
        }

        Directory.CreateDirectory(directory);
        ScenarioJson.Save(scenario, path);
        Console.WriteLine($"Scénario sauvegardé dans {path}");
        return;
    }
}

/// <summary>Dossier "scenarios" à la racine de la solution ; à défaut (appli publiée hors du dépôt), dans le dossier courant.</summary>
static string ScenariosDirectory()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Wh40kSim.sln")))
    {
        directory = directory.Parent;
    }
    return Path.Combine(directory?.FullName ?? Directory.GetCurrentDirectory(), "scenarios");
}

static int ParseIntArgument(string[] args, int index, string name)
{
    if (index >= args.Length)
    {
        throw new ArgumentException($"Valeur manquante après {name}.");
    }

    string text = args[index].Replace("_", "").Replace(" ", "");
    if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
    {
        throw new ArgumentException($"Nombre attendu après {name} (reçu : '{args[index]}').");
    }
    return value;
}

static void PrintHelp()
{
    Console.WriteLine("""
        wh40ksim — simulateur de dégâts Warhammer 40.000 (11e édition)

        Usage :
          wh40ksim                         Saisie interactive du scénario
          wh40ksim <scenario.json>         Charge un scénario depuis un fichier JSON

        Options :
          -n, --iterations <N>             Nombre de simulations (défaut : 1 000 000)
          --seed <S>                       Graine aléatoire, pour reproduire exactement un résultat
          -h, --help                       Affiche cette aide
        """);
}
