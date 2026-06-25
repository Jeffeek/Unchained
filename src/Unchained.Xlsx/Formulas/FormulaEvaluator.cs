using System.Globalization;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Worksheets;
using XlsxCell = Unchained.Xlsx.Cell.Cell;

namespace Unchained.Xlsx.Formulas;

/// <summary>
///     Evaluates a parsed formula tree against a worksheet grid. Resolves cell/range references
///     (same-sheet and sheet-qualified), applies operators with Excel coercion rules, and dispatches
///     functions to <see cref="FormulaFunctions" />. Detects circular references and returns
///     <c>#REF!</c> for them.
/// </summary>
internal sealed class FormulaEvaluator(Worksheet sheet, HashSet<string>? evaluating = null)
{
    private readonly HashSet<string> _evaluating = evaluating ?? [];

    /// <summary>Parses and evaluates <paramref name="formula" /> (without the leading <c>=</c>).</summary>
    public FormulaValue Evaluate(string formula)
    {
        try
        {
            return Evaluate(FormulaParser.Parse(formula));
        }
        catch (FormulaParseException)
        {
            return FormulaValue.FromError(CellError.Name);
        }
    }

    public FormulaValue Evaluate(FormulaNode node) => node switch
    {
        NumberNode n => FormulaValue.FromNumber(n.Value),
        TextNode t => FormulaValue.FromText(t.Value),
        BooleanNode b => FormulaValue.FromBoolean(b.Value),
        ErrorNode e => FormulaValue.FromError(e.Error),
        ReferenceNode r => EvaluateReference(r.Text),
        RangeNode range => EvaluateRange(range),
        UnaryNode u => EvaluateUnary(u),
        BinaryNode bin => EvaluateBinary(bin),
        FunctionNode fn => EvaluateFunction(fn),
        _ => FormulaValue.FromError(CellError.Value)
    };

    // ── References ──────────────────────────────────────────────────────────────

    private FormulaValue EvaluateReference(string text)
    {
        // Sheet-qualified reference: Sheet1!A1 or 'My Sheet'!A1.
        var (sheet1, local) = SplitSheet(text);
        if (CellReference.TryFromA1(local, out var reference))
            return ReadCell(sheet1 ?? sheet, reference);

        // Defined name → resolve to its formula target.
        var defined = sheet.Document.DefinedNames.Find(text)
                      ?? sheet.Document.DefinedNames.FirstOrDefault(n => n.Name.Equals(text, StringComparison.OrdinalIgnoreCase));
        return defined != null ? Evaluate(StripLeadingEquals(defined.Formula)) : FormulaValue.FromError(CellError.Name);
    }

    private FormulaValue EvaluateRange(RangeNode range)
    {
        if (range.Start is not ReferenceNode startRef || range.End is not ReferenceNode endRef)
            return FormulaValue.FromError(CellError.Reference);

        var (sheetName, startLocal) = SplitSheet(startRef.Text);
        var (_, endLocal) = SplitSheet(endRef.Text);
        if (!CellReference.TryFromA1(startLocal, out var start) || !CellReference.TryFromA1(endLocal, out var end))
            return FormulaValue.FromError(CellError.Reference);

        var sheet1 = sheetName ?? sheet;
        var cellRange = new CellRange(start, end);
        var values = new List<FormulaValue>(cellRange.CellCount);
        values.AddRange(cellRange.Cells().Select(cell => ReadCell(sheet1, cell)));
        return FormulaValue.FromGrid(values, cellRange.RowCount, cellRange.ColumnCount);
    }

    private FormulaValue ReadCell(Worksheet worksheet, CellReference reference)
    {
        var cell = worksheet.GetCell(reference);
        if (cell is null)
            return FormulaValue.Blank;

        // Circular-reference guard keyed on sheet+cell.
        var key = $"{worksheet.Name}!{reference.ToA1()}";

        return cell.CellType switch
        {
            CellType.Number => FormulaValue.FromNumber(cell.GetDouble() ?? 0),
            CellType.Boolean => FormulaValue.FromBoolean(cell.GetBoolean() ?? false),
            CellType.String => FormulaValue.FromText(cell.GetString() ?? string.Empty),
            CellType.Error => FormulaValue.FromError(cell.GetError() ?? CellError.Value),
            CellType.Formula => EvaluateReferencedFormula(worksheet, cell, key),
            _ => FormulaValue.Blank
        };
    }

    private FormulaValue EvaluateReferencedFormula(Worksheet worksheet, XlsxCell cell, string key)
    {
        if (!_evaluating.Add(key))
            return FormulaValue.FromError(CellError.Reference); // circular

        try
        {
            var formula = cell.FormulaText;
            if (string.IsNullOrEmpty(formula))
                return FormulaValue.Blank;

            var evaluator = new FormulaEvaluator(worksheet, _evaluating);
            return evaluator.Evaluate(StripLeadingEquals(formula));
        }
        finally
        {
            _evaluating.Remove(key);
        }
    }

    // ── Operators ───────────────────────────────────────────────────────────────

    private FormulaValue EvaluateUnary(UnaryNode node)
    {
        var operand = Evaluate(node.Operand);
        return operand.IsError
            ? operand
            : node.Operator switch
            {
                "-" => FormulaValue.FromNumber(-ToNumber(operand)),
                "+" => FormulaValue.FromNumber(ToNumber(operand)),
                "%" => FormulaValue.FromNumber(ToNumber(operand) / 100.0),
                _ => FormulaValue.FromError(CellError.Value)
            };
    }

    private FormulaValue EvaluateBinary(BinaryNode node)
    {
        var left = Evaluate(node.Left);
        if (left.IsError) return left;

        var right = Evaluate(node.Right);
        if (right.IsError) return right;

        var op = node.Operator;
        switch (op)
        {
            case "&":
                return FormulaValue.FromText(ToText(left) + ToText(right));
            case "=" or "<>" or "<" or ">" or "<=" or ">=":
                return FormulaValue.FromBoolean(Compare(left, right, op));
        }

        // Arithmetic.
        var a = ToNumber(left);
        var b = ToNumber(right);
        return op switch
        {
            "+" => FormulaValue.FromNumber(a + b),
            "-" => FormulaValue.FromNumber(a - b),
            "*" => FormulaValue.FromNumber(a * b),
            "/" => b == 0 ? FormulaValue.FromError(CellError.DivisionByZero) : FormulaValue.FromNumber(a / b),
            "^" => FormulaValue.FromNumber(Math.Pow(a, b)),
            _ => FormulaValue.FromError(CellError.Value)
        };
    }

    private static bool Compare(FormulaValue left, FormulaValue right, string op)
    {
        int cmp;
        // Numeric comparison when both coerce to numbers; else ordinal text comparison.
        if (IsNumericish(left) && IsNumericish(right))
            cmp = ToNumber(left).CompareTo(ToNumber(right));
        else
            cmp = string.Compare(ToText(left), ToText(right), StringComparison.OrdinalIgnoreCase);

        return op switch
        {
            "=" => cmp == 0,
            "<>" => cmp != 0,
            "<" => cmp < 0,
            ">" => cmp > 0,
            "<=" => cmp <= 0,
            ">=" => cmp >= 0,
            _ => false
        };
    }

    private FormulaValue EvaluateFunction(FunctionNode node) =>
        FormulaFunctions.Invoke(node.Name, node.Arguments, this);

    // ── Coercion helpers ────────────────────────────────────────────────────────

    public static double ToNumber(FormulaValue value) => value.Kind switch
    {
        FormulaValueKind.Number => value.Number,
        FormulaValueKind.Boolean => value.Boolean ? 1 : 0,
        FormulaValueKind.Blank => 0,
        FormulaValueKind.Text => double.TryParse(value.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0,
        FormulaValueKind.Array => value.Flatten().Select(ToNumber).FirstOrDefault(),
        _ => 0
    };

    public static string ToText(FormulaValue value) => value.Kind switch
    {
        FormulaValueKind.Text => value.Text ?? string.Empty,
        FormulaValueKind.Number => value.Number.ToString("G15", CultureInfo.InvariantCulture),
        FormulaValueKind.Boolean => value.Boolean ? "TRUE" : "FALSE",
        FormulaValueKind.Blank => string.Empty,
        FormulaValueKind.Error => value.Error.ToLiteral(),
        _ => string.Empty
    };

    private static bool IsNumericish(FormulaValue value) =>
        value.Kind is FormulaValueKind.Number or FormulaValueKind.Boolean or FormulaValueKind.Blank;

    /// <summary>Coerces a value to a boolean using Excel's truthiness rules.</summary>
    public static bool ToBoolean(FormulaValue value) => value.Kind switch
    {
        FormulaValueKind.Boolean => value.Boolean,
        FormulaValueKind.Number => value.Number != 0,
        FormulaValueKind.Text => bool.TryParse(value.Text, out var b) && b,
        FormulaValueKind.Array => ToBoolean(value.Flatten().FirstOrDefault()),
        _ => false
    };

    /// <summary>
    ///     Splits a possibly sheet-qualified reference (<c>Sheet1!A1</c>, <c>'My Sheet'!A1</c>) into the
    ///     target worksheet (null = current sheet) and the local A1 part.
    /// </summary>
    private (Worksheet? sheet, string local) SplitSheet(string text)
    {
        var bang = text.LastIndexOf('!');
        if (bang < 0)
            return (null, text);

        var sheetName = text[..bang].Trim('\'');
        var local = text[(bang + 1)..];
        var sheet1 = sheet.Document.Sheets.Find(sheetName);
        return (sheet: sheet1, local);
    }

    private static string StripLeadingEquals(string formula) =>
        formula.StartsWith('=') ? formula[1..] : formula;
}
