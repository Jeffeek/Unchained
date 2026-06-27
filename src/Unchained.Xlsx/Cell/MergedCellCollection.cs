using System.Collections;
using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Cell;

/// <summary>The set of merged cell ranges in a worksheet.</summary>
public sealed class MergedCellCollection : IReadOnlyList<CellRange>
{
    private readonly List<CellRange> _ranges = [];

    /// <summary>The number of merged ranges.</summary>
    public int Count => _ranges.Count;

    /// <summary>Returns the merged range at the given index.</summary>
    public CellRange this[int index] => _ranges[index];

    /// <inheritdoc />
    public IEnumerator<CellRange> GetEnumerator() => _ranges.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Returns the merged range that contains <paramref name="cell" />, or <see langword="null" />.</summary>
    public CellRange? RangeContaining(CellReference cell) =>
        _ranges.Cast<CellRange?>().FirstOrDefault(r => r!.Value.Contains(cell));

    internal void Add(CellRange range)
    {
        if (!_ranges.Contains(range))
            _ranges.Add(range);
    }

    internal bool Remove(CellRange range) => _ranges.Remove(range);
}
