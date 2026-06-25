using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Worksheets;

/// <summary>View settings for a worksheet (grid lines, headers, zoom, active cell, frozen panes).</summary>
public sealed class SheetView
{
    /// <summary>Whether grid lines are shown.</summary>
    public bool ShowGridLines { get; set; } = true;

    /// <summary>Whether row and column headers are shown.</summary>
    public bool ShowRowColHeaders { get; set; } = true;

    /// <summary>Whether formulas are shown instead of their results.</summary>
    public bool ShowFormulas { get; set; }

    /// <summary>The zoom scale as a percentage (e.g. 100).</summary>
    public int ZoomScale { get; set; } = 100;

    /// <summary>The active cell, or <see langword="null" /> when unset.</summary>
    public CellReference? ActiveCell { get; set; }

    /// <summary>The frozen panes for this view, or <see langword="null" /> when none.</summary>
    public FrozenPanes? FrozenPanes { get; set; }
}
