namespace Wh40kSim.Simulation.Statistics;

/// <summary>Une ligne de tableau de distribution : P(X = k) et P(X ≥ k).</summary>
public readonly record struct DistributionRow(int Value, double Exactly, double AtLeast);

/// <summary>Intervalle de valeurs entières, bornes incluses.</summary>
public readonly record struct IntRange(int Low, int High);

/// <summary>
/// Distribution d'une variable entière positive (figurines tuées, PV retirés…), construite depuis un histogramme :
/// <c>counts[k]</c> = nombre de simulations où la variable vaut exactement k. L'histogramme contient toute
/// l'information : moyenne, centiles et probabilités s'en déduisent exactement. Immuable.
/// </summary>
public sealed class DiscreteDistribution
{
    /// <summary>Seuil sous lequel une ligne aux extrémités du tableau est jugée négligeable (0,05 %).</summary>
    public const double DefaultMinRowProbability = 0.0005;

    /// <summary>Centiles bornant <see cref="CentralRange"/> : du 1er au 9e décile.</summary>
    /// <remarks>Constantes écrites telles quelles : les déduire de 80 % (1 - 0,8) / 2 introduirait une erreur
    /// d'arrondi qui décale la borne haute quand un décile tombe pile sur une valeur.</remarks>
    public const double CentralRangeLow = 0.1;

    /// <inheritdoc cref="CentralRangeLow"/>
    public const double CentralRangeHigh = 0.9;

    private readonly long[] _counts;
    private readonly long[] _countsAtLeast; // _countsAtLeast[k] = nombre de simulations où la variable vaut au moins k

    /// <exception cref="ArgumentException">Si l'histogramme est vide, ne compte aucune simulation ou contient un nombre négatif.</exception>
    public DiscreteDistribution(IReadOnlyList<long> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        if (counts.Count == 0 || counts.Any(c => c < 0))
        {
            throw new ArgumentException("L'histogramme doit contenir au moins une valeur, sans nombre négatif.", nameof(counts));
        }

        _counts = counts.ToArray();
        _countsAtLeast = new long[_counts.Length + 1];
        for (int k = _counts.Length - 1; k >= 0; k--)
        {
            _countsAtLeast[k] = _countsAtLeast[k + 1] + _counts[k];
        }

        Total = _countsAtLeast[0];
        if (Total == 0)
        {
            throw new ArgumentException("L'histogramme ne compte aucune simulation.", nameof(counts));
        }

        Mean = ComputeMean();
        StdDev = ComputeStdDev(Mean);
    }

    /// <summary>Histogramme d'origine.</summary>
    public IReadOnlyList<long> Counts => _counts;

    /// <summary>Nombre total de simulations.</summary>
    public long Total { get; }

    /// <summary>
    /// Plus grande valeur de l'histogramme (sa taille - 1), atteinte ou non : pour les figurines tuées, l'effectif de
    /// l'unité ; pour une distribution prolongée, la plus grande valeur observée.
    /// </summary>
    public int MaxValue => _counts.Length - 1;

    public double Mean { get; }

    public double StdDev { get; }

    public int Median => Percentile(0.5);

    /// <summary>Fourchette du 1er au 9e décile : « 80 % des cas entre Low et High ».</summary>
    public IntRange CentralRange => new(Percentile(CentralRangeLow), Percentile(CentralRangeHigh));

    public double ProbabilityExactly(int value) =>
        value < 0 || value > MaxValue ? 0 : (double)_counts[value] / Total;

    public double ProbabilityAtLeast(int value)
    {
        if (value <= 0) return 1;
        if (value > MaxValue) return 0;
        return (double)_countsAtLeast[value] / Total;
    }

    /// <summary>
    /// Dépassement moyen au-delà d'un seuil, sachant qu'il est atteint : E[X - seuil | X ≥ seuil]
    /// (0 si le seuil n'est jamais atteint). Pour une distribution prolongée, avec l'effectif de l'unité comme seuil :
    /// la marge moyenne quand l'unité est détruite.
    /// </summary>
    public double MeanExcessAbove(int threshold)
    {
        long reached = 0;
        double excess = 0;
        for (int k = Math.Max(threshold, 0); k < _counts.Length; k++)
        {
            reached += _counts[k];
            excess += (double)(k - threshold) * _counts[k];
        }
        return reached == 0 ? 0 : excess / reached;
    }

    /// <summary>Plus petite valeur k telle que P(X ≤ k) ≥ p (p entre 0 et 1, 0,5 = médiane).</summary>
    public int Percentile(double p)
    {
        double threshold = p * Total;
        long cumulative = 0;
        for (int k = 0; k < _counts.Length; k++)
        {
            cumulative += _counts[k];
            if (cumulative >= threshold) return k;
        }
        return MaxValue;
    }

    /// <summary>
    /// Lignes du tableau P(= k) / P(≥ k), sans les lignes négligeables aux extrémités (probabilité inférieure à
    /// <paramref name="minProbability"/>) : au début, P(= k) ; à la fin, P(≥ k). Une queue étalée sur plusieurs valeurs
    /// chacune négligeable (ex. les cas « unité détruite » répartis sur la marge) reste donc visible tant qu'on a une
    /// chance réelle de l'atteindre. Les lignes négligeables situées entre deux lignes affichées sont gardées ; il reste
    /// toujours au moins une ligne.
    /// </summary>
    public IReadOnlyList<DistributionRow> Rows(double minProbability = DefaultMinRowProbability)
    {
        int first = 0;
        while (first < MaxValue && ProbabilityExactly(first) < minProbability) first++;
        int last = MaxValue;
        while (last > first && ProbabilityAtLeast(last) < minProbability) last--;

        var rows = new DistributionRow[last - first + 1];
        for (int k = first; k <= last; k++)
        {
            rows[k - first] = new DistributionRow(k, ProbabilityExactly(k), ProbabilityAtLeast(k));
        }
        return rows;
    }

    private double ComputeMean()
    {
        double sum = 0;
        for (int k = 0; k < _counts.Length; k++) sum += (double)k * _counts[k];
        return sum / Total;
    }

    private double ComputeStdDev(double mean)
    {
        double sum = 0;
        for (int k = 0; k < _counts.Length; k++) sum += (k - mean) * (k - mean) * _counts[k];
        return Math.Sqrt(sum / Total);
    }
}
