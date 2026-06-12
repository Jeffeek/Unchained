using Unchained.Pdf.Content;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Pre-computed geometry for a table. All fields are in PDF user space points (1 pt = 1/72 inch).
///     Computed once before any content stream operators are emitted.
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

    /// <summary>
    ///     Computes layout geometry. When <paramref name="data" /> is provided, column widths are
    ///     proportional to the widest content in each column (using Standard 14 font AFM metrics).
    ///     When <paramref name="data" /> is <see langword="null" />, columns are distributed equally.
    /// </summary>
    internal static TableLayout Compute(
        int columnCount,
        TableStyle style,
        bool hasTitle,
        TableData? data = null
    )
    {
        const float usableWidth = PageWidth - (2 * Margin);
        const float usableHeight = PageHeight - (2 * Margin);
        var rowH = (2 * style.CellPaddingPt) + style.CellFontSize;
        var headerH = (2 * style.CellPaddingPt) + style.HeaderFontSize;
        var titleH = hasTitle ? style.HeaderFontSize + (2 * style.CellPaddingPt) : 0f;
        var rowsPerPage = Math.Max(1, (int)((usableHeight - titleH - headerH) / rowH));

        float[] cols;
        if (data is not null && columnCount > 0)
            cols = ComputeProportionalWidths(data, style, usableWidth);
        else
        {
            cols = new float[columnCount];
            Array.Fill(cols, usableWidth / columnCount);
        }

        // ReSharper disable once BadListLineBreaks
        return new TableLayout(cols,
            rowH,
            headerH,
            titleH,
            usableWidth,
            rowsPerPage);
    }

    // Measures each column's required width (widest header or cell text + padding),
    // then scales proportionally so the total fits usableWidth.
    private static float[] ComputeProportionalWidths(
        TableData data,
        TableStyle style,
        float usableWidth
    )
    {
        var count = data.Headers.Count;
        var widths = new float[count];
        var boldFont = style.FontName + "-Bold";

        for (var c = 0; c < count; c++)
        {
            // Header uses bold font at headerFontSize
            widths[c] = MeasureText(data.Headers[c], boldFont, style.HeaderFontSize) + (2 * style.CellPaddingPt);

            // All data rows use regular font at cellFontSize
            foreach (var cellW in from row in data.Rows
                                  // ReSharper disable AccessToModifiedClosure
                                  where c < row.Count
                                  select MeasureText(row[c], style.FontName, style.CellFontSize) + (2 * style.CellPaddingPt)
                                  into cellW
                                  where cellW > widths[c]
                                  // ReSharper restore AccessToModifiedClosure
                                  select cellW)
                widths[c] = cellW;
        }

        // Scale down proportionally if content exceeds usable width.
        var total = widths.Sum();
        if (total > usableWidth)
        {
            var scale = usableWidth / total;
            for (var i = 0; i < widths.Length; i++)
                widths[i] *= scale;
        }
        else if (total < usableWidth)
        {
            // Distribute remaining space evenly so table fills the full width.
            var extra = (usableWidth - total) / count;
            for (var i = 0; i < widths.Length; i++)
                widths[i] += extra;
        }

        return widths;
    }

    // Computes the advance width of a string in points using Standard 14 AFM metrics.
    private static float MeasureText(string text, string fontName, float fontSize) =>
        text.Sum(ch => Standard14Widths.Get(fontName, ch) / 1000f * fontSize);
}
