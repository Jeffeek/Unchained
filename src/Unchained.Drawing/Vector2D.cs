namespace Unchained.Drawing;

/// <summary>
///     Pure 2D vector arithmetic shared by the rendering assemblies. No image or PDF types —
///     just <see langword="double" /> math, so it can live in the Drawing layer.
/// </summary>
internal static class Vector2D
{
    /// <summary>Euclidean length of the vector (<paramref name="dx" />, <paramref name="dy" />).</summary>
    internal static double Magnitude(double dx, double dy) => Math.Sqrt((dx * dx) + (dy * dy));

    /// <summary>Euclidean distance between two points.</summary>
    internal static double Distance(
        double x1,
        double y1,
        double x2,
        double y2
    ) => Magnitude(x2 - x1, y2 - y1);
}
