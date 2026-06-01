using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
/// Pre-computed geometry for a table. All fields are in PDF user space points (1 pt = 1/72 inch).
/// Computed once before any content stream operators are emitted.
/// </summary>
internal readonly struct TableLayout
{
    internal const float PageWidth = 595f;
    internal const float PageHeight = 842f;
    internal const float Margin = 36f;

    internal float[] ColumnWidths { get; }
    internal float RowHeight { get; }
    internal float HeaderRowHeight { get; }
    internal float TitleHeight { get; }
    internal float TableWidth { get; }
    internal int RowsPerPage { get; }

    private TableLayout(
        float[] columnWidths,
        float rowHeight,
        float headerRowHeight,
        float titleHeight,
        float tableWidth,
        int rowsPerPage
    )
    {
        ColumnWidths = columnWidths;
        RowHeight = rowHeight;
        HeaderRowHeight = headerRowHeight;
        TitleHeight = titleHeight;
        TableWidth = tableWidth;
        RowsPerPage = rowsPerPage;
    }

    internal static TableLayout Compute(int columnCount, TableStyle style, bool hasTitle)
    {
        const float usableWidth = PageWidth - (2 * Margin);
        const float usableHeight = PageHeight - (2 * Margin);
        var colWidth = usableWidth / columnCount;
        var rowH = (2 * style.CellPaddingPt) + style.CellFontSize;
        var headerH = (2 * style.CellPaddingPt) + style.HeaderFontSize;
        var titleH = hasTitle ? style.HeaderFontSize + (2 * style.CellPaddingPt) : 0f;
        var rowsPerPage = Math.Max(1, (int)((usableHeight - titleH - headerH) / rowH));
        var cols = new float[columnCount];
        Array.Fill(cols, colWidth);

        return new TableLayout(
            cols,
            rowH,
            headerH,
            titleH,
            usableWidth,
            rowsPerPage
        );
    }
}
