using Unchained.Xlsx.Formatting;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.Cell;

/// <summary>
///     A single populated cell within a <see cref="Worksheet" />. Cells exist only when they hold a
///     value, a formula, or an explicit style — empty cells are not materialised.
/// </summary>
public sealed partial class Cell
{
    private readonly Worksheet _worksheet;

    internal Cell(Worksheet worksheet, CellReference reference)
    {
        _worksheet = worksheet;
        Reference = reference;
    }

    /// <summary>The address of this cell.</summary>
    public CellReference Reference { get; }

    /// <summary>The 1-based row index of this cell.</summary>
    public int Row => Reference.Row;

    /// <summary>The 1-based column index of this cell.</summary>
    public int Column => Reference.Column;

    /// <summary>The kind of value this cell holds.</summary>
    public CellType CellType { get; internal set; } = CellType.Empty;

    // ── Backing storage ─────────────────────────────────────────────────────────
    // A numeric cell stores its value in _number; string/error literals in _text; a
    // formula cell stores its formula in _formula plus an optional cached result.

    internal double Number;
    internal string? Text;
    internal string? Formula;
    internal CellError? Error;

    /// <summary>The zero-based index into the workbook style table (<c>cellXfs</c>).</summary>
    public int StyleIndex { get; set; }

    // ── Raw value accessors ─────────────────────────────────────────────────────

    /// <summary>
    ///     The numeric value when <see cref="CellType" /> is <see cref="CellType.Number" />, or the
    ///     cached numeric result of a formula; otherwise <see langword="null" />.
    /// </summary>
    public double? NumberValue =>
        CellType == CellType.Number || (CellType == CellType.Formula && Text == null && Error == null)
            ? Number
            : null;

    /// <summary>The text when <see cref="CellType" /> is <see cref="CellType.String" /> (or a string formula result).</summary>
    public string? StringValue =>
        CellType == CellType.String || (CellType == CellType.Formula && Text != null)
            ? Text
            : null;

    /// <summary>The boolean value when <see cref="CellType" /> is <see cref="CellType.Boolean" />.</summary>
    public bool? BooleanValue =>
        CellType == CellType.Boolean ? Number != 0 : null;

    /// <summary>The error value when <see cref="CellType" /> is <see cref="CellType.Error" /> (or a formula's cached error).</summary>
    public CellError? ErrorValue =>
        CellType == CellType.Error || (CellType == CellType.Formula && Error != null) ? Error : null;

    /// <summary>
    ///     The cell's formula in Excel notation including the leading <c>=</c> (e.g. <c>"=SUM(A1:A10)"</c>),
    ///     or <see langword="null" /> when the cell is not a formula cell.
    /// </summary>
    public string? FormulaText
    {
        get => Formula is null ? null : "=" + Formula;
        set => SetFormulaText(value);
    }

    /// <summary><see langword="true" /> when this cell is part of an array formula.</summary>
    public bool IsArrayFormula { get; internal set; }

    /// <summary>The range an array formula applies to, when <see cref="IsArrayFormula" /> is true.</summary>
    public CellRange? ArrayFormulaRange { get; internal set; }

    // ── Typed getters ─────────────────────────────────────────────────────────

    /// <summary>Returns the numeric value, or <see langword="null" /> when the cell is not numeric.</summary>
    public double? GetDouble() => NumberValue;

    /// <summary>Returns the text value, or <see langword="null" /> when the cell holds no string.</summary>
    public string? GetString() => StringValue;

    /// <summary>Returns the boolean value, or <see langword="null" /> when the cell is not boolean.</summary>
    public bool? GetBoolean() => BooleanValue;

    /// <summary>Returns the error value, or <see langword="null" /> when the cell holds no error.</summary>
    public CellError? GetError() => ErrorValue;

    /// <summary>
    ///     Interprets the numeric value as an Excel date/time serial number and returns the
    ///     corresponding <see cref="DateTime" />, honouring the workbook's date system.
    ///     Returns <see langword="null" /> when the cell is not numeric or the serial is out of range.
    /// </summary>
    public DateTime? GetDateTime() =>
        NumberValue is { } serial ? DateTimeSerializer.ToDateTime(serial, _worksheet.Document.Date1904) : null;

    /// <summary>Returns <see cref="GetDateTime" /> as a UTC <see cref="DateTimeOffset" />.</summary>
    public DateTimeOffset? GetDateTimeOffset() =>
        GetDateTime() is { } dt ? new DateTimeOffset(dt, TimeSpan.Zero) : null;

    /// <summary>
    ///     Returns the cell's value rendered as a display string using its effective number format.
    ///     String cells return their text; errors return the error literal; numeric and date cells
    ///     are formatted per the assigned format code (partial format-engine support — see remarks on
    ///     <c>NumberFormatter</c>).
    /// </summary>
    public string GetFormattedString()
    {
        switch (CellType)
        {
            case CellType.Empty:
                return string.Empty;
            case CellType.String:
                return Text ?? string.Empty;
            case CellType.Boolean:
                return Number != 0 ? "TRUE" : "FALSE";
            case CellType.Error:
                return (Error ?? CellError.Value).ToLiteral();
            case CellType.Formula when Text != null:
                return Text;
            case CellType.Number:
            case CellType.Formula:
            default:
            {
                var code = _worksheet.Document.Styles.GetNumberFormatCode(StyleIndex);
                return NumberFormatter.Format(Number, code, _worksheet.Document.Date1904);
            }
        }
    }

    // ── Internal read helpers ───────────────────────────────────────────────────

    internal void SetNumberInternal(double value)
    {
        CellType = CellType.Number;
        Number = value;
        Text = null;
        Error = null;
    }

    internal void SetStringInternal(string value)
    {
        CellType = CellType.String;
        Text = value;
        Error = null;
    }

    internal void SetBooleanInternal(bool value)
    {
        CellType = CellType.Boolean;
        Number = value ? 1 : 0;
        Text = null;
        Error = null;
    }

    internal void SetErrorInternal(CellError error)
    {
        CellType = CellType.Error;
        Error = error;
        Text = null;
    }

    /// <summary><see langword="true" /> when this cell holds nothing worth serializing.</summary>
    internal bool IsEffectivelyEmpty =>
        CellType == CellType.Empty && Formula is null && StyleIndex == 0;
}
