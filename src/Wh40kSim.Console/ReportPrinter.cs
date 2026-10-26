using Wh40kSim.Core.Engine;
using static Wh40kSim.ConsoleApp.ConsoleInput;

namespace Wh40kSim.ConsoleApp;

/// <summary>Affichage texte des résultats (en attendant les vrais graphiques de la phase 5).</summary>
internal static class ReportPrinter
{
    private const int BarWidth = 40;
    private const double MinRowProbability = 0.0005; // on masque les queues de distribution sous 0,05 %

    public static void Print(SimulationSummary s)
    {
        var scenario = s.Scenario;
        var defender = scenario.Defender;

        WriteTitle($"Résultats : {s.Iterations:N0} simulations en {s.Elapsed.TotalSeconds:F2} s (seed {s.Seed})");

        Console.WriteLine($"Attaquant : {scenario.Attacker.Name}");
        foreach (var group in scenario.Attacker.Weapons)
        {
            Console.WriteLine($"  {group.ModelCount} × {group.Weapon.Describe()}");
        }

        Console.WriteLine($"Défenseur : {defender.Name} (E{defender.Toughness}{(defender.InCover ? ", à couvert" : "")})");
        for (int g = 0; g < defender.Groups.Count; g++)
        {
            Console.WriteLine($"  {g + 1}. {defender.Groups[g].Describe()}");
        }

        Console.WriteLine();
        Console.WriteLine($"Figurines tuées  : moyenne {s.MeanKills:F2} (écart-type {s.StdDevKills:F2}), " +
                          $"médiane {s.PercentileKills(0.5)}, 80 % des cas entre {s.PercentileKills(0.1)} et {s.PercentileKills(0.9)}");
        Console.WriteLine($"PV retirés       : moyenne {s.MeanDamage:F2} (écart-type {s.StdDevDamage:F2}), " +
                          $"médiane {s.PercentileDamage(0.5)}, 80 % des cas entre {s.PercentileDamage(0.1)} et {s.PercentileDamage(0.9)}");
        Console.WriteLine($"Unité détruite   : {s.ProbabilityWipe:P1}");

        Console.WriteLine("Pertes moyennes par groupe :");
        for (int g = 0; g < defender.Groups.Count; g++)
        {
            var group = defender.Groups[g];
            Console.WriteLine($"  {group.Name} : {s.MeanKilledInGroup(g):F2} / {group.Count}");
        }

        PrintDistribution("Figurines tuées", s.KillsHistogram, s.Iterations);
        PrintDistribution("PV retirés", s.DamageHistogram, s.Iterations);
    }

    private static void PrintDistribution(string title, IReadOnlyList<long> histogram, long iterations)
    {
        if (iterations == 0)
        {
            return;
        }

        // P(≥ k) par sommes cumulées depuis la fin
        var atLeast = new double[histogram.Count + 1];
        for (int k = histogram.Count - 1; k >= 0; k--)
        {
            atLeast[k] = atLeast[k + 1] + (double)histogram[k] / iterations;
        }

        int first = 0;
        while (first < histogram.Count - 1 && (double)histogram[first] / iterations < MinRowProbability) first++;
        int last = histogram.Count - 1;
        while (last > first && (double)histogram[last] / iterations < MinRowProbability) last--;

        double max = histogram.Max() / (double)iterations;

        Console.WriteLine();
        Console.WriteLine($"{title} :");
        Console.WriteLine($"{"k",5}  {"P(= k)",8}  {"P(≥ k)",8}");
        for (int k = first; k <= last; k++)
        {
            double p = (double)histogram[k] / iterations;
            int bar = max > 0 ? (int)Math.Round(p / max * BarWidth) : 0;
            Console.WriteLine($"{k,5}  {p,8:P1}  {atLeast[k],8:P1}  {new string('█', bar)}");
        }
    }
}
