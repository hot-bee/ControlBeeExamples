namespace PnpExample.Utils;

// Written by GPT.
public static class Calibration
{
    /// <summary>
    ///     Returns % output for a given gram value using piecewise-linear interpolation.
    ///     points: list of (Gram, Percent). At least 2, with strictly increasing Gram.
    ///     If clamp=true, values outside the range return the nearest endpoint's %.
    ///     If clamp=false, they are linearly extrapolated.
    /// </summary>
    public static double PercentFromGram(
        double gram,
        IEnumerable<(double Gram, double Percent)> points,
        bool clamp = true)
    {
        if (points == null) throw new ArgumentNullException(nameof(points));

        // Sort by gram just in case
        var pts = points.OrderBy(p => p.Gram).ToArray();
        if (pts.Length < 2) throw new ArgumentException("Need at least two points.", nameof(points));

        // Validate monotonic grams and no duplicates
        for (var i = 1; i < pts.Length; i++)
            if (pts[i].Gram <= pts[i - 1].Gram)
                throw new ArgumentException("Gram values must be strictly increasing.");

        // Handle out-of-range
        if (gram <= pts.First().Gram)
            return clamp ? pts.First().Percent : Lerp(pts[0], pts[1], gram);
        if (gram >= pts.Last().Gram)
            return clamp ? pts.Last().Percent : Lerp(pts[^2], pts[^1], gram);

        // Find bracketing segment (binary search)
        int lo = 0, hi = pts.Length - 1;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) / 2;
            if (gram >= pts[mid].Gram) lo = mid;
            else hi = mid;
        }

        return Lerp(pts[lo], pts[hi], gram);

        static double Lerp((double Gram, double Percent) a, (double Gram, double Percent) b, double g)
        {
            var dx = b.Gram - a.Gram;
            if (dx == 0) throw new DivideByZeroException("Duplicate gram points.");
            var t = (g - a.Gram) / dx;
            return a.Percent + t * (b.Percent - a.Percent);
        }
    }
}