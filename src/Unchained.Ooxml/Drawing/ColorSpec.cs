using Unchained.Drawing.Primitives;

namespace Unchained.Ooxml.Drawing;

/// <summary>
///     A colour value that is either an absolute ARGB colour or a reference to a named
///     slot in the presentation's theme colour scheme, with optional luminance transforms.
/// </summary>
/// <remarks>
///     Use <see cref="FromRgb" />, <see cref="FromArgb" />, or <see cref="FromTheme" /> to
///     construct a value. Call <see cref="Resolve" /> to obtain a concrete ARGB value after
///     applying any luminance modifiers.
/// </remarks>
public readonly struct ColorSpec : IEquatable<ColorSpec>
{
    private ColorSpec(
        ColorSpecType type,
        uint rgb,
        ThemeColorSlot themeSlot,
        double luminanceModifier,
        double luminanceOffset
    )
    {
        Type = type;
        Rgb = rgb;
        ThemeSlot = themeSlot;
        LuminanceModifier = luminanceModifier;
        LuminanceOffset = luminanceOffset;
    }

    // ── Properties ───────────────────────────────────────────────────────────

    /// <summary>
    ///     Indicates whether this colour is an absolute RGB value or a theme slot reference.
    /// </summary>
    public ColorSpecType Type { get; }

    /// <summary>
    ///     The raw ARGB value (0xAARRGGBB) when <see cref="Type" /> is <see cref="ColorSpecType.Rgb" />;
    ///     otherwise 0.
    /// </summary>
    public uint Rgb { get; }

    /// <summary>
    ///     The theme colour slot when <see cref="Type" /> is <see cref="ColorSpecType.ThemeSlot" />.
    /// </summary>
    public ThemeColorSlot ThemeSlot { get; }

    /// <summary>
    ///     A multiplier (0.0–1.0) applied to the luminance after resolving the base colour.
    ///     1.0 means no change.
    /// </summary>
    public double LuminanceModifier { get; }

    /// <summary>
    ///     An additive offset (0.0–1.0) applied to the luminance after applying <see cref="LuminanceModifier" />.
    ///     0.0 means no offset.
    /// </summary>
    public double LuminanceOffset { get; }

    // ── Factory ──────────────────────────────────────────────────────────────

    /// <summary>Creates a fully opaque RGB colour.</summary>
    /// <param name="red">Red channel (0–255).</param>
    /// <param name="green">Green channel (0–255).</param>
    /// <param name="blue">Blue channel (0–255).</param>
    public static ColorSpec FromRgb(byte red, byte green, byte blue) =>
        new(
            ColorSpecType.Rgb,
            0xFF000000 | ((uint)red << 16) | ((uint)green << 8) | blue,
            default,
            1.0,
            0.0
        );

    /// <summary>Creates an ARGB colour with an explicit alpha (opacity) channel.</summary>
    /// <param name="alpha">Alpha channel (0 = fully transparent, 255 = fully opaque).</param>
    /// <param name="red">Red channel (0–255).</param>
    /// <param name="green">Green channel (0–255).</param>
    /// <param name="blue">Blue channel (0–255).</param>
    public static ColorSpec FromArgb(
        byte alpha,
        byte red,
        byte green,
        byte blue
    ) =>
        new(
            ColorSpecType.Rgb,
            ((uint)alpha << 24) | ((uint)red << 16) | ((uint)green << 8) | blue,
            default,
            1.0,
            0.0
        );

    /// <summary>
    ///     Creates a colour that references a theme colour slot, with optional luminance transforms.
    /// </summary>
    /// <param name="slot">The theme colour slot.</param>
    /// <param name="luminanceModifier">
    ///     Multiplier applied to the luminance of the resolved colour (0.0–1.0). Default 1.0 = no change.
    /// </param>
    /// <param name="luminanceOffset">
    ///     Additive offset applied to the luminance after the modifier (0.0–1.0). Default 0.0 = no offset.
    /// </param>
    public static ColorSpec FromTheme(
        ThemeColorSlot slot,
        double luminanceModifier = 1.0,
        double luminanceOffset = 0.0
    ) =>
        new(ColorSpecType.ThemeSlot, 0, slot, luminanceModifier, luminanceOffset);

    // ── Resolution ───────────────────────────────────────────────────────────

    private const double DoubleComparisonTolerance = 0.05;

    /// <summary>
    ///     Resolves this colour to a concrete ARGB value by looking up the theme slot (if applicable)
    ///     and applying luminance transforms.
    /// </summary>
    /// <param name="scheme">
    ///     The colour scheme to use when resolving theme slot references.
    ///     Pass <see langword="null" /> to fall back to a neutral grey for unresolved theme colours.
    /// </param>
    /// <returns>A 32-bit ARGB value (0xAARRGGBB).</returns>
    public uint Resolve(ColorScheme? scheme)
    {
        uint baseArgb;

        if (Type == ColorSpecType.Rgb)
            baseArgb = Rgb;
        else
            baseArgb = scheme?.Resolve(ThemeSlot) ?? OoxmlScaling.UnresolvedThemeColorArgb;

        return Math.Abs(LuminanceModifier - 1.0) < DoubleComparisonTolerance && LuminanceOffset == 0.0
            ? baseArgb
            : ApplyLuminanceTransform(baseArgb, LuminanceModifier, LuminanceOffset);
    }

    // ── Equality ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public bool Equals(ColorSpec other) =>
        Type == other.Type &&
        Rgb == other.Rgb &&
        ThemeSlot == other.ThemeSlot &&
        Math.Abs(LuminanceModifier - other.LuminanceModifier) < DoubleComparisonTolerance &&
        Math.Abs(LuminanceOffset - other.LuminanceOffset) < DoubleComparisonTolerance;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ColorSpec other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(Type, Rgb, ThemeSlot, LuminanceModifier, LuminanceOffset);

    /// <summary>Returns <see langword="true" /> when both colour specs are identical.</summary>
    public static bool operator ==(ColorSpec left, ColorSpec right) => left.Equals(right);

    /// <summary>Returns <see langword="true" /> when the colour specs differ.</summary>
    public static bool operator !=(ColorSpec left, ColorSpec right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() =>
        Type == ColorSpecType.Rgb
            ? $"#{Rgb:X8}"
            : $"Theme:{ThemeSlot} mod={LuminanceModifier} off={LuminanceOffset}";

    // ── Luminance transform ───────────────────────────────────────────────────

    private static uint ApplyLuminanceTransform(uint argb, double modifier, double offset)
    {
        var (alpha, red, green, blue) = ColorMath.UnpackArgb(argb);

        RgbToHls(
            red / 255.0,
            green / 255.0,
            blue / 255.0,
            out var hue,
            out var luminance,
            out var saturation
        );

        luminance = Math.Clamp((luminance * modifier) + offset, 0.0, 1.0);

        HlsToRgb(
            hue,
            luminance,
            saturation,
            out var r,
            out var g,
            out var b
        );

        return ((uint)alpha << 24) |
               ((uint)Math.Round(r * 255) << 16) |
               ((uint)Math.Round(g * 255) << 8) |
               (uint)Math.Round(b * 255);
    }

    private static void RgbToHls(
        double r,
        double g,
        double b,
        out double hue,
        out double luminance,
        out double saturation
    )
    {
        var maximum = Math.Max(r, Math.Max(g, b));
        var minimum = Math.Min(r, Math.Min(g, b));
        var delta = maximum - minimum;

        luminance = (maximum + minimum) / 2.0;

        if (delta == 0.0)
        {
            hue = 0.0;
            saturation = 0.0;
            return;
        }

        saturation = luminance < 0.5
            ? delta / (maximum + minimum)
            : delta / (2.0 - maximum - minimum);

        if (Math.Abs(maximum - r) < DoubleComparisonTolerance)
            hue = (g - b) / delta % 6.0;
        else if (Math.Abs(maximum - g) < DoubleComparisonTolerance)
            hue = ((b - r) / delta) + 2.0;
        else
            hue = ((r - g) / delta) + 4.0;

        hue /= 6.0;
        if (hue < 0) hue += 1.0;
    }

    private static void HlsToRgb(
        double hue,
        double luminance,
        double saturation,
        out double r,
        out double g,
        out double b
    )
    {
        if (saturation == 0.0)
        {
            r = g = b = luminance;
            return;
        }

        var q = luminance < 0.5
            ? luminance * (1.0 + saturation)
            : luminance + saturation - (luminance * saturation);
        var p = (2.0 * luminance) - q;

        r = HueToRgbChannel(p, q, hue + (1.0 / 3.0));
        g = HueToRgbChannel(p, q, hue);
        b = HueToRgbChannel(p, q, hue - (1.0 / 3.0));
    }

    private static double HueToRgbChannel(double p, double q, double t)
    {
        if (t < 0) t += 1.0;
        if (t > 1) t -= 1.0;

        return t switch
        {
            < 1.0 / 6.0 => p + ((q - p) * 6.0 * t),
            < 1.0 / 2.0 => q,
            < 2.0 / 3.0 => p + ((q - p) * ((2.0 / 3.0) - t) * 6.0),
            _ => p
        };
    }
}
