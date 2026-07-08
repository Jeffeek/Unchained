using Microsoft.AspNetCore.Components.Web;
using Unchained.Ooxml;
using Unchained.Xlsx.Drawings;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Drawings;

namespace Unchained.Studio.Components.Xlsx;

/// <summary>
///     Drawing overlay: selection, drag, resize, context-menu.
///     Holds internal drawing state; exposes callbacks for side effects.
/// </summary>
public sealed partial class SheetGrid
{
    private const double EmusPerPixel = 9525.0; // 914400 EMU per inch / 96 dpi
    private object? _contextMenuTarget;         // null = grid, ChartDrawing = chart
    private bool _contextMenuVisible;

    // Context menu.
    private double _contextMenuX;
    private double _contextMenuY;
    private WorksheetDrawing? _dragChart;
    private Emu _dragOffsetX = Emu.Zero;
    private Emu _dragOffsetY = Emu.Zero;
    private double _dragStartX;
    private double _dragStartY;
    private WorksheetDrawing? _resizeChart;
    private DrawingResizeDir _resizeDirection;
    private double _resizeStartX;
    private double _resizeStartY;

    // Drawing selection + drag/resize.
    private WorksheetDrawing? _selectedDrawing;

    private bool HasChartMenu => _contextMenuTarget is ChartDrawing;
    private bool HasImageMenu => _contextMenuTarget is PictureDrawing;

    private void DeselectDrawing() => _selectedDrawing = null;

    private void OnDrawingMouseDown(WorksheetDrawing drawing, MouseEventArgs e)
    {
        DeselectDrawing();
        _selectedDrawing = drawing;

        // Start drag (don't fire ChartSelected — that's on double-click).
        _dragChart = drawing;
        _dragStartX = e.ClientX;
        _dragStartY = e.ClientY;

        // Remember the current offsets so we accumulate the delta on top of them.
        _dragOffsetX = drawing.Anchor.FromOffsetX;
        _dragOffsetY = drawing.Anchor.FromOffsetY;

        StateHasChanged();
    }

    private void OnChartDoubleClick(ChartDrawing chart) => ChartSelected.InvokeAsync(chart);

    private void DeleteDrawing(WorksheetDrawing drawing)
    {
        Sheet.Drawings.Remove(drawing);
        if (_selectedDrawing == drawing)
            DeselectDrawing();

        _ = OnEdited.InvokeAsync();
    }

    // ── Drawing drag ───────────────────────────────────────────────────────────

    private void OnGridMouseMove(MouseEventArgs e)
    {
        // Column/row header resize takes priority (handled in the resize partial).
        if (TryApplyAxisResize(e))
        {
            StateHasChanged();
            return;
        }

        if (_resizeChart != null)
        {
            ApplyResizeDelta(e.ClientX - _resizeStartX, e.ClientY - _resizeStartY);
            StateHasChanged();
        }
        else if (_dragChart != null)
        {
            var anchor = _dragChart.Anchor;
            var deltaX = e.ClientX - _dragStartX;
            var deltaY = e.ClientY - _dragStartY;

            // Total offset in pixels from the anchor cell's top-left corner.
            var totalX = (_dragOffsetX.Value / EmusPerPixel) + deltaX;
            var totalY = (_dragOffsetY.Value / EmusPerPixel) + deltaY;

            // Walk columns to find the anchor cell.
            var col = anchor.From.Column;
            var running = 0.0;
            for (var c = anchor.From.Column; c <= Math.Max(col, Sheet.Columns.Count); c++)
            {
                var cw = SheetGridDisplay.ColumnWidthPx(Sheet, c);
                if (totalX < running + cw)
                {
                    col = c;
                    break;
                }

                running += cw;
            }

            // Walk rows to find the anchor cell.
            var row = anchor.From.Row;
            running = 0.0;
            for (var r = anchor.From.Row; r <= Math.Max(row, Sheet.Rows.Count); r++)
            {
                var rh = SheetGridDisplay.RowHeightPx(Sheet, r);
                if (totalY < running + rh)
                {
                    row = r;
                    break;
                }

                running += rh;
            }

            anchor.From = new CellReference(row, col);
            anchor.FromOffsetX = Emu.FromPixels(totalX, 96);
            anchor.FromOffsetY = Emu.FromPixels(totalY, 96);

            StateHasChanged();
        }
    }

    private void ApplyResizeDelta(double deltaX, double deltaY)
    {
        if (_resizeChart == null)
            return;

        var anchor = _resizeChart.Anchor;
        const double minW = 100.0;
        const double minH = 60.0;

        if (anchor.AnchorType != DrawingAnchorType.OneCell) return;

        var w = (anchor.Width.Value / EmusPerPixel) +
                (_resizeDirection is DrawingResizeDir.TopLeft or DrawingResizeDir.BottomLeft ? -deltaX : deltaX);
        var h = (anchor.Height.Value / EmusPerPixel) +
                (_resizeDirection is DrawingResizeDir.TopLeft or DrawingResizeDir.TopRight ? -deltaY : deltaY);

        if (w >= minW)
        {
            anchor.Width = Emu.FromPixels(w, 96);
            if (_resizeDirection is DrawingResizeDir.TopLeft or DrawingResizeDir.BottomLeft)
                anchor.FromOffsetX = Emu.FromPixels(deltaX, 96);
        }

        if (!(h >= minH)) return;

        anchor.Height = Emu.FromPixels(h, 96);
        if (_resizeDirection is DrawingResizeDir.TopLeft or DrawingResizeDir.TopRight)
            anchor.FromOffsetY = Emu.FromPixels(deltaY, 96);
    }

    private void StartResizeDrawing(WorksheetDrawing drawing, MouseEventArgs e, DrawingResizeDir direction)
    {
        _resizeChart = drawing;
        _resizeDirection = direction;
        _resizeStartX = e.ClientX;
        _resizeStartY = e.ClientY;
        _selectedDrawing = drawing;

        // Prevent any other drag from starting.
        _dragChart = null;
    }

    // ── Drawing context menu ───────────────────────────────────────────────────

    private void OnChartContextMenu(ChartDrawing chart, MouseEventArgs e)
    {
        _contextMenuTarget = chart;
        _contextMenuX = e.ClientX;
        _contextMenuY = e.ClientY;
        _contextMenuVisible = true;
        StateHasChanged();
    }

    private void OnDrawingContextMenu(WorksheetDrawing drawing, MouseEventArgs e)
    {
        _contextMenuTarget = drawing;
        _contextMenuX = e.ClientX;
        _contextMenuY = e.ClientY;
        _contextMenuVisible = true;
        StateHasChanged();
    }

    private void OnGridContextMenu(MouseEventArgs e)
    {
        _contextMenuTarget = null;
        _contextMenuX = e.ClientX;
        _contextMenuY = e.ClientY;
        _contextMenuVisible = true;
        StateHasChanged();
    }

    private void CloseContextMenu() => _contextMenuVisible = false;

    // ── Drawing click on empty area ────────────────────────────────────────────

    private void OnScrollAreaClick()
    {
        DeselectDrawing();
        CloseContextMenu();
    }

    private enum DrawingResizeDir { TopLeft, TopRight, BottomLeft, BottomRight }
}
