using System.Collections;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.Cell;

/// <summary>
///     The sparse collection of materialised rows in a <see cref="Worksheet" />, keyed by 1-based
///     row number. Only rows that hold cells or carry explicit properties are present.
/// </summary>
public sealed class RowCollection : IReadOnlyList<Row>
{
    private readonly SortedDictionary<int, Row> _rows = [];

    internal IEnumerable<Row> AllRows => _rows.Values;

    /// <summary>The number of materialised rows.</summary>
    public int Count => _rows.Count;

    /// <summary>Returns the materialised row at the given collection index (not row number).</summary>
    public Row this[int index] => _rows.Values.ElementAt(index);

    /// <inheritdoc />
    public IEnumerator<Row> GetEnumerator() => _rows.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Returns the row with the given 1-based number, or <see langword="null" /> if not materialised.</summary>
    public Row? GetRow(int rowNumber) => _rows.GetValueOrDefault(rowNumber);

    /// <summary>Returns the row with the given number, materialising it if necessary.</summary>
    public Row GetOrCreateRow(int rowNumber)
    {
        if (_rows.TryGetValue(rowNumber, out var existing))
            return existing;

        var row = new Row(rowNumber);
        _rows[rowNumber] = row;
        return row;
    }

    /// <summary>Enumerates materialised rows whose number is within <paramref name="fromRow" />..<paramref name="toRow" />.</summary>
    public IEnumerable<Row> GetRowsInRange(int fromRow, int toRow) =>
        _rows.Where(kv => kv.Key >= fromRow && kv.Key <= toRow).Select(static kv => kv.Value);

    // ── Internal ───────────────────────────────────────────────────────────────

    internal void AddExisting(Row row) => _rows[row.RowNumber] = row;

    internal void Remove(int rowNumber) => _rows.Remove(rowNumber);

    internal void RenumberFrom(IEnumerable<Row> rows)
    {
        _rows.Clear();
        foreach (var row in rows)
            _rows[row.RowNumber] = row;
    }
}
