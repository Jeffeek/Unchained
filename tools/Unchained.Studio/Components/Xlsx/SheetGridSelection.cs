using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Unchained.Xlsx.Models.Cell;

namespace Unchained.Studio.Components.Xlsx;

/// <summary>
///     Selection, navigation, and cell editing for SheetGrid.
///     Holds internal drag/edit state; exposes callbacks for side effects.
/// </summary>
public sealed partial class SheetGrid
{
    private int _activeCol = 1;
    private int _activeRow = 1;
    private int _anchorCol = 1;
    // Selection: anchor (where selection started) + active (cursor). The rectangle between them is selected.
    private int _anchorRow = 1;
    private bool _dragging;
    private int _editCol = -1;
    private ElementReference _editInput;

    private int _editRow = -1;
    private string _editValue = string.Empty;
    private bool _focusPending;

    public CellRange Selection =>
        new(new CellReference(_anchorRow, _anchorCol), new CellReference(_activeRow, _activeCol));

    private string SelectionLabel =>
        _anchorRow == _activeRow && _anchorCol == _activeCol
            ? new CellReference(_activeRow, _activeCol).ToA1()
            : Selection.ToA1();

    private bool InSelection(int row, int col)
    {
        var top = Math.Min(_anchorRow, _activeRow);
        var bottom = Math.Max(_anchorRow, _activeRow);
        var left = Math.Min(_anchorCol, _activeCol);
        var right = Math.Max(_anchorCol, _activeCol);
        return row >= top && row <= bottom && col >= left && col <= right;
    }

    private async Task RaiseSelection()
    {
        await CellSelected.InvokeAsync(Sheet.GetCell(_activeRow, _activeCol));
        await SelectionChanged.InvokeAsync(Selection);
    }

    // ── Selection (click, shift-click, drag) ────────────────────────────────────

    private async Task OnCellMouseDown(int row, int col, MouseEventArgs e)
    {
        if (_editRow != -1)
            await CommitEdit();

        _activeRow = row;
        _activeCol = col;
        if (!e.ShiftKey)
        {
            _anchorRow = row;
            _anchorCol = col;
        }

        _dragging = true;
        await RaiseSelection();
    }

    private Task OnCellMouseOver(int row, int col)
    {
        if (!_dragging || (row == _activeRow && col == _activeCol))
            return Task.CompletedTask;

        _activeRow = row;
        _activeCol = col;
        return RaiseSelection();
    }

    private Task SelectRow(int row)
    {
        _anchorRow = row;
        _activeRow = row;
        _anchorCol = 1;
        _activeCol = _cols;
        return RaiseSelection();
    }

    private Task SelectColumn(int col)
    {
        _anchorCol = col;
        _activeCol = col;
        _anchorRow = 1;
        _activeRow = _rows;
        return RaiseSelection();
    }

    private Task SelectAll()
    {
        _anchorRow = 1;
        _anchorCol = 1;
        _activeRow = _rows;
        _activeCol = _cols;
        return RaiseSelection();
    }

    private async Task ClearSelection()
    {
        Sheet.ClearRange(Selection);
        await OnEdited.InvokeAsync();
        await CellSelected.InvokeAsync(null);
    }

    // ── Editing ──────────────────────────────────────────────────────────────

    private void BeginEdit(int row, int col)
    {
        _anchorRow = _activeRow = row;
        _anchorCol = _activeCol = col;
        _editRow = row;
        _editCol = col;
        _dragging = false;
        _ = FormulaActiveChanged.InvokeAsync(true);

        var cell = Sheet.GetCell(row, col);
        _editValue = cell switch
        {
            null => string.Empty,
            { FormulaText: { } f } => f,
            _ => cell.CellType switch
            {
                CellType.Number => cell.GetDouble()?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                CellType.Boolean => cell.GetBoolean() == true ? "TRUE" : "FALSE",
                _ => cell.GetString() ?? cell.GetFormattedString()
            }
        };

        _focusPending = true;
    }

    private void CommitEditEvent(ChangeEventArgs e) => _editValue = e.Value?.ToString() ?? string.Empty;

    private async Task CommitEdit()
    {
        if (_editRow == -1)
            return;

        var row = _editRow;
        var col = _editCol;
        _editRow = -1;
        _editCol = -1;
        await FormulaActiveChanged.InvokeAsync(false);

        ApplyEdit(row, col, _editValue);
        RecalculateDimensions();

        await OnEdited.InvokeAsync();
        await CellSelected.InvokeAsync(Sheet.GetCell(row, col));
    }

    private async Task OnEditKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "Enter":
                await CommitEdit();
                if (_activeRow < _rows)
                {
                    _activeRow++;
                    _anchorRow = _activeRow;
                    _anchorCol = _activeCol;
                }

            break;
            case "Escape":
                _editRow = -1;
                _editCol = -1;
            break;
            case "Tab":
                await CommitEdit();
                if (_activeCol < _cols)
                {
                    _activeCol++;
                    _anchorRow = _activeRow;
                    _anchorCol = _activeCol;
                }

            break;
        }
    }

    /// <summary>Applies a raw text edit to a cell with type inference.</summary>
    private void ApplyEdit(int row, int col, string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            Sheet.ClearCell(row, col);
            return;
        }

        if (raw.StartsWith('='))
        {
            Sheet.SetFormula(row, col, raw);
            return;
        }

        if (bool.TryParse(raw, out var b))
        {
            Sheet.SetValue(row, col, b);
            return;
        }

        // Avoid coercing zero-padded identifiers ("007") into numbers.
        var looksNumeric = !(raw.Length > 1 && raw[0] == '0' && raw[1] != '.');
        if (looksNumeric &&
            double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d))
        {
            Sheet.SetValue(row, col, d);
            return;
        }

        Sheet.SetValue(row, col, raw);
    }
}
