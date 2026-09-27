using Microsoft.JSInterop;

namespace Unchained.Studio.Components.Xlsx;

/// <summary>
///     Viewport virtualization for SheetGrid: tracks the scroll window reported by JS and
///     exposes the visible row/column range plus the spacer sizes that keep the scrollbar
///     geometry identical to a fully rendered grid.
/// </summary>
public sealed partial class SheetGrid
{
    /// <summary>Extra rows/columns rendered outside the viewport so small scrolls don't flash blanks.</summary>
    private const int Overscan = 6;

    private double _scrollTop;
    private double _scrollLeft;
    private double _viewportWidth;
    private double _viewportHeight;
    private bool _viewportKnown;

    // Cumulative pixel offsets: _rowOffsets[i] is the top of display row i+1.
    // Length is _rows + 1 so the last entry is the total extent.
    private double[] _rowOffsets = [];
    private double[] _colOffsets = [];

    private int _firstRow = 1;
    private int _lastRow = MinRows;
    private int _firstCol = 1;
    private int _lastCol = MinCols;

    /// <summary>Height of the filler row above the rendered window.</summary>
    private double TopSpacer => _rowOffsets.Length == 0 ? 0 : _rowOffsets[_firstRow - 1];

    /// <summary>Height of the filler row below the rendered window.</summary>
    private double BottomSpacer =>
        _rowOffsets.Length == 0 ? 0 : _rowOffsets[_rows] - _rowOffsets[_lastRow];

    /// <summary>Width of the filler cell left of the rendered window.</summary>
    private double LeftSpacer => _colOffsets.Length == 0 ? 0 : _colOffsets[_firstCol - 1];

    /// <summary>Width of the filler cell right of the rendered window.</summary>
    private double RightSpacer =>
        _colOffsets.Length == 0 ? 0 : _colOffsets[_cols] - _colOffsets[_lastCol];

    /// <summary>
    ///     Invoked from JS on scroll/resize of the sheet scroll container. Re-renders only when
    ///     the visible window actually moved — a scroll inside the overscan band is a no-op.
    /// </summary>
    [JSInvokable]
    public void OnViewportChanged(double scrollTop, double scrollLeft, double clientWidth, double clientHeight)
    {
        _scrollTop = scrollTop;
        _scrollLeft = scrollLeft;
        _viewportWidth = clientWidth;
        _viewportHeight = clientHeight;
        _viewportKnown = clientHeight > 0;

        if (RecalculateWindow())
            StateHasChanged();
    }

    /// <summary>Rebuilds the cumulative offset tables. Called whenever dimensions or sizes change.</summary>
    private void RebuildOffsets()
    {
        if (_rowOffsets.Length != _rows + 1)
            _rowOffsets = new double[_rows + 1];
        if (_colOffsets.Length != _cols + 1)
            _colOffsets = new double[_cols + 1];

        var top = 0.0;
        for (var r = 1; r <= _rows; r++)
        {
            _rowOffsets[r - 1] = top;
            top += SheetGridDisplay.RowHeightPx(Sheet, r);
        }

        _rowOffsets[_rows] = top;

        var left = 0.0;
        for (var c = 1; c <= _cols; c++)
        {
            _colOffsets[c - 1] = left;
            left += SheetGridDisplay.ColumnWidthPx(Sheet, c);
        }

        _colOffsets[_cols] = left;
    }

    /// <summary>
    ///     Recomputes the visible row/column window from the last reported scroll offsets.
    ///     Returns <see langword="true" /> when the window changed and a re-render is needed.
    /// </summary>
    private bool RecalculateWindow()
    {
        var firstRow = _firstRow;
        var lastRow = _lastRow;
        var firstCol = _firstCol;
        var lastCol = _lastCol;

        if (!_viewportKnown)
        {
            // No measurement yet (first render): render a screenful so the grid isn't blank
            // and the container gets a real height for JS to measure.
            _firstRow = 1;
            _lastRow = Math.Min(_rows, MinRows);
            _firstCol = 1;
            _lastCol = Math.Min(_cols, MinCols);
        }
        else
        {
            // The grid scrolls inside a container that also holds the sticky headers, so the
            // body offset is the raw scrollTop; the header band is sticky, not scrolled away.
            (_firstRow, _lastRow) = WindowFor(_rowOffsets, _rows, _scrollTop, _viewportHeight);
            (_firstCol, _lastCol) = WindowFor(_colOffsets, _cols, _scrollLeft, _viewportWidth);
        }

        return _firstRow != firstRow || _lastRow != lastRow || _firstCol != firstCol || _lastCol != lastCol;
    }

    /// <summary>
    ///     Binary-searches the cumulative offsets for the 1-based index range intersecting
    ///     [<paramref name="start" />, start + <paramref name="extent" />], padded by <see cref="Overscan" />.
    /// </summary>
    private static (int First, int Last) WindowFor(double[] offsets, int count, double start, double extent)
    {
        if (count <= 0 || offsets.Length == 0)
            return (1, 0);

        var first = IndexAt(offsets, count, start) - Overscan;
        var last = IndexAt(offsets, count, start + extent) + Overscan;

        return (Math.Max(1, first), Math.Min(count, last));
    }

    /// <summary>Returns the 1-based index whose band contains <paramref name="position" />.</summary>
    private static int IndexAt(double[] offsets, int count, double position)
    {
        // offsets is sorted ascending; Array.BinarySearch gives the insertion point when absent.
        var found = Array.BinarySearch(offsets, 0, count, position);
        var index = found >= 0 ? found : ~found - 1;
        return Math.Clamp(index + 1, 1, count);
    }
}
