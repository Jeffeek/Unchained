using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Cell;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Worksheets;

public sealed partial class Worksheet
{
    private readonly MergedCellCollection _mergedCells = new();
    private bool _mergedCellsParsed;

    /// <summary>The merged cell ranges in this worksheet.</summary>
    public MergedCellCollection MergedCells
    {
        get
        {
            EnsureMergedCellsParsed();
            return _mergedCells;
        }
    }

    /// <summary>Merges the cells in <paramref name="range" /> into a single visible cell.</summary>
    public void MergeCells(CellRange range)
    {
        EnsureMergedCellsParsed();
        _mergedCells.Add(range);
    }

    /// <summary>Removes the merge covering <paramref name="range" />.</summary>
    public void UnmergeCells(CellRange range)
    {
        EnsureMergedCellsParsed();
        _mergedCells.Remove(range);
    }

    internal bool MergedCellsMaterialised => _mergedCellsParsed;

    internal MergedCellCollection MergedCellsInternal => _mergedCells;

    private void EnsureMergedCellsParsed()
    {
        if (_mergedCellsParsed)
            return;

        _mergedCellsParsed = true;
        var mergeCells = RawElement?.Child(SmlNames.MergeCells);
        if (mergeCells == null)
            return;

        foreach (var mergeCell in mergeCells.Children(SmlNames.MergeCell))
        {
            var reference = mergeCell.GetAttr("ref");
            if (reference != null)
                _mergedCells.Add(CellRange.FromA1(reference));
        }
    }
}
