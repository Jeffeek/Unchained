namespace Unchained.Pdf.Models;

/// <summary>
///     Numbering style for a page label range (ISO 32000-1 §12.4.2 Table 159).
/// </summary>
public enum PageLabelStyle
{
    /// <summary>Decimal Arabic numerals: 1, 2, 3, …</summary>
    Decimal,
    /// <summary>Uppercase Roman numerals: I, II, III, …</summary>
    RomanUpper,
    /// <summary>Lowercase Roman numerals: i, ii, iii, …</summary>
    RomanLower,
    /// <summary>Uppercase letters: A, B, C, …, AA, AB, …</summary>
    AlphaUpper,
    /// <summary>Lowercase letters: a, b, c, …, aa, ab, …</summary>
    AlphaLower,
    /// <summary>No numbering — page label is the prefix string only (or empty).</summary>
    None
}

/// <summary>
///     Defines a contiguous range of pages that share the same numbering style and prefix.
///     Ranges are applied in ascending <see cref="StartPageIndex" /> order.
/// </summary>
/// <param name="StartPageIndex">
///     Zero-based index of the first page in this range.
///     The first range must start at 0.
/// </param>
/// <param name="Style">Numbering style for pages in this range.</param>
/// <param name="Prefix">
///     Optional prefix string prepended to every label in this range (e.g. <c>"A-"</c>).
///     Pass <see langword="null" /> or an empty string for no prefix.
/// </param>
/// <param name="FirstLabelNumber">
///     The logical page number assigned to the first page of this range.
///     Defaults to 1. Use a higher value to continue numbering from a prior section.
/// </param>
public sealed record PageLabelRange(
    int StartPageIndex,
    PageLabelStyle Style = PageLabelStyle.Decimal,
    string? Prefix = null,
    int FirstLabelNumber = 1
);
