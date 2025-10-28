using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace PnpExample.Utils;

public class CircleCenterCalculator
{
    // Written by GPT.
    public static Vector<double> GetCcwCenter(Vector<double> p1, Vector<double> p2, double angleDeg)
    {
        var dVec = p2 - p1;
        var d = dVec.L2Norm(); // distance between points

        var angleRad = angleDeg * Math.PI / 180.0;
        var r = d / (2.0 * Math.Sin(angleRad / 2.0));

        // Midpoint of the segment
        var mid = (p1 + p2) / 2.0;

        // Distance from midpoint to circle center
        var h = Math.Sqrt(r * r - Math.Pow(d / 2.0, 2));

        // Perpendicular unit vector
        var perp = DenseVector.OfArray(new[] { -dVec[1], dVec[0] }).Normalize(2);

        // Two possible centers
        var c1 = mid + perp * h;
        var c2 = mid - perp * h;

        // Helper function to check CCW direction
        bool IsCCW(Vector<double> center)
        {
            var v1 = p1 - center;
            var v2 = p2 - center;
            var cross = v1[0] * v2[1] - v1[1] * v2[0];
            return cross > 0; // CCW if positive
        }

        return IsCCW(c1) ? c1 : c2;
    }
}