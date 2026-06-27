using System.Globalization;
using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Formulas;

internal static partial class FormulaFunctions
{
    // ── Lookup ──────────────────────────────────────────────────────────────────

    private static FormulaValue VLookup(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 3) return FormulaValue.FromError(CellError.Value);

        var key = ev.Evaluate(args[0]);
        var table = ev.Evaluate(args[1]);
        var colIndex = (int)FormulaEvaluator.ToNumber(ev.Evaluate(args[2]));
        var approximate = args.Count < 4 || FormulaEvaluator.ToBoolean(ev.Evaluate(args[3]));
        if (table.Kind != FormulaValueKind.Array || table.Columns == 0)
            return FormulaValue.FromError(CellError.NotAvailable);

        var matchRow = FindRow(table, key, approximate, 0);
        return matchRow < 0
            ? FormulaValue.FromError(CellError.NotAvailable)
            : colIndex < 1 || colIndex > table.Columns
                ? FormulaValue.FromError(CellError.Reference)
                : table.At(matchRow, colIndex - 1);
    }

    private static FormulaValue HLookup(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 3) return FormulaValue.FromError(CellError.Value);

        var key = ev.Evaluate(args[0]);
        var table = ev.Evaluate(args[1]);
        var rowIndex = (int)FormulaEvaluator.ToNumber(ev.Evaluate(args[2]));
        var approximate = args.Count < 4 || FormulaEvaluator.ToBoolean(ev.Evaluate(args[3]));
        if (table.Kind != FormulaValueKind.Array || table.Columns == 0)
            return FormulaValue.FromError(CellError.NotAvailable);

        var matchCol = FindColumn(table, key, approximate);
        return matchCol < 0
            ? FormulaValue.FromError(CellError.NotAvailable)
            : rowIndex < 1 || rowIndex > table.Rows
                ? FormulaValue.FromError(CellError.Reference)
                : table.At(rowIndex - 1, matchCol);
    }

    private static FormulaValue Index(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var array = ev.Evaluate(args[0]);
        var rowNum = (int)FormulaEvaluator.ToNumber(ev.Evaluate(args[1]));
        var colNum = args.Count >= 3 ? (int)FormulaEvaluator.ToNumber(ev.Evaluate(args[2])) : 0;

        if (array.Kind != FormulaValueKind.Array)
            return rowNum <= 1 ? array : FormulaValue.FromError(CellError.Reference);

        // Single-row or single-column arrays accept one index.
        if (array.Rows == 1 && colNum == 0)
        {
            colNum = rowNum;
            rowNum = 1;
        }

        if (array.Columns == 1 && colNum == 0) colNum = 1;
        if (colNum == 0) colNum = 1;

        return rowNum < 1 || rowNum > array.Rows || colNum < 1 || colNum > array.Columns
            ? FormulaValue.FromError(CellError.Reference)
            : array.At(rowNum - 1, colNum - 1);
    }

    private static FormulaValue Match(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var key = ev.Evaluate(args[0]);
        var array = ev.Evaluate(args[1]).Flatten().ToList();
        var matchType = args.Count >= 3 ? (int)FormulaEvaluator.ToNumber(ev.Evaluate(args[2])) : 1;

        switch (matchType)
        {
            case 0:
                for (var i = 0; i < array.Count; i++)
                {
                    if (ScalarEquals(array[i], key))
                        return Number(i + 1);
                }

                return FormulaValue.FromError(CellError.NotAvailable);
            case > 0:
            {
                var best = -1;
                for (var i = 0; i < array.Count; i++)
                {
                    if (Compare(array[i], key) <= 0)
                        best = i;
                }

                return best < 0 ? FormulaValue.FromError(CellError.NotAvailable) : Number(best + 1);
            }
            default:
            {
                var best = -1;
                for (var i = 0; i < array.Count; i++)
                {
                    if (Compare(array[i], key) >= 0)
                        best = i;
                }

                return best < 0 ? FormulaValue.FromError(CellError.NotAvailable) : Number(best + 1);
            }
        }
    }

    private static FormulaValue Choose(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var index = (int)FormulaEvaluator.ToNumber(ev.Evaluate(args[0]));
        return index < 1 || index >= args.Count ? FormulaValue.FromError(CellError.Value) : ev.Evaluate(args[index]);
    }

    private static int FindRow(FormulaValue table, FormulaValue key, bool approximate, int column)
    {
        var match = -1;
        for (var r = 0; r < table.Rows; r++)
        {
            var cell = table.At(r, column);
            if (!approximate)
            {
                if (ScalarEquals(cell, key)) return r;
            }
            else if (Compare(cell, key) <= 0)
                match = r;
            else
                break;
        }

        return match;
    }

    private static int FindColumn(FormulaValue table, FormulaValue key, bool approximate)
    {
        var match = -1;
        for (var c = 0; c < table.Columns; c++)
        {
            var cell = table.At(0, c);
            if (!approximate)
            {
                if (ScalarEquals(cell, key)) return c;
            }
            else if (Compare(cell, key) <= 0)
                match = c;
            else
                break;
        }

        return match;
    }

    // ── Criterion matching ──────────────────────────────────────────────────────

    private static bool MatchesCriterion(FormulaValue value, FormulaValue criterion)
    {
        if (criterion.Kind != FormulaValueKind.Text)
            return FormulaEvaluator.ToNumber(value).Equals(FormulaEvaluator.ToNumber(criterion));

        var text = criterion.Text ?? string.Empty;
        foreach (var op in new[] { "<>", ">=", "<=", ">", "<", "=" })
        {
            if (!text.StartsWith(op, StringComparison.Ordinal)) continue;

            var rhs = text[op.Length..];
            return double.TryParse(rhs, NumberStyles.Any, CultureInfo.InvariantCulture, out var num) && IsNumber(value)
                ? CompareNum(FormulaEvaluator.ToNumber(value), num, op)
                : CompareText(FormulaEvaluator.ToText(value), rhs, op);
        }

        return string.Equals(FormulaEvaluator.ToText(value), text, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CompareNum(double a, double b, string op) => op switch
    {
        ">" => a > b, "<" => a < b, ">=" => a >= b, "<=" => a <= b, "<>" => !a.Equals(b), _ => a.Equals(b)
    };

    private static bool CompareText(string a, string b, string op)
    {
        var cmp = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        return op switch { "<>" => cmp != 0, ">" => cmp > 0, "<" => cmp < 0, ">=" => cmp >= 0, "<=" => cmp <= 0, _ => cmp == 0 };
    }

    private static bool ScalarEquals(FormulaValue a, FormulaValue b) =>
        IsNumber(a) && IsNumber(b)
            ? FormulaEvaluator.ToNumber(a).Equals(FormulaEvaluator.ToNumber(b))
            : string.Equals(FormulaEvaluator.ToText(a), FormulaEvaluator.ToText(b), StringComparison.OrdinalIgnoreCase);

    private static int Compare(FormulaValue a, FormulaValue b) =>
        IsNumber(a) && IsNumber(b)
            ? FormulaEvaluator.ToNumber(a).CompareTo(FormulaEvaluator.ToNumber(b))
            : string.Compare(FormulaEvaluator.ToText(a), FormulaEvaluator.ToText(b), StringComparison.OrdinalIgnoreCase);
}
