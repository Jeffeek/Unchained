namespace Unchained.Pdf.Models;

/// <summary>
/// A single Gouraud-shaded triangle in device-independent user space: three vertices, each
/// with an RGB colour. Mesh shadings (types 4/5/6/7) are decoded into a list of these.
/// </summary>
/// <param name="X0">Vertex 0 X (user space).</param>
/// <param name="Y0">Vertex 0 Y.</param>
/// <param name="R0">Vertex 0 red (0–255).</param><param name="G0">Vertex 0 green.</param><param name="B0">Vertex 0 blue.</param>
/// <param name="X1">Vertex 1 X.</param><param name="Y1">Vertex 1 Y.</param>
/// <param name="R1">Vertex 1 red.</param><param name="G1">Vertex 1 green.</param><param name="B1">Vertex 1 blue.</param>
/// <param name="X2">Vertex 2 X.</param><param name="Y2">Vertex 2 Y.</param>
/// <param name="R2">Vertex 2 red.</param><param name="G2">Vertex 2 green.</param><param name="B2">Vertex 2 blue.</param>
public sealed record ShadingTriangle(
    double X0, double Y0, byte R0, byte G0, byte B0,
    double X1, double Y1, byte R1, byte G1, byte B1,
    double X2, double Y2, byte R2, byte G2, byte B2
);

/// <summary>
/// A decoded shading. Axial (type 2) / radial (type 3) shadings carry a parametric colour
/// ramp painted from the geometry; mesh shadings (type 4/5/6/7) are decoded into
/// <see cref="Triangles"/> with per-vertex colours and Gouraud-interpolated by the renderer.
/// See ISO 32000-1 §8.7.4.5.
/// </summary>
/// <param name="ShadingType">2 = axial, 3 = radial, 4/5/6/7 = mesh.</param>
/// <param name="Coords">
/// Axial: <c>[x0 y0 x1 y1]</c>. Radial: <c>[x0 y0 r0 x1 y1 r1]</c>. Empty for mesh shadings.
/// </param>
/// <param name="ExtendStart">Extend before the start (axial/radial only).</param>
/// <param name="ExtendEnd">Extend past the end (axial/radial only).</param>
/// <param name="ColorRamp">256 RGB triples sampling t=0→1 (axial/radial only).</param>
/// <param name="Triangles">Decoded mesh triangles (mesh shadings only), else empty.</param>
public sealed record ShadingInfo(
    int ShadingType,
    double[] Coords,
    bool ExtendStart,
    bool ExtendEnd,
    byte[] ColorRamp,
    IReadOnlyList<ShadingTriangle>? Triangles = null
)
{
    /// <summary><see langword="true"/> for mesh shadings (types 4–7).</summary>
    public bool IsMesh => ShadingType is 4 or 5 or 6 or 7;

    /// <summary>Returns the RGB colour at parametric position <paramref name="t"/> (0–1).</summary>
    public (byte R, byte G, byte B) ColorAt(double t)
    {
        var idx = (int)Math.Clamp(Math.Round(t * 255), 0, 255);
        var o = idx * 3;
        return (ColorRamp[o], ColorRamp[o + 1], ColorRamp[o + 2]);
    }
}
