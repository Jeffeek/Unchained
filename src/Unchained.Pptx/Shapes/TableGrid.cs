using Unchained.Ooxml;

namespace Unchained.Pptx.Shapes;

/// <summary>
///     Holds the column width and row height definitions of a <see cref="TableShape" />,
///     and provides indexed access to its <see cref="TableCell" /> objects.
/// </summary>
public sealed class TableGrid
{
    private readonly List<Emu> _columnWidths = [];
    private readonly List<Emu> _rowHeights = [];
    private readonly List<List<TableCell>> _rows = [];

    // ── Dimensions ───────────────────────────────────────────────────────────

    /// <summary>The number of columns in the table.</summary>
    public int ColumnCount => _columnWidths.Count;

    /// <summary>The number of rows in the table.</summary>
    public int RowCount => _rowHeights.Count;

    /// <summary>The width of each column, indexed from 0.</summary>
    public IReadOnlyList<Emu> ColumnWidths => _columnWidths;

    /// <summary>The height of each row, indexed from 0.</summary>
    public IReadOnlyList<Emu> RowHeights => _rowHeights;

    // ── Cell access ──────────────────────────────────────────────────────────

    /// <summary>
    ///     Returns the cell at the given column and row (both zero-based).
    /// </summary>
    public TableCell this[int column, int row] => _rows[row][column];

    // ── Construction (internal) ───────────────────────────────────────────────

    /// <summary>Initialises the grid with the given column widths and row heights.</summary>
    internal static TableGrid Create(Emu[] columnWidths, Emu[] rowHeights)
    {
        ArgumentNullException.ThrowIfNull(columnWidths);
        ArgumentNullException.ThrowIfNull(rowHeights);

        var grid = new TableGrid();
        grid._columnWidths.AddRange(columnWidths);
        grid._rowHeights.AddRange(rowHeights);

        foreach (var row in rowHeights.Select(_ => columnWidths.Select(static _ => new TableCell()).ToList()))
            grid._rows.Add(row);

        return grid;
    }

    /// <summary>Appends a row with the given height and returns its newly-created cells.</summary>
    public void AddRow(Emu height)
    {
        _rowHeights.Add(height);
        var row = new List<TableCell>();
        for (var i = 0; i < _columnWidths.Count; i++)
            row.Add(new TableCell());
        _rows.Add(row);
    }

    /// <summary>
    ///     Inserts a row of empty cells at <paramref name="rowIndex" /> with the given height
    ///     and returns the new cells.
    /// </summary>
    public void InsertRow(int rowIndex, Emu height)
    {
        if (rowIndex < 0 || rowIndex > _rowHeights.Count)
            throw new ArgumentOutOfRangeException(nameof(rowIndex));

        _rowHeights.Insert(rowIndex, height);
        var row = new List<TableCell>();
        for (var i = 0; i < _columnWidths.Count; i++)
            row.Add(new TableCell());
        _rows.Insert(rowIndex, row);
    }

    /// <summary>Appends a column with the given width.</summary>
    public void AddColumn(Emu width)
    {
        _columnWidths.Add(width);
        foreach (var row in _rows)
            row.Add(new TableCell());
    }

    /// <summary>Inserts a column of empty cells at <paramref name="columnIndex" />.</summary>
    public void InsertColumn(int columnIndex, Emu width)
    {
        if (columnIndex < 0 || columnIndex > _columnWidths.Count)
            throw new ArgumentOutOfRangeException(nameof(columnIndex));

        _columnWidths.Insert(columnIndex, width);
        foreach (var row in _rows)
            row.Insert(columnIndex, new TableCell());
    }

    /// <summary>Removes the row at <paramref name="rowIndex" />.</summary>
    public void RemoveRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _rowHeights.Count)
            throw new ArgumentOutOfRangeException(nameof(rowIndex));

        _rowHeights.RemoveAt(rowIndex);
        _rows.RemoveAt(rowIndex);
    }

    /// <summary>Removes the column at <paramref name="columnIndex" />.</summary>
    public void RemoveColumn(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _columnWidths.Count)
            throw new ArgumentOutOfRangeException(nameof(columnIndex));

        _columnWidths.RemoveAt(columnIndex);
        foreach (var row in _rows.Where(row => columnIndex < row.Count))
            row.RemoveAt(columnIndex);
    }

    /// <summary>Adds a cell to an existing row (used by the parser).</summary>
    internal void AddCell(int rowIndex, TableCell cell) => _rows[rowIndex].Add(cell);

    /// <summary>Adds a complete row of cells (used by the parser).</summary>
    internal void AddRowWithCells(Emu height, IEnumerable<TableCell> cells)
    {
        _rowHeights.Add(height);
        _rows.Add(cells.ToList());
    }

    /// <summary>Adds a column width entry (used by the parser).</summary>
    internal void AddColumnWidth(Emu width) => _columnWidths.Add(width);
}
