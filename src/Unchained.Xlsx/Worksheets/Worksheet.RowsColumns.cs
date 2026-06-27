using Unchained.Xlsx.Cell;
using Unchained.Xlsx.Formulas;
using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Worksheets;

public sealed partial class Worksheet
{
    // ── Row insert / delete ─────────────────────────────────────────────────────

    /// <summary>
    ///     Inserts a blank row at <paramref name="rowNumber" />, pushing existing rows at and below it
    ///     down by one. Relative A1 references in formulas across the sheet are shifted; absolute
    ///     references and named ranges are not.
    /// </summary>
    public void InsertRow(int rowNumber) => ShiftRows(rowNumber, +1);

    /// <summary>
    ///     Deletes the row at <paramref name="rowNumber" />, collapsing rows below it up by one.
    ///     Relative A1 references are shifted; references into the deleted row become <c>#REF!</c>.
    /// </summary>
    public void DeleteRow(int rowNumber) => ShiftRows(rowNumber, -1);

    /// <summary>Inserts a blank column, pushing existing columns at and to the right of it rightward.</summary>
    public void InsertColumn(int columnNumber) => ShiftColumns(columnNumber, +1);

    /// <summary>Deletes the column at <paramref name="columnNumber" />, collapsing columns to its right.</summary>
    public void DeleteColumn(int columnNumber) => ShiftColumns(columnNumber, -1);

    private void ShiftRows(int rowNumber, int delta)
    {
        EnsureCellsParsedPublic();

        var moved = (from row in RowsInternal.AllRows.OrderBy(static r => r.RowNumber)
                     where delta >= 0 || row.RowNumber != rowNumber
                     let newNumber = row.RowNumber >= rowNumber ? row.RowNumber + delta : row.RowNumber
                     select newNumber == row.RowNumber ? row : RelocateRow(row, newNumber)).ToList();

        RowsInternal.RenumberFrom(moved);
        ShiftAllFormulas(FormulaShifter.Axis.Row, rowNumber, delta);
    }

    private void ShiftColumns(int columnNumber, int delta)
    {
        EnsureCellsParsedPublic();

        foreach (var row in RowsInternal.AllRows.ToList())
        {
            var cells = row.CellsInternal.OrderBy(static c => c.Column).ToList();
            foreach (var cell in cells)
                row.RemoveCell(cell.Column);

            foreach (var cell in cells)
            {
                if (delta < 0 && cell.Column == columnNumber)
                    continue;

                var newColumn = cell.Column >= columnNumber ? cell.Column + delta : cell.Column;
                if (newColumn is < 1 or > CellReference.MaxColumn)
                    continue;

                row.AddCell(RelocateCell(cell, cell.Row, newColumn));
            }
        }

        ShiftAllFormulas(FormulaShifter.Axis.Column, columnNumber, delta);
    }

    private Row RelocateRow(Row source, int newRowNumber)
    {
        var target = new Row(newRowNumber)
        {
            Height = source.Height,
            IsCustomHeight = source.IsCustomHeight,
            IsHidden = source.IsHidden,
            IsCollapsed = source.IsCollapsed,
            OutlineLevel = source.OutlineLevel,
            StyleIndex = source.StyleIndex
        };

        foreach (var cell in source.CellsInternal)
            target.AddCell(RelocateCell(cell, newRowNumber, cell.Column));

        return target;
    }

    private Cell.Cell RelocateCell(Cell.Cell source, int newRow, int newColumn)
    {
        var clone = new Cell.Cell(this, new CellReference(newRow, newColumn))
        {
            CellType = source.CellType,
            StyleIndex = source.StyleIndex,
            IsArrayFormula = source.IsArrayFormula,
            ArrayFormulaRange = source.ArrayFormulaRange,
            Number = source.Number,
            Text = source.Text,
            Error = source.Error,
            Formula = source.Formula
        };
        return clone;
    }

    private void ShiftAllFormulas(FormulaShifter.Axis axis, int at, int delta)
    {
        foreach (var cell in RowsInternal.AllRows.SelectMany(static row => row.CellsInternal))
        {
            if (cell.Formula != null)
                cell.Formula = FormulaShifter.Shift(cell.Formula, axis, at, delta);
        }
    }

    // ── Row properties ──────────────────────────────────────────────────────────

    /// <summary>Sets a row's height in points, materialising the row if necessary.</summary>
    public void SetRowHeight(int rowNumber, double heightPoints)
    {
        var row = Rows.GetOrCreateRow(rowNumber);
        row.Height = heightPoints;
        row.IsCustomHeight = true;
    }

    /// <summary>Hides the given row.</summary>
    public void HideRow(int rowNumber) => Rows.GetOrCreateRow(rowNumber).IsHidden = true;

    /// <summary>Shows the given row.</summary>
    public void ShowRow(int rowNumber)
    {
        var row = Rows.GetRow(rowNumber);
        row?.IsHidden = false;
    }
}
