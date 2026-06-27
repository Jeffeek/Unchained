using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Cell;

public sealed partial class Cell
{
    // ── Value setters (M3) ──────────────────────────────────────────────────────

    /// <summary>Sets this cell to a numeric value.</summary>
    public void SetValue(double value) => SetNumberInternal(value);

    /// <summary>Sets this cell to a boolean value.</summary>
    public void SetValue(bool value) => SetBooleanInternal(value);

    /// <summary>Sets this cell to an error value.</summary>
    public void SetValue(CellError error) => SetErrorInternal(error);

    /// <summary>Sets this cell to a string value, interning it in the workbook shared-string table.</summary>
    public void SetValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        SetStringInternal(value);
    }

    /// <summary>Clears this cell's value and formula, leaving any explicit style in place.</summary>
    public void Clear()
    {
        CellType = CellType.Empty;
        Number = 0;
        Text = null;
        Error = null;
        Formula = null;
        IsArrayFormula = false;
        ArrayFormulaRange = null;
    }

    // ── Formula (M5) ─────────────────────────────────────────────────────────

    private void SetFormulaText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            Formula = null;
            if (CellType == CellType.Formula)
                CellType = CellType.Empty;
            return;
        }

        Formula = value.TrimStart('=');
        CellType = CellType.Formula;
    }

    // ── Cached formula results (set by the in-engine calculator) ─────────────────

    /// <summary>Stores a numeric cached result on this formula cell, leaving the formula intact.</summary>
    internal void SetFormulaCachedNumber(double value)
    {
        Number = value;
        Text = null;
        Error = null;
    }

    /// <summary>Stores a text cached result on this formula cell.</summary>
    internal void SetFormulaCachedText(string value)
    {
        Text = value;
        Error = null;
    }

    /// <summary>Stores an error cached result on this formula cell.</summary>
    internal void SetFormulaCachedError(CellError error)
    {
        Error = error;
        Text = null;
    }
}
