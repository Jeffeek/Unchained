using Unchained.Ooxml;

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

    /// <summary>
    /// Merges the rectangular block of cells spanning from <paramref name="firstColumn"/>,
    /// <paramref name="firstRow"/> to <paramref name="lastColumn"/>, <paramref name="lastRow"/>
    /// (all zero-based, inclusive). The top-left (anchor) cell receives the combined
    /// <see cref="TableCell.ColumnSpan"/>/<see cref="TableCell.RowSpan"/>; every other cell in
    /// the block is flagged as a merge continuation (the OOXML <c>hMerge</c>/<c>vMerge</c> model).
    /// </summary>
    public void MergeCells(int firstColumn, int firstRow, int lastColumn, int lastRow)
    {
        var c0 = Math.Min(firstColumn, lastColumn);
        var c1 = Math.Max(firstColumn, lastColumn);
        var r0 = Math.Min(firstRow, lastRow);
        var r1 = Math.Max(firstRow, lastRow);

        if (c0 < 0 || r0 < 0 || c1 >= Grid.ColumnCount || r1 >= Grid.RowCount)
            throw new ArgumentOutOfRangeException(nameof(firstColumn), "Merge range is outside the table.");
        if (c0 == c1 && r0 == r1)
            return; // single cell — nothing to merge

        var anchor = Grid[c0, r0];
        anchor.ColumnSpan = (c1 - c0) + 1;
        anchor.RowSpan = (r1 - r0) + 1;

        for (var r = r0; r <= r1; r++)
        for (var c = c0; c <= c1; c++)
        {
            if (c == c0 && r == r0) continue;
            var cell = Grid[c, r];
            // A cell to the right of the anchor in the same row continues horizontally; a cell
            // below continues vertically. Cells in the interior continue both ways.
            if (c > c0) cell.IsHorizontalMergeContinuation = true;
            if (r > r0) cell.IsVerticalMergeContinuation = true;
        }
    }

    /// <summary>Merges the two given cells' bounding block. Convenience over the index overload.</summary>
    public void MergeCells(TableCell first, TableCell second)
    {
        var a = Locate(first) ?? throw new ArgumentException("Cell is not part of this table.", nameof(first));
        var b = Locate(second) ?? throw new ArgumentException("Cell is not part of this table.", nameof(second));
        MergeCells(a.Column, a.Row, b.Column, b.Row);
    }

    private (int Column, int Row)? Locate(TableCell target)
    {
        for (var r = 0; r < Grid.RowCount; r++)
        for (var c = 0; c < Grid.ColumnCount; c++)
            if (ReferenceEquals(Grid[c, r], target))
                return (c, r);
        return null;
    }
}
