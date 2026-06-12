namespace Unchained.Pdf.Engine;

/// <summary>Font and TrueType metric constants.</summary>
internal static class FontConstants
{
    /// <summary>
    ///     Target units-per-em after TrueType metric normalisation.
    ///     All widths and bounding-box values are scaled to a 1000-unit em square (PDF convention).
    /// </summary>
    internal const int NormalizedUnitsPerEm = 1000;

    /// <summary><see cref="NormalizedUnitsPerEm" /> as <see langword="double" /> for calculations.</summary>
    internal const double NormalizedUnitsPerEmDouble = 1000.0;

    /// <summary>
    ///     Estimated cap-height as a fraction of the ascender when the OS/2 table is absent.
    ///     Approximately 72% of ascent is a widely-used typographic approximation.
    /// </summary>
    internal const double CapHeightAscentRatio = 0.72;

    /// <summary>
    ///     Default FontMatrix scale factor for Type3 fonts (= 1/1000).
    ///     Produces the matrix [0.001 0 0 0.001 0 0] when the font omits /FontMatrix
    ///     (ISO 32000-1 §9.6.5).
    /// </summary>
    internal const double Type3DefaultMatrixScale = 0.001;

    /// <summary>
    ///     Maximum number of CIDs in a single CIDToGIDMap range.
    ///     Guards against malformed /W arrays that specify excessively large ranges.
    /// </summary>
    internal const int MaxCidRangeSize = 65536;
}
