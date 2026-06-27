using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Cell;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Worksheets;

public sealed partial class Worksheet
{
    /// <summary>The merged cell ranges in this worksheet.</summary>
    public MergedCellCollection MergedCells
    {
        get
        {
            EnsureMergedCellsParsed();
            return MergedCellsInternal;
        }
    }

    internal bool MergedCellsMaterialised { get; private set; }

    internal MergedCellCollection MergedCellsInternal { get; } = new();

    /// <summary>Merges the cells in <paramref name="range" /> into a single visible cell.</summary>
    public void MergeCells(CellRange range)
    {
        EnsureMergedCellsParsed();
        MergedCellsInternal.Add(range);
    }

    /// <summary>Removes the merge covering <paramref name="range" />.</summary>
    public void UnmergeCells(CellRange range)
    {
        EnsureMergedCellsParsed();
        MergedCellsInternal.Remove(range);
    }

    private void EnsureMergedCellsParsed()
    {
        if (MergedCellsMaterialised)
            return;

        MergedCellsMaterialised = true;
        var mergeCells = RawElement?.Child(SmlNames.MergeCells);
        if (mergeCells == null)
            return;

        foreach (var reference in mergeCells.Children(SmlNames.MergeCell).Select(static mergeCell => mergeCell.GetAttr("ref")).OfType<string>())
            MergedCellsInternal.Add(CellRange.FromA1(reference));
    }
}
