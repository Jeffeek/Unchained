namespace Unchained.Xlsx.Worksheets;

/// <summary>
///     A frozen-pane configuration: the number of leading rows and/or columns kept visible while the
///     rest of the sheet scrolls.
/// </summary>
public sealed class FrozenPanes
{
    /// <summary>The number of rows frozen at the top (0 = none).</summary>
    public int FrozenRows { get; set; }

    /// <summary>The number of columns frozen at the left (0 = none).</summary>
    public int FrozenColumns { get; set; }

    /// <summary>Creates a frozen-pane configuration freezing the given number of rows and columns.</summary>
    public FrozenPanes(int frozenRows, int frozenColumns)
    {
        FrozenRows = frozenRows;
        FrozenColumns = frozenColumns;
    }
}
