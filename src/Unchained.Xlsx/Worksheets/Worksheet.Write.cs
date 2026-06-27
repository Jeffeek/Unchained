using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Worksheets;

public sealed partial class Worksheet
{
    // ── Cell mutation ───────────────────────────────────────────────────────────

    /// <summary>Sets a numeric value at the given 1-based position.</summary>
    public void SetValue(int row, int column, double value) => this[row, column].SetValue(value);

    /// <summary>Sets a string value at the given 1-based position.</summary>
    public void SetValue(int row, int column, string value) => this[row, column].SetValue(value);

    /// <summary>Sets a boolean value at the given 1-based position.</summary>
    public void SetValue(int row, int column, bool value) => this[row, column].SetValue(value);

    /// <summary>Sets a date/time value (converted to a serial number) at the given 1-based position.</summary>
    public void SetValue(int row, int column, DateTime value) =>
        this[row, column].SetValue(Formatting.DateTimeSerializer.ToSerial(value, Document.Date1904));

    /// <summary>Sets a date/time value (converted to a serial number) at the given 1-based position.</summary>
    public void SetValue(int row, int column, DateTimeOffset value) =>
        SetValue(row, column, value.UtcDateTime);

    /// <summary>
    ///     Sets a value of unknown runtime type, dispatching to the matching typed setter. Supports
    ///     <see langword="null" /> (clears the cell), <see langword="string" />, the numeric primitives,
    ///     <see langword="bool" />, <see cref="DateTime" />, and <see cref="DateTimeOffset" />.
    /// </summary>
    public void SetValue(int row, int column, object? value)
    {
        switch (value)
        {
            case null: ClearCell(row, column); break;
            case string s: SetValue(row, column, s); break;
            case bool b: SetValue(row, column, b); break;
            case DateTime dt: SetValue(row, column, dt); break;
            case DateTimeOffset dto: SetValue(row, column, dto); break;
            case CellError e: this[row, column].SetValue(e); break;
            case IConvertible conv: SetValue(row, column, conv.ToDouble(System.Globalization.CultureInfo.InvariantCulture)); break;
            default:
                throw new ArgumentException($"Unsupported cell value type '{value.GetType().Name}'.", nameof(value));
        }
    }

    /// <summary>Sets a formula (with or without a leading <c>=</c>) at the given 1-based position.</summary>
    public void SetFormula(int row, int column, string formula)
    {
        ArgumentException.ThrowIfNullOrEmpty(formula);
        this[row, column].FormulaText = formula.StartsWith('=') ? formula : "=" + formula;
    }

    /// <summary>Sets a formula along with its last-calculated numeric result.</summary>
    public void SetFormulaWithCache(int row, int column, string formula, double cachedValue)
    {
        SetFormula(row, column, formula);
        this[row, column].Number = cachedValue;
    }

    /// <summary>Removes the cell at the given position from the model.</summary>
    public void ClearCell(int row, int column)
    {
        EnsureCellsParsedPublic();
        _rows.GetRow(row)?.RemoveCell(column);
    }

    /// <summary>Clears every cell within <paramref name="range" />.</summary>
    public void ClearRange(CellRange range)
    {
        EnsureCellsParsedPublic();
        for (var r = range.TopLeft.Row; r <= range.BottomRight.Row; r++)
        {
            var row = _rows.GetRow(r);
            if (row == null)
                continue;

            for (var c = range.TopLeft.Column; c <= range.BottomRight.Column; c++)
                row.RemoveCell(c);
        }
    }

    // ── Bulk import ───────────────────────────────────────────────────────────

    /// <summary>Writes a 2-D array of values starting at <paramref name="topRow" />, <paramref name="leftColumn" />.</summary>
    public void ImportArray(object?[,] data, int topRow, int leftColumn)
    {
        ArgumentNullException.ThrowIfNull(data);
        var rows = data.GetLength(0);
        var cols = data.GetLength(1);
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
                SetValue(topRow + r, leftColumn + c, data[r, c]);
    }

    /// <summary>Writes a sequence of value rows starting at <paramref name="topRow" />, <paramref name="leftColumn" />.</summary>
    public void ImportRows(IEnumerable<IEnumerable<object?>> rows, int topRow, int leftColumn)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var r = topRow;
        foreach (var rowValues in rows)
        {
            var c = leftColumn;
            foreach (var value in rowValues)
                SetValue(r, c++, value);
            r++;
        }
    }

    private void EnsureCellsParsedPublic() => _ = Rows;
}
