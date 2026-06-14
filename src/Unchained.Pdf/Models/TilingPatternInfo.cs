namespace Unchained.Pdf.Models;

/// <summary>
///     A tiling pattern (ISO 32000-1 §8.7.3.1, PatternType 1): a small cell of content painted
///     repeatedly on a lattice to fill a region. The renderer rasterises one cell, then tiles it
///     across the painted area under <see cref="Matrix" />.
/// </summary>
/// <param name="PaintType">
///     1 = coloured (the cell specifies its own colours); 2 = uncoloured (the cell is painted in the
///     current fill colour).
/// </param>
/// <param name="BBox">Cell bounding box <c>[llx lly urx ury]</c> in pattern space.</param>
/// <param name="XStep">Horizontal spacing between cell origins, in pattern space.</param>
/// <param name="YStep">Vertical spacing between cell origins, in pattern space.</param>
/// <param name="Matrix">Pattern matrix mapping pattern space to the default (page) coordinate system.</param>
/// <param name="Operators">The cell's content-stream operators (its own resources are inlined where simple).</param>
public sealed record TilingPatternInfo(
    int PaintType,
    double[] BBox,
    double XStep,
    double YStep,
    double[] Matrix,
    IReadOnlyList<ContentOperator> Operators
);
