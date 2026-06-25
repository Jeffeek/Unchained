using System.Collections;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.Tables;

/// <summary>The collection of structured tables (<see cref="ListObject" />) on a worksheet.</summary>
public sealed class ListObjectCollection : IReadOnlyList<ListObject>
{
    private readonly Worksheet _worksheet;
    private readonly List<ListObject> _tables = [];

    internal ListObjectCollection(Worksheet worksheet) => _worksheet = worksheet;

    /// <summary>The number of tables.</summary>
    public int Count => _tables.Count;

    /// <summary>Returns the table at the given index.</summary>
    public ListObject this[int index] => _tables[index];

    /// <inheritdoc />
    public IEnumerator<ListObject> GetEnumerator() => _tables.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Returns the table with the given display name, or <see langword="null" />.</summary>
    public ListObject? Find(string displayName) =>
        _tables.FirstOrDefault(t => t.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Adds a table over <paramref name="range" />. When <paramref name="hasHeaders" /> is true, the
    ///     first row supplies the column names; otherwise generic names (Column1, Column2…) are used.
    /// </summary>
    public ListObject Add(CellRange range, string? name = null, bool hasHeaders = true)
    {
        var id = _worksheet.Document.NextTableId();
        var tableName = name ?? $"Table{id}";
        var table = new ListObject(id, tableName, range) { ShowHeaderRow = hasHeaders };

        for (var col = range.TopLeft.Column; col <= range.BottomRight.Column; col++)
        {
            var header = hasHeaders
                ? _worksheet.GetCell(range.TopLeft.Row, col)?.GetString() ?? $"Column{col - range.TopLeft.Column + 1}"
                : $"Column{col - range.TopLeft.Column + 1}";
            table.AddColumn(EnsureUnique(table, header));
        }

        _tables.Add(table);
        return table;
    }

    /// <summary>Removes a table.</summary>
    public void Remove(ListObject table) => _tables.Remove(table);

    internal void AddExisting(ListObject table) => _tables.Add(table);

    internal IReadOnlyList<ListObject> All => _tables;

    private static string EnsureUnique(ListObject table, string header)
    {
        if (!table.Columns.Any(c => c.Name.Equals(header, StringComparison.OrdinalIgnoreCase)))
            return header;

        var n = 2;
        string candidate;
        do candidate = $"{header}{n++}";
        while (table.Columns.Any(c => c.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)));

        return candidate;
    }
}
