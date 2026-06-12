namespace Unchained.Pdf.Engine;

/// <summary>Font and TrueType metric constants.</summary>
internal static class FontConstants
{
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
