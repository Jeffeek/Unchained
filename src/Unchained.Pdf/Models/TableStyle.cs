namespace Unchained.Pdf.Models;

/// <summary>
///     Visual styling applied when rendering a <see cref="TableData" /> into a PDF page.
///     All font sizes and padding values are in PDF points (1 pt = 1/72 inch).
/// </summary>
/// <param name="FontName">The name of the font used for all table text.</param>
/// <param name="HeaderFontSize">Font size in points for header cells.</param>
/// <param name="CellFontSize">Font size in points for data cells.</param>
/// <param name="CellPaddingPt">Padding in points applied inside each cell.</param>
/// <param name="AlternatingRowColor">
///     When <see langword="true" />, odd and even data rows are rendered with alternating
///     background colours.
/// </param>
/// <param name="DrawBorders">
///     When <see langword="true" />, a 0.5-point border is drawn around each cell.
/// </param>
public sealed record TableStyle(
    string FontName = "Helvetica",
    float HeaderFontSize = 10f,
    float CellFontSize = 9f,
    float CellPaddingPt = 4f,
    bool AlternatingRowColor = true,
    bool DrawBorders = true
)
{
    /// <summary>Default style: Helvetica 9/10, 4 pt padding, alternating rows, borders.</summary>
    public static readonly TableStyle Default = new();

    /// <summary>Compact style: smaller fonts and tighter padding for dense data tables.</summary>
    public static readonly TableStyle Compact = new(
        HeaderFontSize: 8f,
        CellFontSize: 7f,
        CellPaddingPt: 2f);
}
