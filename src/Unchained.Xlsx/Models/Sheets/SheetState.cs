namespace Unchained.Xlsx.Models.Sheets;

/// <summary>The visibility state of a worksheet within a workbook.</summary>
public enum SheetState
{
    /// <summary>The sheet is visible (default).</summary>
    Visible,

    /// <summary>The sheet is hidden but can be unhidden through the application UI.</summary>
    Hidden,

    /// <summary>
    ///     The sheet is hidden and cannot be unhidden through the application UI —
    ///     only programmatically or via VBA.
    /// </summary>
    VeryHidden
}
