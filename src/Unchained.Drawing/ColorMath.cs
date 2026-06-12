namespace Unchained.Drawing;

/// <summary>
///     Pure colour-component arithmetic shared across the rendering and document assemblies.
///     No image or PDF types — just scalar math — so it belongs in the Drawing layer.
/// </summary>
internal static class ColorMath
{
    /// <summary>
    ///     Naive (non-ICC) DeviceCMYK → DeviceRGB conversion (ISO 32000-1 §8.6.4.4), returning
    ///     components in the [0,1] range. Callers apply their own [0,255] byte conversion so the
    ///     existing rounding/truncation behaviour at each call site is preserved.
    /// </summary>
    internal static (double R, double G, double B) CmykToRgb(
        double c,
        double m,
        double y,
        double k
    ) => ((1 - c) * (1 - k), (1 - m) * (1 - k), (1 - y) * (1 - k));

    /// <summary>
    ///     Maps a colour component in [0,1] to an 8-bit channel value, rounding to nearest.
    ///     Note: this is the rounding variant; renderer fill colours use a separate truncating
    ///     conversion and must not be routed through here.
    /// </summary>
    internal static byte ToByteRounded(double value) =>
        (byte)Math.Clamp((int)Math.Round(value * 255), 0, 255);
}
