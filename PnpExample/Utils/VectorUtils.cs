using MathNet.Numerics.LinearAlgebra.Double;

namespace PnpExample.Utils;

internal class VectorUtils
{
    // Written by Gpt
    public static DenseVector RotateVector(DenseVector v, double angleDegrees, DenseVector center)
    {
        if (v.Count != 2 || center.Count != 2)
            throw new ArgumentException("Only 2D vectors are supported.");

        var angleRadians = angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(angleRadians);
        var sin = Math.Sin(angleRadians);

        // Step 1: Translate point so center is at origin
        var x = v[0] - center[0];
        var y = v[1] - center[1];

        // Step 2: Rotate
        var rotatedX = x * cos - y * sin;
        var rotatedY = x * sin + y * cos;

        // Step 3: Translate back
        return DenseVector.OfArray([
            rotatedX + center[0],
            rotatedY + center[1]
        ]);
    }

    public static double GetDistance((double X, double Y) point1, (double X, double Y) point2, double mmPerPixel = 1)
    {
        var dx = point1.X - point2.X;
        var dy = point1.Y - point2.Y;
        return Math.Sqrt(dx * dx + dy * dy) * mmPerPixel;
    }

    public static double[] GetRectangleAllDistances(double[] px, double[] py, double mmPerPixel)
    {
        var points = px.Zip(py, (x, y) => (X: x, Y: y)).ToList();
        points.Sort((a, b) => a.Y.CompareTo(b.Y));

        var top = points.Take(2).OrderBy(p => p.X).ToArray();
        var bottom = points.Skip(2).OrderBy(p => p.X).ToArray();

        var leftTop = top[0];
        var rightTop = top[1];
        var leftBottom = bottom[0];
        var rightBottom = bottom[1];

        return
        [
            GetDistance(leftTop, rightTop, mmPerPixel),
            GetDistance(rightTop, rightBottom, mmPerPixel),
            GetDistance(rightBottom, leftBottom, mmPerPixel),
            GetDistance(leftBottom, leftTop, mmPerPixel),
            GetDistance(leftTop, rightBottom, mmPerPixel),
            GetDistance(rightTop, leftBottom, mmPerPixel)
        ];
    }
}