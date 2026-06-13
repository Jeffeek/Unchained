namespace Unchained.Drawing;

/// <summary>
///     Pure 2D affine-matrix arithmetic shared by the rendering and document assemblies.
///     Matrices are row-major 6-element arrays <c>[a b c d e f]</c> (PDF / PostScript convention,
///     ISO 32000-1 §8.3.3). No image or PDF types — just scalar math — so it lives in Drawing.
/// </summary>
internal static class Matrix2D
{
    /// <summary>The identity matrix.</summary>
    internal static double[] Identity() => [1, 0, 0, 1, 0, 0];

    /// <summary>A pure-translation matrix by (<paramref name="tx" />, <paramref name="ty" />).</summary>
    internal static double[] Translate(double tx, double ty) => [1, 0, 0, 1, tx, ty];

    /// <summary>
    ///     Concatenates two matrices: the result applies <paramref name="m1" /> first, then
    ///     <paramref name="m2" /> (i.e. <c>m1 × m2</c>).
    /// </summary>
    internal static double[] Multiply(double[] m1, double[] m2) =>
        // [a1 b1 0]   [a2 b2 0]
        // [c1 d1 0] × [c2 d2 0]
        // [e1 f1 1]   [e2 f2 1]
        [
            (m1[0] * m2[0]) + (m1[1] * m2[2]),
            (m1[0] * m2[1]) + (m1[1] * m2[3]),
            (m1[2] * m2[0]) + (m1[3] * m2[2]),
            (m1[2] * m2[1]) + (m1[3] * m2[3]),
            (m1[4] * m2[0]) + (m1[5] * m2[2]) + m2[4],
            (m1[4] * m2[1]) + (m1[5] * m2[3]) + m2[5]
        ];

    /// <summary>Transforms a point through matrix <paramref name="m" />.</summary>
    internal static (double X, double Y) Transform(
        double[] m,
        double x,
        double y
    ) => ((m[0] * x) + (m[2] * y) + m[4], (m[1] * x) + (m[3] * y) + m[5]);
}
