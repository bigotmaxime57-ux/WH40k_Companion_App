using System.Globalization;
using System.Text;

namespace Wh40kSim.UI.Components;

/// <summary>
/// Courbe lissée pour les graphiques SVG : interpolation cubique monotone (Fritsch-Carlson). Elle passe exactement par
/// chaque point et ne crée ni bosse ni creux entre deux points : une suite décroissante (« au moins k ») reste
/// décroissante. Géométrie de dessin seulement, aucune règle de jeu.
/// </summary>
public static class MonotoneCurve
{
    /// <summary>
    /// Chemin SVG (attribut <c>d</c>) passant par <paramref name="points"/>, abscisses strictement croissantes :
    /// « M » puis un segment de Bézier cubique « C » par intervalle. Un seul point : « M » seul ; aucun : chaîne vide.
    /// </summary>
    public static string ToSvgPath(IReadOnlyList<(double X, double Y)> points)
    {
        int n = points.Count;
        if (n == 0)
        {
            return "";
        }

        var path = new StringBuilder($"M{F(points[0].X)},{F(points[0].Y)}");
        if (n == 1)
        {
            return path.ToString();
        }

        double[] tangents = Tangents(points);
        for (int i = 0; i < n - 1; i++)
        {
            var (x0, y0) = points[i];
            var (x1, y1) = points[i + 1];
            double third = (x1 - x0) / 3;
            path.Append($" C{F(x0 + third)},{F(y0 + tangents[i] * third)} {F(x1 - third)},{F(y1 - tangents[i + 1] * third)} {F(x1)},{F(y1)}");
        }
        return path.ToString();
    }

    /// <summary>Pente de la courbe en chaque point, bornée pour garder la monotonie de chaque intervalle.</summary>
    private static double[] Tangents(IReadOnlyList<(double X, double Y)> points)
    {
        int n = points.Count;
        var slopes = new double[n - 1]; // pente de chaque intervalle
        for (int i = 0; i < n - 1; i++)
        {
            slopes[i] = (points[i + 1].Y - points[i].Y) / (points[i + 1].X - points[i].X);
        }

        var tangents = new double[n];
        tangents[0] = slopes[0];
        tangents[n - 1] = slopes[n - 2];
        for (int i = 1; i < n - 1; i++)
        {
            // Changement de sens (ou palier) : tangente horizontale, sinon moyenne des deux pentes voisines
            tangents[i] = slopes[i - 1] * slopes[i] <= 0 ? 0 : (slopes[i - 1] + slopes[i]) / 2;
        }

        for (int i = 0; i < n - 1; i++)
        {
            if (slopes[i] == 0)
            {
                tangents[i] = tangents[i + 1] = 0;
                continue;
            }
            double a = tangents[i] / slopes[i], b = tangents[i + 1] / slopes[i];
            double norm = a * a + b * b;
            if (norm > 9)
            {
                // Tangentes trop raides : la courbe dépasserait ses points ; on les ramène dans la zone monotone
                double t = 3 / Math.Sqrt(norm);
                tangents[i] = t * a * slopes[i];
                tangents[i + 1] = t * b * slopes[i];
            }
        }
        return tangents;
    }

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
