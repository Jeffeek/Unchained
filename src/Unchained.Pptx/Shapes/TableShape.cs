using Unchained.Pptx.Core;

namespace Unchained.Pptx.Shapes;

/// <summary>
/// A shape that contains a table — a grid of rows and columns with individual cell
/// text and formatting.
/// </summary>
public sealed class TableShape : Shape
{
    /// <summary>
    /// The column and row grid. Access cells via <c>Grid[columnIndex, rowIndex]</c>.
    /// </summary>
    public TableGrid Grid { get; internal set; } = new();

    /// <summary><see langword="true"/> when the first row is styled as a header row.</summary>
    public bool HasHeaderRow { get; set; }

    /// <summary><see langword="true"/> when the last row is styled as a totals row.</summary>
    public bool HasTotalRow { get; set; }

    /// <summary><see langword="true"/> when alternate rows use a banded (striped) style.</summary>
    public bool HasBandedRows { get; set; }

    /// <summary><see langword="true"/> when alternate columns use a banded style.</summary>
    public bool HasBandedColumns { get; set; }

    /// <summary><see langword="true"/> when the first column uses a distinct style.</summary>
    public bool HasFirstColumn { get; set; }

    /// <summary><see langword="true"/> when the last column uses a distinct style.</summary>
    public bool HasLastColumn { get; set; }

    // ── Convenience ──────────────────────────────────────────────────────────

    /// <summary>Gets the cell at the given column and row (both zero-based).</summary>
    public TableCell this[int column, int row] => Grid[column, row];
}
