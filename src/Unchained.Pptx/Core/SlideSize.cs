using Unchained.Ooxml;

namespace Unchained.Pptx.Core;

/// <summary>
///     The size of a presentation slide, stored as <see cref="Emu" /> dimensions.
/// </summary>
/// <remarks>
///     Use the static presets (<see cref="Widescreen" />, <see cref="Standard" />) for common sizes,
///     or <see cref="Custom" /> for arbitrary dimensions.
/// </remarks>
public readonly struct SlideSize : IEquatable<SlideSize>
{
    /// <summary>The width of the slide in EMUs.</summary>
    public Emu Width { get; }

    /// <summary>The height of the slide in EMUs.</summary>
    public Emu Height { get; }

    /// <summary>Initialises a slide size with the given dimensions.</summary>
    public SlideSize(Emu width, Emu height)
    {
        Width = width;
        Height = height;
    }

    // ── Presets ──────────────────────────────────────────────────────────────

    /// <summary>
    ///     Widescreen 16:9 (12,192,000 × 6,858,000 EMU ≈ 13.33 × 7.5 in).
    ///     The default size for PowerPoint presentations since Office 2013.
    /// </summary>
    public static readonly SlideSize Widescreen = new(new Emu(12_192_000), new Emu(6_858_000));

    /// <summary>
    ///     Standard 4:3 (9,144,000 × 6,858,000 EMU ≈ 10 × 7.5 in).
    ///     The legacy default for older presentations.
    /// </summary>
    public static readonly SlideSize Standard = new(new Emu(9_144_000), new Emu(6_858_000));

    /// <summary>A4 portrait (7,560,000 × 10,692,000 EMU ≈ 8.27 × 11.69 in).</summary>
    public static readonly SlideSize A4Portrait = new(new Emu(7_560_000), new Emu(10_692_000));

    /// <summary>A4 landscape (10,692,000 × 7,560,000 EMU ≈ 11.69 × 8.27 in).</summary>
    public static readonly SlideSize A4Landscape = new(new Emu(10_692_000), new Emu(7_560_000));

    /// <summary>Letter portrait (7,772,400 × 10,058,400 EMU ≈ 8.5 × 11 in).</summary>
    public static readonly SlideSize LetterPortrait = new(Emu.FromInches(8.5), Emu.FromInches(11));

    /// <summary>Letter landscape (10,058,400 × 7,772,400 EMU ≈ 11 × 8.5 in).</summary>
    public static readonly SlideSize LetterLandscape = new(Emu.FromInches(11), Emu.FromInches(8.5));

    // ── Factory ──────────────────────────────────────────────────────────────

    /// <summary>Creates a slide size with arbitrary dimensions.</summary>
    public static SlideSize Custom(Emu width, Emu height) => new(width, height);

    // ── Equality ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public bool Equals(SlideSize other) => Width == other.Width && Height == other.Height;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SlideSize other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Width, Height);

    /// <summary>Returns <see langword="true" /> when both sizes have equal dimensions.</summary>
    public static bool operator ==(SlideSize left, SlideSize right) => left.Equals(right);

    /// <summary>Returns <see langword="true" /> when the sizes differ.</summary>
    public static bool operator !=(SlideSize left, SlideSize right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => $"{Width.ToInches():F2}\" × {Height.ToInches():F2}\"";
}
