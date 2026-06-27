using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Tables;

/// <summary>
///     A structured table (an OOXML <c>ListObject</c>) over a worksheet range, with a header row,
///     optional totals row, named columns, and a table style.
/// </summary>
public sealed class ListObject
{
    private readonly List<ListColumn> _columns = [];

    internal ListObject(int id, string name, CellRange range)
    {
        Id = id;
        Name = name;
        DisplayName = name;
        Range = range;
    }

    /// <summary>The stable table id within the workbook.</summary>
    public int Id { get; internal set; }

    /// <summary>The internal table name (no spaces). Used in structured references.</summary>
    public string Name { get; set; }

    /// <summary>The user-visible table name.</summary>
    public string DisplayName { get; set; }

    /// <summary>The range the table covers, including header and totals rows.</summary>
    public CellRange Range { get; private set; }

    /// <summary>Whether the header row is shown.</summary>
    public bool ShowHeaderRow { get; set; } = true;

    /// <summary>Whether the totals row is shown.</summary>
    public bool ShowTotalsRow { get; set; }

    /// <summary>Whether banded (striped) rows are shown.</summary>
    public bool ShowBandedRows { get; set; } = true;

    /// <summary>Whether banded columns are shown.</summary>
    public bool ShowBandedColumns { get; set; }

    /// <summary>Whether the first column is emphasised.</summary>
    public bool ShowFirstColumn { get; set; }

    /// <summary>Whether the last column is emphasised.</summary>
    public bool ShowLastColumn { get; set; }

    /// <summary>The built-in table style name (e.g. "TableStyleMedium9").</summary>
    public string StyleName { get; set; } = "TableStyleMedium2";

    /// <summary>The table's columns, in order.</summary>
    public IReadOnlyList<ListColumn> Columns => _columns;

    /// <summary>The OPC part URI backing this table (assigned on save).</summary>
    internal string PartUri { get; set; } = string.Empty;

    /// <summary>The relationship id linking the sheet to this table part.</summary>
    internal string RelationshipId { get; set; } = string.Empty;

    /// <summary>Adds a column with the given header name.</summary>
    /// <exception cref="ArgumentException">Thrown when a column with the same name already exists.</exception>
    public ListColumn AddColumn(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (_columns.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"A column named '{name}' already exists in table '{DisplayName}'.", nameof(name));

        var id = _columns.Count == 0 ? 1 : _columns.Max(static c => c.Id) + 1;
        var column = new ListColumn(id, name);
        _columns.Add(column);
        return column;
    }

    /// <summary>Moves the table to a new range. Cells outside the new range are not cleared.</summary>
    public void Resize(CellRange newRange) => Range = newRange;

    internal void AddColumnRaw(ListColumn column) => _columns.Add(column);
}
