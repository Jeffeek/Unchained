namespace Unchained.Ooxml;

/// <summary>
///     A measurement unit used throughout ECMA-376 (OOXML) for positions, sizes, and spacing.
///     One inch equals 914,400 EMUs; one centimetre equals 360,000 EMUs.
/// </summary>
/// <remarks>
///     All shape coordinates and dimensions in a presentation are stored as integer EMU values.
///     Use the static factory methods to convert from familiar units without losing precision.
/// </remarks>
public readonly struct Emu : IEquatable<Emu>, IComparable<Emu>
{
    /// <summary>The raw EMU value.</summary>
    public long Value { get; }

    /// <summary>Initialises an <see cref="Emu" /> with the given raw value.</summary>
    public Emu(long value) => Value = value;

    /// <summary>Zero EMUs.</summary>
    public static readonly Emu Zero = new(0);

    // ── Conversion constants ────────────────────────────────────────────────

    /// <summary>EMUs per inch (914 400).</summary>
    public const long EmusPerInch = 914_400;

    /// <summary>EMUs per centimetre (360 000).</summary>
    public const long EmusPerCentimetre = 360_000;

    /// <summary>EMUs per typographic point (12 700).</summary>
    public const long EmusPerPoint = 12_700;

    /// <summary>EMUs per CSS/screen pixel at 96 DPI (9 525).</summary>
    public const long EmusPerCssPixel96Dpi = 9_525;

    // ── Conversion helpers (inverse of factory methods) ─────────────────────

    /// <summary>Multiplier to convert EMU to inches.</summary>
    public const double EmuToInch = 1.0 / EmusPerInch;

    /// <summary>Multiplier to convert EMU to centimetres.</summary>
    public const double EmuToCentimetre = 1.0 / EmusPerCentimetre;

    /// <summary>Multiplier to convert EMU to points.</summary>
    public const double EmuToPoints = 1.0 / EmusPerPoint;

    /// <summary>Multiplier to convert EMU to CSS pixels at 96 DPI.</summary>
    public const double EmuToCssPx = 1.0 / EmusPerCssPixel96Dpi;

    // ── Factory methods ─────────────────────────────────────────────────────

    /// <summary>Creates an <see cref="Emu" /> from a measurement in inches.</summary>
    public static Emu FromInches(double inches) => new((long)(inches * EmusPerInch));

    /// <summary>Creates an <see cref="Emu" /> from a measurement in centimetres.</summary>
    public static Emu FromCentimetres(double centimetres) => new((long)(centimetres * EmusPerCentimetre));

    /// <summary>Creates an <see cref="Emu" /> from a measurement in typographic points (1/72 inch).</summary>
    public static Emu FromPoints(double points) => new((long)(points * EmusPerPoint));

    /// <summary>
    ///     Creates an <see cref="Emu" /> from a pixel count at the given screen DPI.
    /// </summary>
    /// <param name="pixels">Pixel count.</param>
    /// <param name="dpi">Dots (pixels) per inch of the target display. Use 96 for standard screens.</param>
    public static Emu FromPixels(double pixels, double dpi) => new((long)(pixels * EmusPerInch / dpi));

    // ── Conversion back ─────────────────────────────────────────────────────

    /// <summary>Returns the value in inches.</summary>
    public double ToInches() => Value / (double)EmusPerInch;

    /// <summary>Returns the value in centimetres.</summary>
    public double ToCentimetres() => Value / (double)EmusPerCentimetre;

    /// <summary>Returns the value in typographic points.</summary>
    public double ToPoints() => Value / (double)EmusPerPoint;

    /// <summary>Returns the value in pixels at the given DPI.</summary>
    public double ToPixels(double dpi) => Value * dpi / EmusPerInch;

    // ── Arithmetic operators ────────────────────────────────────────────────

    /// <summary>Adds two <see cref="Emu" /> values.</summary>
    public static Emu operator +(Emu left, Emu right) => new(left.Value + right.Value);

    /// <summary>Subtracts one <see cref="Emu" /> from another.</summary>
    public static Emu operator -(Emu left, Emu right) => new(left.Value - right.Value);

    /// <summary>Scales an <see cref="Emu" /> by a scalar factor.</summary>
    public static Emu operator *(Emu emu, double factor) => new((long)(emu.Value * factor));

    /// <summary>Scales an <see cref="Emu" /> by a scalar factor.</summary>
    public static Emu operator *(double factor, Emu emu) => new((long)(emu.Value * factor));

    // ── Comparison operators ────────────────────────────────────────────────

    /// <inheritdoc />
    public bool Equals(Emu other) => Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Emu other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc />
    public int CompareTo(Emu other) => Value.CompareTo(other.Value);

    /// <summary>Returns <see langword="true" /> when both values are equal.</summary>
    public static bool operator ==(Emu left, Emu right) => left.Equals(right);

    /// <summary>Returns <see langword="true" /> when the values differ.</summary>
    public static bool operator !=(Emu left, Emu right) => !left.Equals(right);

    /// <summary>Returns <see langword="true" /> when <paramref name="left" /> is less than <paramref name="right" />.</summary>
    public static bool operator <(Emu left, Emu right) => left.Value < right.Value;

    /// <summary>Returns <see langword="true" /> when <paramref name="left" /> is greater than <paramref name="right" />.</summary>
    public static bool operator >(Emu left, Emu right) => left.Value > right.Value;

    /// <inheritdoc />
    public override string ToString() => $"{Value} EMU";
}
