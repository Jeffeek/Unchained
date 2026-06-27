using System.Collections;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.Cell;

/// <summary>
///     The collection of column definitions in a <see cref="Worksheet" />. Each definition may cover
///     a contiguous range of columns; <see cref="GetOrCreateColumn" /> splits ranges as needed so a
///     single column can be addressed independently.
/// </summary>
public sealed class ColumnCollection : IReadOnlyList<Column>
{
    private readonly List<Column> _columns = [];

    internal IEnumerable<Column> Ordered => _columns.OrderBy(static c => c.Min);

    /// <summary>The number of column definitions.</summary>
    public int Count => _columns.Count;

    /// <summary>Returns the column definition at the given collection index.</summary>
    public Column this[int index] => _columns[index];

    /// <inheritdoc />
    public IEnumerator<Column> GetEnumerator() => _columns.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Returns the definition covering <paramref name="columnNumber" />, or <see langword="null" />.</summary>
    public Column? GetColumn(int columnNumber) =>
        _columns.FirstOrDefault(c => columnNumber >= c.Min && columnNumber <= c.Max);

    /// <summary>
    ///     Returns a definition that covers exactly <paramref name="columnNumber" />, splitting any
    ///     wider definition so this single column can carry independent properties.
    /// </summary>
    public Column GetOrCreateColumn(int columnNumber)
    {
        var existing = GetColumn(columnNumber);
        if (existing == null)
        {
            var fresh = new Column(columnNumber, columnNumber);
            _columns.Add(fresh);
            return fresh;
        }

        if (existing.Min == columnNumber && existing.Max == columnNumber)
            return existing;

        // Split the wider definition into up to three parts so the requested column is isolated.
        var isolated = new Column(columnNumber, columnNumber)
        {
            Width = existing.Width,
            IsCustomWidth = existing.IsCustomWidth,
            IsHidden = existing.IsHidden,
            IsCollapsed = existing.IsCollapsed,
            OutlineLevel = existing.OutlineLevel,
            StyleIndex = existing.StyleIndex
        };

        if (existing.Min < columnNumber)
        {
            var left = Clone(existing, existing.Min, columnNumber - 1);
            _columns.Add(left);
        }

        if (existing.Max > columnNumber)
        {
            var right = Clone(existing, columnNumber + 1, existing.Max);
            _columns.Add(right);
        }

        _columns.Remove(existing);
        _columns.Add(isolated);
        return isolated;
    }

    private static Column Clone(Column source, int min, int max) =>
        new(min, max)
        {
            Width = source.Width,
            IsCustomWidth = source.IsCustomWidth,
            IsHidden = source.IsHidden,
            IsCollapsed = source.IsCollapsed,
            OutlineLevel = source.OutlineLevel,
            StyleIndex = source.StyleIndex
        };

    internal void AddExisting(Column column) => _columns.Add(column);
}
