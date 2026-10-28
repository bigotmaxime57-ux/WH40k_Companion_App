using Wh40kSim.Simulation;
using Wh40kSim.Simulation.Statistics;
using static Wh40kSim.ConsoleApp.ConsoleInput;

namespace Wh40kSim.ConsoleApp;

/// <summary>
/// Affichage texte des résultats (en attendant les graphiques de la phase 3). Mise en forme uniquement :
/// tous les chiffres viennent de <see cref="SimulationResult"/>.
/// </summary>
internal static class ReportPrinter
{
    private const int BarWidth = 40;

    public static void Print(SimulationResult result)
    {
        var scenario = result.Scenario;
        var defender = scenario.Defender;

        WriteTitle($"Résultats : {result.Iterations:N0} simulations en {result.Elapsed.TotalSeconds:F2} s (seed {result.Seed})");

        Console.WriteLine($"Attaquant : {scenario.Attacker.Name}");
        foreach (var group in scenario.Attacker.Weapons)
        {
            Console.WriteLine($"  {group.ModelCount} × {group.Weapon.Describe()}");
        }

        Console.WriteLine($"Défenseur : {defender.Name}{(defender.Boosts.InCover ? " (à couvert)" : "")}");
        for (int g = 0; g < defender.Groups.Count; g++)
        {
            Console.WriteLine($"  {g + 1}. {defender.Groups[g].Describe()}");
        }

        Console.WriteLine();
        Console.WriteLine($"Figurines tuées  : {Summarize(result.Kills)}");
        Console.WriteLine($"PV retirés       : {Summarize(result.Damage)}");
        Console.WriteLine($"Unité détruite   : {result.ProbabilityWipe:P1}");
        if (result.MeanPointsDestroyed is double destroyed)
        {
            string efficiency = result.Efficiency is double e ? $", rendement {e:F2} ({scenario.Attacker.Points} pts engagés)" : "";
            Console.WriteLine($"Points détruits  : {destroyed:F1} / {defender.TotalPoints} pts (au prorata des PV){efficiency}");
        }

        Console.WriteLine("Pertes moyennes par groupe :");
        for (int g = 0; g < defender.Groups.Count; g++)
        {
            var group = defender.Groups[g];
            Console.WriteLine($"  {group.Name} : {result.MeanKilledInGroup(g):F2} / {group.Count} fig., {result.MeanDamageInGroup(g):F2} / {group.Count * group.Wounds} PV");
        }

        PrintDistribution("Figurines tuées", result.Kills);
        PrintDistribution("PV retirés", result.Damage);

        if (result.ProbabilityWipe > 0)
        {
            PrintExcess(result);
        }

        PrintRawDamage(result);
    }

    /// <summary>Dégâts bruts (sans plafond par figurine) et part perdue sur des figurines déjà achevées.</summary>
    private static void PrintRawDamage(SimulationResult result)
    {
        WriteTitle("Dégâts bruts");
        Console.WriteLine("Dégâts de chaque attaque non sauvegardée comptés en entier, sans plafond par figurine ni arrêt à la destruction :");
        Console.WriteLine($"Dégâts bruts     : {Summarize(result.RawDamage)}");
        Console.WriteLine($"Gaspillage       : {result.MeanWastedDamage:F2} en moyenne, soit {result.WastedShare:P1} des dégâts bruts " +
                          "(dégâts au-delà des PV de la figurine touchée, marge non comprise)");
    }

    /// <summary>Distributions prolongées au-delà de la destruction de l'unité : la marge de l'attaquant.</summary>
    private static void PrintExcess(SimulationResult result)
    {
        WriteTitle("Au-delà de l'unité (excédent)");
        Console.WriteLine("Après la destruction de l'unité, les attaques restantes continuent contre des figurines virtuelles :");
        Console.WriteLine($"  {(result.ExcessProfile with { Count = 1 }).Describe()}");
        Console.WriteLine();
        Console.WriteLine($"Marge quand l'unité est détruite : +{result.KillMarginWhenWiped:F2} figurine(s), " +
                          $"+{result.DamageMarginWhenWiped:F2} PV en moyenne");
        Console.WriteLine();
        Console.WriteLine($"Figurines tuées  : {Summarize(result.ExtendedKills)}");
        Console.WriteLine($"PV retirés       : {Summarize(result.ExtendedDamage)}");

        PrintDistribution("Figurines tuées (prolongé)", result.ExtendedKills);
        PrintDistribution("PV retirés (prolongé)", result.ExtendedDamage);
    }

    private static string Summarize(DiscreteDistribution distribution)
    {
        var range = distribution.CentralRange;
        return $"moyenne {distribution.Mean:F2} (écart-type {distribution.StdDev:F2}), médiane {distribution.Median}, " +
               $"80 % des cas entre {range.Low} et {range.High}";
    }

    private static void PrintDistribution(string title, DiscreteDistribution distribution)
    {
        var rows = distribution.Rows();
        double max = rows.Max(r => r.Exactly);

        Console.WriteLine();
        Console.WriteLine($"{title} :");
        Console.WriteLine($"{"k",5}  {"P(= k)",8}  {"P(≥ k)",8}");
        foreach (var row in rows)
        {
            int bar = max > 0 ? (int)Math.Round(row.Exactly / max * BarWidth) : 0;
            Console.WriteLine($"{row.Value,5}  {row.Exactly,8:P1}  {row.AtLeast,8:P1}  {new string('█', bar)}");
        }
    }
}
