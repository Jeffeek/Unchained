using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Formulas;

/// <summary>The kind of value a formula evaluation produced.</summary>
internal enum FormulaValueKind
{
    Number,
    Text,
    Boolean,
    Error,
    /// <summary>An empty/blank cell reference result (coerces to 0 or "").</summary>
    Blank,
    /// <summary>A range or array of values (used internally by aggregate functions).</summary>
    Array
}

/// <summary>
///     A value flowing through formula evaluation: a number, text, boolean, error, blank, or a
///     2-D array (the contents of a range, stored row-major). Immutable. Scalars report
///     <see cref="Rows" /> = <see cref="Columns" /> = 0.
/// </summary>
internal readonly struct FormulaValue
{
    private FormulaValue(
        FormulaValueKind kind,
        double number,
        string? text,
        bool boolean,
        CellError error,
        IReadOnlyList<FormulaValue>? array,
        int rows,
        int columns)
    {
        Kind = kind;
        Number = number;
        Text = text;
        Boolean = boolean;
        Error = error;
        Array = array;
        Rows = rows;
        Columns = columns;
    }

    public FormulaValueKind Kind { get; }
    public double Number { get; }
    public string? Text { get; }
    public bool Boolean { get; }
    public CellError Error { get; }
    public IReadOnlyList<FormulaValue>? Array { get; }

    /// <summary>The number of rows when this is a range/array; 0 for scalars.</summary>
    public int Rows { get; }

    /// <summary>The number of columns when this is a range/array; 0 for scalars.</summary>
    public int Columns { get; }

    // ReSharper disable BadListLineBreaks
    public static readonly FormulaValue Blank = new(FormulaValueKind.Blank, 0, null, false, default, null, 0, 0);
    // ReSharper restore BadListLineBreaks

    // ReSharper disable BadListLineBreaks
    public static FormulaValue FromNumber(double value) => new(FormulaValueKind.Number, value, null, false, default, null, 0, 0);
    public static FormulaValue FromText(string value) => new(FormulaValueKind.Text, 0, value, false, default, null, 0, 0);
    public static FormulaValue FromBoolean(bool value) => new(FormulaValueKind.Boolean, value ? 1 : 0, null, value, default, null, 0, 0);
    public static FormulaValue FromError(CellError error) => new(FormulaValueKind.Error, 0, null, false, error, null, 0, 0);

    // ReSharper restore BadListLineBreaks

    /// <summary>Creates a 1-D array (single column) from a flat list.</summary>
    public static FormulaValue FromArray(IReadOnlyList<FormulaValue> values) =>
        // ReSharper disable BadListLineBreaks
        new(FormulaValueKind.Array, 0, null, false, default, values, values.Count, values.Count == 0 ? 0 : 1);
    // ReSharper restore BadListLineBreaks

    /// <summary>Creates a 2-D array from a row-major flat list with explicit dimensions.</summary>
    public static FormulaValue FromGrid(IReadOnlyList<FormulaValue> rowMajor, int rows, int columns) =>
        // ReSharper disable BadListLineBreaks
        new(FormulaValueKind.Array, 0, null, false, default, rowMajor, rows, columns);
    // ReSharper restore BadListLineBreaks

    public bool IsError => Kind == FormulaValueKind.Error;

    /// <summary>Returns the element at (rowIndex, colIndex) within a 2-D array; blank when out of range.</summary>
    public FormulaValue At(int rowIndex, int colIndex)
    {
        if (Kind != FormulaValueKind.Array || Array == null || Columns == 0)
            return Blank;

        var flat = (rowIndex * Columns) + colIndex;
        return flat >= 0 && flat < Array.Count ? Array[flat] : Blank;
    }

    /// <summary>Flattens this value to a sequence of scalars (arrays expand; scalars yield themselves).</summary>
    public IEnumerable<FormulaValue> Flatten()
    {
        if (Kind == FormulaValueKind.Array && Array != null)
        {
            foreach (var inner in Array.SelectMany(static item => item.Flatten()))
                yield return inner;
        }
        else
            yield return this;
    }
}
