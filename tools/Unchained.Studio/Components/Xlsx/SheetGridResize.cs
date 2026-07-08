using Microsoft.AspNetCore.Components.Web;

namespace Unchained.Studio.Components.Xlsx;

/// <summary>
///     Column/row resize for SheetGrid.
///     Holds internal resize state; exposes callbacks for side effects.
/// </summary>
public sealed partial class SheetGrid
{
    private const double PixelsPerChar = 7.0;
    private const double PixelsPerPoint = 96.0 / 72.0;
    // Resize drag state.
    private int _resizeCol = -1;
    private int _resizeRow = -1;
    private double _resizeStartClient;
    private double _resizeStartSize;

    // ── Column / row resize ──────────────────────────────────────────────────

    /// <summary>
    ///     Applies an in-progress column or row header resize from the grid's mousemove.
    ///     Returns <see langword="true" /> if a resize was active and handled.
    /// </summary>
    private bool TryApplyAxisResize(MouseEventArgs e)
    {
        if (_resizeCol != -1)
        {
            var deltaChars = (e.ClientX - _resizeStartClient) / PixelsPerChar;
            var newWidth = Math.Max(0, _resizeStartSize + deltaChars);
            Sheet.SetColumnWidth(_resizeCol, Math.Round(newWidth, 2));
            return true;
        }

        if (_resizeRow == -1) return false;

        var deltaPoints = (e.ClientY - _resizeStartClient) / PixelsPerPoint;
        var newHeight = Math.Max(1, _resizeStartSize + deltaPoints);
        Sheet.SetRowHeight(_resizeRow, Math.Round(newHeight, 1));
        return true;
    }

    private void StartColResize(int col, MouseEventArgs e)
    {
        _resizeCol = col;
        _resizeStartClient = e.ClientX;
        _resizeStartSize = Sheet.GetColumn(col)?.Width ?? SheetGridDisplay.DefaultColWidthChars;
        _dragging = false;
    }

    private void StartRowResize(int row, MouseEventArgs e)
    {
        _resizeRow = row;
        _resizeStartClient = e.ClientY;
        _resizeStartSize = Sheet.GetRow(row)?.Height ?? SheetGridDisplay.DefaultRowHeightPoints;
        _dragging = false;
    }

    private Task FinishResize()
    {
        // The final size is applied on the move handler; here we just clear state and persist.
        _resizeCol = -1;
        _resizeRow = -1;
        return OnEdited.InvokeAsync();
    }

    // Wired from the scroll container's mousemove via OnGridMouseMove.
}
