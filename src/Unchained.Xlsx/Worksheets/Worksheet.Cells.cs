using System.Globalization;
using Unchained.Xlsx.Cell;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Parsing;

namespace Unchained.Xlsx.Worksheets;

public sealed partial class Worksheet
{
    /// <summary>The materialised rows of this sheet, in row order.</summary>
    public RowCollection Rows
    {
        get
        {
            EnsureCellsParsed();
            return RowsInternal;
        }
    }

    /// <summary>
    ///     Returns the cell at the given position, materialising it (and its row) on demand. Use this
    ///     for fluent read/write; the cell exists in the model immediately even before a value is set.
    /// </summary>
    public Cell.Cell this[int row, int column]
    {
        get
        {
            EnsureCellsParsed();
            return RowsInternal.GetOrCreateRow(row).GetOrAddCell(this, column);
        }
    }

    /// <summary>Returns the cell at the given A1 reference, materialising it on demand.</summary>
    public Cell.Cell this[string a1]
    {
        get
        {
            var reference = CellReference.FromA1(a1);
            return this[reference.Row, reference.Column];
        }
    }

    /// <summary>Returns the cell at the given reference, materialising it on demand.</summary>
    public Cell.Cell this[CellReference reference] => this[reference.Row, reference.Column];

    // ── Writer accessors ────────────────────────────────────────────────────────

    /// <summary>
    ///     <see langword="true" /> once cells have been materialised. When false, the writer keeps the
    ///     raw <c>&lt;sheetData&gt;</c> as-is rather than regenerating it.
    /// </summary>
    internal bool CellsMaterialised { get; private set; }

    /// <summary><see langword="true" /> once columns have been materialised.</summary>
    internal bool ColumnsMaterialised { get; private set; }

    internal RowCollection RowsInternal { get; } = new();

    internal ColumnCollection ColumnsInternal { get; } = new();

    // ── Cell access ─────────────────────────────────────────────────────────────

    /// <summary>Returns the cell at the given 1-based row and column, or <see langword="null" /> if empty.</summary>
    public Cell.Cell? GetCell(int row, int column) => GetCell(new CellReference(row, column));

    /// <summary>Returns the cell at <paramref name="reference" />, or <see langword="null" /> if empty.</summary>
    public Cell.Cell? GetCell(CellReference reference)
    {
        EnsureCellsParsed();
        return RowsInternal.GetRow(reference.Row)?.GetCell(reference.Column);
    }

    /// <summary>Returns the materialised row with the given number, or <see langword="null" /> if absent.</summary>
    public Row? GetRow(int rowNumber)
    {
        EnsureCellsParsed();
        return RowsInternal.GetRow(rowNumber);
    }

    // ── Used range ───────────────────────────────────────────────────────────

    /// <summary>
    ///     Returns the bounding range of all non-empty cells, or <see langword="null" /> when the
    ///     sheet holds no data.
    /// </summary>
    public CellRange? GetUsedRange()
    {
        EnsureCellsParsed();

        int minRow = int.MaxValue, minCol = int.MaxValue, maxRow = 0, maxCol = 0;
        var any = false;

        foreach (var cell in RowsInternal.AllRows.SelectMany(static row => row.CellsInternal.Where(static cell => !cell.IsEffectivelyEmpty)))
        {
            any = true;
            minRow = Math.Min(minRow, cell.Row);
            maxRow = Math.Max(maxRow, cell.Row);
            minCol = Math.Min(minCol, cell.Column);
            maxCol = Math.Max(maxCol, cell.Column);
        }

        return any
            ? CellRange.FromBounds(minRow, minCol, maxRow, maxCol)
            : null;
    }

    /// <summary>Returns the worksheet contents as tab-separated rows, newline-separated (debugging aid).</summary>
    public string GetAllText()
    {
        var used = GetUsedRange();
        if (used is null)
            return string.Empty;

        var lines = new List<string>(used.Value.RowCount);
        for (var r = used.Value.TopLeft.Row; r <= used.Value.BottomRight.Row; r++)
        {
            var fields = new List<string>(used.Value.ColumnCount);
            for (var c = used.Value.TopLeft.Column; c <= used.Value.BottomRight.Column; c++)
            {
                var cell = GetCell(r, c);
                fields.Add(cell?.GetString() ?? cell?.GetDouble()?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            }

            lines.Add(string.Join('\t', fields));
        }

        return string.Join('\n', lines);
    }

    // ── Lazy cell parsing ───────────────────────────────────────────────────────

    private void EnsureCellsParsed()
    {
        if (CellsMaterialised)
            return;

        CellsMaterialised = true;
        if (RawElement != null)
            WorksheetParser.ParseCells(this, RawElement, RowsInternal);
    }
}
