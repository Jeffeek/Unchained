using Unchained.Pdf.Core;

namespace Unchained.Pdf.Models;

/// <summary>
/// Describes a Type3 font (ISO 32000-1 §9.6.5). Each glyph is defined as a content
/// stream (PDF operators) in the <c>/CharProcs</c> dictionary. The glyph is rendered
/// by executing those operators within a local coordinate system established by the
/// <c>/FontMatrix</c>.
/// </summary>
internal sealed class Type3FontInfo
{
    /// <summary>
    /// Font matrix [a b c d e f] that maps glyph space (1000 units = 1 glyph unit)
    /// to text space. Typically [0.001 0 0 0.001 0 0].
    /// </summary>
    internal double[] FontMatrix { get; init; } = [0.001, 0, 0, 0.001, 0, 0];

    /// <summary>
    /// Maps each char code (0–255) to a glyph name (e.g. "A", "space", "uni0041").
    /// Null entries mean the char code has no glyph.
    /// </summary>
    internal string?[] Encoding { get; init; } = new string?[256];

    /// <summary>
    /// Maps glyph name → parsed content operators for that glyph's drawing program.
    /// </summary>
    internal IReadOnlyDictionary<string, IReadOnlyList<ContentOperator>> CharProcs { get; init; }
        = new Dictionary<string, IReadOnlyList<ContentOperator>>();

    /// <summary>Width of each glyph in glyph space, indexed by char code − FirstChar.</summary>
    internal double[] Widths { get; init; } = [];

    /// <summary>First char code covered by <see cref="Widths"/>.</summary>
    internal int FirstChar { get; init; }
}
