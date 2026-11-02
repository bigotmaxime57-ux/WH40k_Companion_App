using Wh40kSim.Simulation.Statistics;

namespace Wh40kSim.Simulation;

/// <summary>
/// Score d'efficacité d'un attaquant contre une unité, de 0 (rien fait) à 100 (unité toujours détruite) : critère du
/// classement de la comparaison. Calculé à chaque simulation, 100 × (0,4 × part des PV retirés + 0,3 × part des
/// figurines tuées + 0,3 × unité détruite), poids fixes (décision de l'utilisateur) ; <see cref="Mean"/> est sa moyenne.
/// </summary>
/// <param name="Mean">Score moyen.</param>
/// <param name="StandardError">Erreur type de <see cref="Mean"/> (bruit de la simulation), exacte.</param>
/// <param name="DamagePart">Part du score due aux PV retirés (0 à 40).</param>
/// <param name="KillsPart">Part due aux figurines tuées (0 à 30).</param>
/// <param name="WipePart">Part due à la destruction de l'unité (0 à 30).</param>
public sealed record EfficiencyScore(double Mean, double StandardError, double DamagePart, double KillsPart, double WipePart)
{
    public const double DamageWeight = 0.4;
    public const double KillsWeight = 0.3;
    public const double WipeWeight = 0.3;

    /// <summary>
    /// Score tiré des distributions des figurines tuées et des PV retirés (leur maximum est l'effectif et les PV de
    /// l'unité) et du produit figurines × PV cumulé, seul lien entre les deux qui manque pour l'erreur type. L'unité est
    /// détruite exactement quand toutes ses figurines sont tuées, c'est-à-dire quand tous ses PV sont retirés.
    /// </summary>
    public static EfficiencyScore Compute(DiscreteDistribution kills, DiscreteDistribution damage, long killsTimesDamage)
    {
        ArgumentNullException.ThrowIfNull(kills);
        ArgumentNullException.ThrowIfNull(damage);
        double models = kills.MaxValue, wounds = damage.MaxValue, iterations = kills.Total;

        // x = part des PV retirés, y = part des figurines tuées, z = unité détruite (0 ou 1, de moyenne p)
        double meanX = damage.Mean / wounds, meanY = kills.Mean / models;
        double p = kills.ProbabilityAtLeast(kills.MaxValue);
        double varX = Square(damage.StdDev / wounds), varY = Square(kills.StdDev / models), varZ = p * (1 - p);
        double covXY = killsTimesDamage / (iterations * models * wounds) - meanX * meanY;
        // z = 1 seulement quand x = y = 1 : E[xz] = E[yz] = p
        double covXZ = p * (1 - meanX), covYZ = p * (1 - meanY);

        const double a = DamageWeight, b = KillsWeight, c = WipeWeight;
        double variance = a * a * varX + b * b * varY + c * c * varZ + 2 * (a * b * covXY + a * c * covXZ + b * c * covYZ);
        double damagePart = 100 * a * meanX, killsPart = 100 * b * meanY, wipePart = 100 * c * p;
        return new EfficiencyScore(
            damagePart + killsPart + wipePart,
            100 * Math.Sqrt(Math.Max(variance, 0) / iterations),
            damagePart, killsPart, wipePart);
    }

    private static double Square(double value) => value * value;
}
