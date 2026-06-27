using System.Text;
using System.Text.RegularExpressions;
using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Formulas;

/// <summary>
///     Shifts A1-style cell references inside formula strings when rows or columns are inserted or
///     deleted. Absolute references (those carrying a <c>$</c> on the affected axis) are not shifted.
///     Sheet-qualified references, named ranges, quoted text, and string-argument functions such as
///     <c>INDIRECT</c> are intentionally left untouched — see <c>research-notes.md</c> Difficulty 3.
/// </summary>
internal static partial class FormulaShifter
{
    public enum Axis { Row, Column }

    // Matches an optional sheet qualifier we must skip over, then a cell reference with optional
    // $ markers. We deliberately avoid matching references preceded by '!' handling here and instead
    // shift every bare A1 token; cross-sheet shifting is out of scope for v0.1.0.
    [GeneratedRegex(@"(\$?)([A-Za-z]{1,3})(\$?)(\d{1,7})", RegexOptions.CultureInvariant)]
    private static partial Regex CellRefPattern();

    /// <summary>
    ///     Returns <paramref name="formula" /> with references adjusted for an insert/delete of
    ///     <paramref name="count" /> units (positive = insert, negative = delete) at or after
    ///     <paramref name="at" /> on the given <paramref name="axis" />.
    /// </summary>
    public static string Shift(string formula, Axis axis, int at, int count)
    {
        if (string.IsNullOrEmpty(formula))
            return formula;

        return ProcessOutsideQuotes(
            formula,
            segment =>
                CellRefPattern().Replace(segment, match => ShiftMatch(match, axis, at, count))
        );
    }

    /// <summary>
    ///     Shifts every relative reference in <paramref name="formula" /> by a fixed row and column
    ///     delta, used to expand a shared-formula continuation from its master. Absolute references
    ///     (those with a <c>$</c> on the affected axis) are left unchanged.
    /// </summary>
    public static string ShiftRelative(string formula, int rowDelta, int columnDelta)
    {
        if (string.IsNullOrEmpty(formula) || (rowDelta == 0 && columnDelta == 0))
            return formula;

        return ProcessOutsideQuotes(
            formula,
            segment =>
                CellRefPattern().Replace(segment, match => ShiftMatchRelative(match, rowDelta, columnDelta))
        );
    }

    private static string ShiftMatchRelative(Match match, int rowDelta, int columnDelta)
    {
        var colAbsolute = match.Groups[1].Value == "$";
        var letters = match.Groups[2].Value;
        var rowAbsolute = match.Groups[3].Value == "$";
        var digits = match.Groups[4].Value;

        if (!int.TryParse(digits, out var row) || letters.Length is < 1 or > 3 || row is < 1 or > CellReference.MaxRow)
            return match.Value;

        int column;
        try { column = CellReference.ColumnLettersToNumber(letters); }
        catch (FormatException) { return match.Value; }

        if (column > CellReference.MaxColumn)
            return match.Value;

        if (!rowAbsolute) row += rowDelta;
        if (!colAbsolute) column += columnDelta;

        if (row is < 1 or > CellReference.MaxRow || column is < 1 or > CellReference.MaxColumn)
            return "#REF!";

        var builder = new StringBuilder();
        if (colAbsolute) builder.Append('$');
        builder.Append(CellReference.ColumnNumberToLetters(column));
        if (rowAbsolute) builder.Append('$');
        builder.Append(row);
        return builder.ToString();
    }

    private static string ShiftMatch(Match match, Axis axis, int at, int count)
    {
        var colAbsolute = match.Groups[1].Value == "$";
        var letters = match.Groups[2].Value;
        var rowAbsolute = match.Groups[3].Value == "$";
        var digits = match.Groups[4].Value;

        if (!int.TryParse(digits, out var row) ||
            letters.Length is < 1 or > 3 ||
            row is < 1 or > CellReference.MaxRow)
            return match.Value;

        int column;
        try { column = CellReference.ColumnLettersToNumber(letters); }
        catch (FormatException) { return match.Value; }

        if (column > CellReference.MaxColumn)
            return match.Value;

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (axis)
        {
            case Axis.Row when !rowAbsolute:
            {
                if (count < 0 && row >= at && row < at - count)
                    return "#REF!"; // reference fell inside the deleted band

                if (row >= at)
                    row += count;
                break;
            }
            case Axis.Column when !colAbsolute:
            {
                if (count < 0 && column >= at && column < at - count)
                    return "#REF!";

                if (column >= at)
                    column += count;
                break;
            }
        }

        if (row is < 1 or > CellReference.MaxRow || column is < 1 or > CellReference.MaxColumn)
            return "#REF!";

        var builder = new StringBuilder();
        if (colAbsolute) builder.Append('$');
        builder.Append(CellReference.ColumnNumberToLetters(column));
        if (rowAbsolute) builder.Append('$');
        builder.Append(row);
        return builder.ToString();
    }

    /// <summary>
    ///     Applies <paramref name="transform" /> only to the parts of <paramref name="formula" /> outside double-quoted
    ///     strings.
    /// </summary>
    private static string ProcessOutsideQuotes(string formula, Func<string, string> transform)
    {
        var result = new StringBuilder(formula.Length);
        var segment = new StringBuilder();
        var inQuotes = false;

        foreach (var c in formula)
        {
            if (c == '"')
            {
                if (!inQuotes)
                {
                    result.Append(transform(segment.ToString()));
                    segment.Clear();
                }

                inQuotes = !inQuotes;
                result.Append(c);
                continue;
            }

            if (inQuotes)
                result.Append(c);
            else
                segment.Append(c);
        }

        if (segment.Length > 0)
            result.Append(transform(segment.ToString()));

        return result.ToString();
    }
}
