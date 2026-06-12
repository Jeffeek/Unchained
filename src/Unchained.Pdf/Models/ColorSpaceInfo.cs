using Unchained.Pdf.Content;

namespace Unchained.Pdf.Models;

/// <summary>
///     A resolved color space from a PDF /Resources /ColorSpace entry.
///     Carries enough information for the renderer to convert component values to sRGB.
///     ISO 32000-1 §8.6.
/// </summary>
internal sealed class ColorSpaceInfo
{
    /// <summary>Canonical color space kind name (e.g. "DeviceRGB", "Separation", "ICCBased").</summary>
    public string Kind { get; init; } = "DeviceRGB";

    /// <summary>
    ///     For Separation and DeviceN: the tint transform function that maps component
    ///     values (0–1 each) to the alternate color space. Null for Device/Cal/ICC spaces.
    /// </summary>
    public PdfFunction? TintTransform { get; init; }

    /// <summary>
    ///     For Separation and DeviceN: the alternate color space name after tint transform
    ///     (e.g. "DeviceCMYK", "DeviceRGB"). Used to interpret the function output.
    /// </summary>
    public string AlternateSpace { get; init; } = "DeviceRGB";

    /// <summary>
    ///     For Indexed: the palette lookup table (one entry per index, BaseChannels bytes each).
    /// </summary>
    public byte[]? IndexedLookup { get; init; }

    /// <summary>For Indexed: bytes per palette entry (1 = Gray, 3 = RGB, 4 = CMYK).</summary>
    public int IndexedBaseChannels { get; init; }

    /// <summary>For Indexed: base color space name used by the palette.</summary>
    public string IndexedBaseSpace { get; init; } = "DeviceRGB";

    /// <summary>For CalRGB: gamma [Gr, Gg, Gb] and XYZ matrix [Mxx…Mzz].</summary>
    public double[]? CalRgbGamma { get; init; }

    /// <summary>For CalRGB: 3×3 column-major XYZ matrix.</summary>
    public double[]? CalRgbMatrix { get; init; }

    /// <summary>For CalGray: gamma value.</summary>
    public double CalGrayGamma { get; init; } = 1.0;

    // ── Convenience factories ────────────────────────────────────────────────

    internal static ColorSpaceInfo Device(string name) => new() { Kind = name };

    internal static ColorSpaceInfo Separation(PdfFunction? fn, string alternate) =>
        new() { Kind = "Separation", TintTransform = fn, AlternateSpace = alternate };

    internal static ColorSpaceInfo DeviceN(PdfFunction? fn, string alternate) =>
        new() { Kind = "DeviceN", TintTransform = fn, AlternateSpace = alternate };

    internal static ColorSpaceInfo Indexed(byte[] lookup, int channels, string baseSpace) =>
        new() { Kind = "Indexed", IndexedLookup = lookup, IndexedBaseChannels = channels, IndexedBaseSpace = baseSpace };

    internal static ColorSpaceInfo IccBased(string alternate) =>
        new() { Kind = "ICCBased", AlternateSpace = alternate };

    internal static ColorSpaceInfo CalRgb(double[]? gamma, double[]? matrix) =>
        new() { Kind = "CalRGB", CalRgbGamma = gamma, CalRgbMatrix = matrix };

    internal static ColorSpaceInfo CalGrayInfo(double gamma) =>
        new() { Kind = "CalGray", CalGrayGamma = gamma };

    internal static ColorSpaceInfo Lab() => new() { Kind = "Lab", AlternateSpace = "DeviceRGB" };

    // ── Conversion ────────────────────────────────────────────────────────────

    /// <summary>
    ///     Converts component values (0–1 range) in this color space to sRGB bytes.
    ///     Falls back to grey 128 when the conversion cannot be performed.
    /// </summary>
    public (byte R, byte G, byte B) ToRgb(double[] components, PdfFunction? overrideFn = null)
    {
        byte B255(double v) => (byte)Math.Clamp((int)Math.Round(v * 255), 0, 255);

        switch (Kind)
        {
            case "DeviceGray":
            case "CalGray":
            {
                var v = components.Length > 0 ? components[0] : 0.5;
                if (Kind == "CalGray" && CalGrayGamma != 1.0)
                    v = Math.Pow(Math.Max(0, v), CalGrayGamma);
                var b = B255(v);
                return (b, b, b);
            }

            case "DeviceRGB":
                return components.Length >= 3
                    ? (B255(components[0]), B255(components[1]), B255(components[2]))
                    : ((byte)128, (byte)128, (byte)128);

            case "DeviceCMYK":
            case "ICCBased" when AlternateSpace == "DeviceCMYK":
            {
                if (components.Length < 4) return (128, 128, 128);
                var c = components[0];
                var m = components[1];
                var y = components[2];
                var k = components[3];
                return (B255((1 - c) * (1 - k)), B255((1 - m) * (1 - k)), B255((1 - y) * (1 - k)));
            }

            case "ICCBased":
            {
                // Use alternate space for conversion.
                var alt = new ColorSpaceInfo { Kind = AlternateSpace };
                return alt.ToRgb(components);
            }

            case "Separation":
            case "DeviceN":
            {
                var fn = overrideFn ?? TintTransform;
                if (fn is null) return (128, 128, 128);
                // Evaluation: single tint value for Separation, multi-component for DeviceN.
                var tint = components.Length > 0 ? components[0] : 0.5;
                var output = fn.Eval(tint);
                var alt = new ColorSpaceInfo { Kind = AlternateSpace };
                return alt.ToRgb(output);
            }

            case "Indexed":
            {
                if (IndexedLookup is null || components.Length == 0) return (128, 128, 128);
                var idx = (int)Math.Clamp(Math.Round(components[0] * 255), 0, (IndexedLookup.Length / Math.Max(1, IndexedBaseChannels)) - 1);
                var offset = idx * IndexedBaseChannels;
                if (offset + IndexedBaseChannels > IndexedLookup.Length) return (128, 128, 128);
                var palette = IndexedLookup.AsSpan(offset, IndexedBaseChannels);
                return IndexedBaseChannels switch
                {
                    1 => (palette[0], palette[0], palette[0]),
                    3 => (palette[0], palette[1], palette[2]),
                    4 => (B255((1 - (palette[0] / 255.0)) * (1 - (palette[3] / 255.0))),
                        B255((1 - (palette[1] / 255.0)) * (1 - (palette[3] / 255.0))),
                        B255((1 - (palette[2] / 255.0)) * (1 - (palette[3] / 255.0)))),
                    _ => (128, 128, 128)
                };
            }

            case "CalRGB":
            {
                if (components.Length < 3) return (128, 128, 128);
                // Apply gamma then matrix to get CIE XYZ, then convert to sRGB.
                var gamma = CalRgbGamma ?? [1.0, 1.0, 1.0];
                var ar = Math.Pow(Math.Max(0, components[0]), gamma.Length > 0 ? gamma[0] : 1.0);
                var ag = Math.Pow(Math.Max(0, components[1]), gamma.Length > 1 ? gamma[1] : 1.0);
                var ab = Math.Pow(Math.Max(0, components[2]), gamma.Length > 2 ? gamma[2] : 1.0);
                double xr, yg, zb;
                if (CalRgbMatrix is { Length: >= 9 } m)
                {
                    xr = (m[0] * ar) + (m[3] * ag) + (m[6] * ab);
                    yg = (m[1] * ar) + (m[4] * ag) + (m[7] * ab);
                    zb = (m[2] * ar) + (m[5] * ag) + (m[8] * ab);
                }
                else
                {
                    xr = ar;
                    yg = ag;
                    zb = ab;
                }

                // D65 XYZ → linear sRGB (IEC 61966-2-1)
                var lr = (3.2404542 * xr) + (-1.5371385 * yg) + (-0.4985314 * zb);
                var lg = (-0.9692660 * xr) + (1.8760108 * yg) + (0.0415560 * zb);
                var lb = (0.0556434 * xr) + (-0.2040259 * yg) + (1.0572252 * zb);

                // Gamma-compress sRGB
                static double Gamma(double v) => v <= 0.0031308 ? 12.92 * v : (1.055 * Math.Pow(v, 1.0 / 2.4)) - 0.055;
                return (B255(Gamma(Math.Max(0, lr))), B255(Gamma(Math.Max(0, lg))), B255(Gamma(Math.Max(0, lb))));
            }

            case "Lab":
            {
                if (components.Length < 3) return (128, 128, 128);
                // L*a*b* → XYZ D50 → linear sRGB (approximate)
                var lStar = components[0];
                var a = components[1];
                var b2 = components[2];
                var fy = (lStar + 16) / 116.0;
                var fx = (a / 500.0) + fy;
                var fz = fy - (b2 / 200.0);
                static double F(double t) => t > 0.206897 ? t * t * t : (t - (16.0 / 116.0)) / 7.787;
                var x = 0.9505 * F(fx);
                var y = 1.0000 * F(fy);
                var z = 1.0890 * F(fz);
                var lr2 = (3.2404542 * x) + (-1.5371385 * y) + (-0.4985314 * z);
                var lg2 = (-0.9692660 * x) + (1.8760108 * y) + (0.0415560 * z);
                var lb2 = (0.0556434 * x) + (-0.2040259 * y) + (1.0572252 * z);
                static double Gamma(double v) => v <= 0.0031308 ? 12.92 * v : (1.055 * Math.Pow(v, 1.0 / 2.4)) - 0.055;
                return (B255(Gamma(Math.Max(0, lr2))), B255(Gamma(Math.Max(0, lg2))), B255(Gamma(Math.Max(0, lb2))));
            }

            default:
                return components.Length >= 3
                    ? (B255(components[0]), B255(components[1]), B255(components[2]))
                    : ((byte)128, (byte)128, (byte)128);
        }
    }
}
