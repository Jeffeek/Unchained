using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.Cell;

/// <summary>
///     A row within a <see cref="Worksheet" />. Rows are materialised only when they hold cells or
///     carry explicit properties (custom height, hidden, outline level, row-level style).
/// </summary>
public sealed class Row
{
    private readonly SortedDictionary<int, Cell> _cells = [];

    internal Row(int rowNumber) => RowNumber = rowNumber;

    /// <summary>The 1-based row number.</summary>
    public int RowNumber { get; }

    /// <summary>The row height in points, or <see langword="null" /> to use the sheet default.</summary>
    public double? Height { get; set; }

    /// <summary><see langword="true" /> when the row has an explicit (non-default) height.</summary>
    public bool IsCustomHeight { get; set; }

    /// <summary><see langword="true" /> when the row is hidden.</summary>
    public bool IsHidden { get; set; }

    /// <summary><see langword="true" /> when the row's outline group is collapsed.</summary>
    public bool IsCollapsed { get; set; }

    /// <summary>The outline (grouping) level, 0 when not grouped.</summary>
    public int OutlineLevel { get; set; }

    /// <summary>The row-level default style index, or <see langword="null" /> when unset.</summary>
    public int? StyleIndex { get; set; }

    /// <summary>The populated cells in this row, in column order.</summary>
    public IReadOnlyList<Cell> Cells => _cells.Values.ToList();

    // ── Internal cell storage ───────────────────────────────────────────────────

    internal IEnumerable<Cell> CellsInternal => _cells.Values;

    internal int CellCount => _cells.Count;

    /// <summary><see langword="true" /> when the row has no cells and no non-default properties.</summary>
    internal bool IsEffectivelyEmpty =>
        _cells.Count == 0 && !IsHidden && Height is null && !IsCustomHeight &&
        OutlineLevel == 0 && StyleIndex is null;

    internal Cell? GetCell(int column) => _cells.GetValueOrDefault(column);

    internal Cell GetOrAddCell(Worksheet worksheet, int column)
    {
        if (_cells.TryGetValue(column, out var existing))
            return existing;

        var cell = new Cell(worksheet, new CellReference(RowNumber, column));
        _cells[column] = cell;
        return cell;
    }

    internal void AddCell(Cell cell) => _cells[cell.Column] = cell;

    internal void RemoveCell(int column) => _cells.Remove(column);
}
