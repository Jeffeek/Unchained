namespace Unchained.Pdf.Models;

/// <summary>
/// Visual styling applied when rendering a <see cref="TableData"/> into a PDF page
/// via <see cref="ITableGenerator"/>. All font sizes and padding are in PDF points (1 pt = 1/72 inch).
/// </summary>
public sealed record TableStyle(
    /// <summary>
    /// The name of the font used for all table text.
    /// Must be one of the PDF Standard 14 fonts (e.g. <c>Helvetica</c>, <c>Times-Roman</c>)
    /// or the name of an embedded font available in the document.
    /// Defaults to <c>Helvetica</c>.
    /// </summary>
    string FontName = "Helvetica",

    /// <summary>Font size in points for header cells. Defaults to 10.</summary>
    float HeaderFontSize = 10f,

    /// <summary>Font size in points for data cells. Defaults to 9.</summary>
    float CellFontSize = 9f,

    /// <summary>
    /// Padding in points applied inside each cell on all four sides.
    /// Larger values increase row height. Defaults to 4.
    /// </summary>
    float CellPaddingPt = 4f,

    /// <summary>
    /// When <see langword="true"/>, odd and even data rows are rendered with
    /// alternating background colours to improve readability.
    /// </summary>
    bool AlternatingRowColor = true,

    /// <summary>
    /// When <see langword="true"/>, a 1-point border is drawn around each cell.
    /// </summary>
    bool DrawBorders = true
)
{
    /// <summary>Default style: Helvetica 9/10, 4 pt padding, alternating rows, borders.</summary>
    public static readonly TableStyle Default = new();

    /// <summary>
    /// Compact style: smaller fonts and tighter padding for dense data tables.
    /// </summary>
    public static readonly TableStyle Compact = new(
        HeaderFontSize: 8f,
        CellFontSize: 7f,
        CellPaddingPt: 2f);
}
