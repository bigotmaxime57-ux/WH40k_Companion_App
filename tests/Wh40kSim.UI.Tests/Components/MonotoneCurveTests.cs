using System.Globalization;
using System.Text.RegularExpressions;
using Wh40kSim.UI.Components;

namespace Wh40kSim.UI.Tests.Components;

/// <summary>Courbe lissée monotone : passe par chaque point, sans bosse entre deux points.</summary>
public class MonotoneCurveTests
{
    /// <summary>Nombres du chemin SVG, dans l'ordre : M x,y puis, par segment, deux points de contrôle et l'arrivée.</summary>
    private static double[] Numbers(string path) =>
        Regex.Matches(path, @"-?\d+(\.\d+)?").Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();

    [Fact]
    public void Empty_and_single_point_paths()
    {
        Assert.Equal("", MonotoneCurve.ToSvgPath([]));
        Assert.Equal("M10,20", MonotoneCurve.ToSvgPath([(10, 20)]));
    }

    [Fact]
    public void The_curve_goes_through_every_point()
    {
        (double X, double Y)[] points = [(0, 10), (10, 30), (20, 35), (30, 80)];

        double[] n = Numbers(MonotoneCurve.ToSvgPath(points));

        Assert.Equal((0, 10), (n[0], n[1]));
        for (int segment = 0; segment < points.Length - 1; segment++)
        {
            int end = 2 + segment * 6 + 4;
            Assert.Equal(points[segment + 1], (n[end], n[end + 1]));
        }
    }

    [Fact]
    public void Control_points_stay_between_their_two_ends_so_a_decreasing_series_never_goes_back_up()
    {
        // Suite décroissante avec paliers et chutes brusques, comme une courbe « au moins k » (y SVG croissant vers le bas)
        (double X, double Y)[] points = [(0, 10), (10, 10.5), (20, 60), (30, 61), (40, 200), (50, 200), (60, 240)];

        double[] n = Numbers(MonotoneCurve.ToSvgPath(points));

        for (int segment = 0; segment < points.Length - 1; segment++)
        {
            double low = points[segment].Y, high = points[segment + 1].Y;
            int c = 2 + segment * 6;
            Assert.InRange(n[c + 1], low, high);
            Assert.InRange(n[c + 3], low, high);
        }
    }
}
