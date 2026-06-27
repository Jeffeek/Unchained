using System.Collections;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.Pivot;

/// <summary>The pivot tables anchored on a worksheet.</summary>
public sealed class PivotTableCollection : IReadOnlyList<PivotTable>
{
    private readonly List<PivotTable> _pivots = [];
    private readonly Worksheet _worksheet;

    internal PivotTableCollection(Worksheet worksheet) => _worksheet = worksheet;

    internal IReadOnlyList<PivotTable> All => _pivots;

    /// <summary>The number of pivot tables.</summary>
    public int Count => _pivots.Count;

    /// <summary>Returns the pivot table at the given index.</summary>
    public PivotTable this[int index] => _pivots[index];

    /// <inheritdoc />
    public IEnumerator<PivotTable> GetEnumerator() => _pivots.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Returns the pivot table with the given name, or <see langword="null" />.</summary>
    public PivotTable? Find(string name) =>
        _pivots.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Creates a pivot table from <paramref name="sourceRange" /> (whose first row supplies the
    ///     field names) placed at <paramref name="targetCell" />, snapshotting the source data into
    ///     the cache. The source range is taken from <paramref name="sourceSheet" /> (defaults to this sheet).
    /// </summary>
    /// <exception cref="ArgumentException">When the range is empty, headers are blank, or the target overlaps the source.</exception>
    public PivotTable Add(CellRange sourceRange, CellReference targetCell, string name, Worksheet? sourceSheet = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var source = sourceSheet ?? _worksheet;

        if (sourceRange.RowCount < 1)
            throw new ArgumentException("The source range must have at least a header row.", nameof(sourceRange));
        if (ReferenceEquals(source, _worksheet) && sourceRange.Contains(targetCell))
            throw new ArgumentException("The target cell cannot lie inside the source range.", nameof(targetCell));

        var cacheId = _worksheet.Document.NextPivotCacheId();
        var pivot = new PivotTable(name, cacheId, sourceRange, targetCell, source.Name);

        // Build the field list from the header row.
        var headerRow = sourceRange.TopLeft.Row;
        for (var c = sourceRange.TopLeft.Column; c <= sourceRange.BottomRight.Column; c++)
        {
            var header = source.GetCell(headerRow, c)?.GetFormattedString();
            if (string.IsNullOrEmpty(header))
                throw new ArgumentException("All header cells in the source range must be non-empty.", nameof(sourceRange));

            pivot.AddFieldRaw(new PivotField(header, c - sourceRange.TopLeft.Column));
        }

        pivot.Refresh(NameResolver(source));
        _pivots.Add(pivot);
        return pivot;
    }

    /// <summary>Removes a pivot table.</summary>
    public void Remove(PivotTable pivot) => _pivots.Remove(pivot);

    /// <summary>Refreshes every pivot table on this sheet from its source data.</summary>
    public void RefreshAll()
    {
        foreach (var pivot in _pivots)
            pivot.Refresh(NameResolver(_worksheet));
    }

    internal void AddExisting(PivotTable pivot) => _pivots.Add(pivot);

    private PivotTable.SpreadsheetCallback NameResolver(Worksheet fallback) =>
        sheetName => _worksheet.Document.Sheets.Find(sheetName) ?? fallback;
}
