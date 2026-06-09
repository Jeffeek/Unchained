namespace Unchained.Pdf.Models;

/// <summary>
/// A decoded axial (type 2) or radial (type 3) shading, ready for a renderer to paint
/// without re-evaluating PDF functions. The colour ramp is pre-sampled into
/// <see cref="ColorRamp"/> (256 RGB entries spanning the shading's domain); the renderer
/// only computes the parametric value <c>t ∈ [0,1]</c> per pixel from the geometry and looks
/// up the colour. See ISO 32000-1 §8.7.4.5.
/// </summary>
/// <param name="ShadingType">2 = axial, 3 = radial.</param>
/// <param name="Coords">
/// Axial: <c>[x0 y0 x1 y1]</c> (the gradient axis). Radial: <c>[x0 y0 r0 x1 y1 r1]</c>
/// (two circles). User-space coordinates.
/// </param>
/// <param name="ExtendStart">Whether to extend the shading beyond the start (t&lt;0).</param>
/// <param name="ExtendEnd">Whether to extend the shading beyond the end (t&gt;1).</param>
/// <param name="ColorRamp">256 RGB triples (768 bytes) sampling the colour from t=0 to t=1.</param>
public sealed record ShadingInfo(
    int ShadingType,
    double[] Coords,
    bool ExtendStart,
    bool ExtendEnd,
    byte[] ColorRamp
)
{
    /// <summary>Returns the RGB colour at parametric position <paramref name="t"/> (0–1).</summary>
    public (byte R, byte G, byte B) ColorAt(double t)
    {
        var idx = (int)Math.Clamp(Math.Round(t * 255), 0, 255);
        var o = idx * 3;
        return (ColorRamp[o], ColorRamp[o + 1], ColorRamp[o + 2]);
    }
}
