using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using XlsxCell = Unchained.Xlsx.Cell.Cell;

namespace Unchained.Xlsx.Formulas;

/// <summary>
///     Drives evaluation of every formula cell in a workbook, storing each computed result as the
///     cell's cached value (so <see cref="Cell.Cell.GetDouble" /> / <c>GetString</c> reflect it without
///     a spreadsheet application). Unlike <c>RecalculateAll</c> (which only sets a flag for Excel to
///     recompute on open), this actually evaluates formulas in-engine.
/// </summary>
internal static class FormulaCalculator
{
    public static int Recalculate(SpreadsheetDocument document)
    {
        var evaluated = 0;
        foreach (var sheet in document.Sheets)
        {
            foreach (var row in sheet.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    if (cell.CellType != CellType.Formula || string.IsNullOrEmpty(cell.FormulaText))
                        continue;

                    var evaluator = new FormulaEvaluator(sheet);
                    var result = evaluator.Evaluate(StripEquals(cell.FormulaText));
                    Store(cell, result);
                    evaluated++;
                }
            }
        }

        return evaluated;
    }

    private static void Store(XlsxCell cell, FormulaValue value)
    {
        while (true)
        {
            switch (value.Kind)
            {
                case FormulaValueKind.Number:
                case FormulaValueKind.Blank:
                    cell.SetFormulaCachedNumber(value.Kind == FormulaValueKind.Blank ? 0 : value.Number);
                break;
                case FormulaValueKind.Boolean:
                    cell.SetFormulaCachedNumber(value.Boolean ? 1 : 0);
                break;
                case FormulaValueKind.Text:
                    cell.SetFormulaCachedText(value.Text ?? string.Empty);
                break;
                case FormulaValueKind.Error:
                    cell.SetFormulaCachedError(value.Error);
                break;
                case FormulaValueKind.Array:
                    // A formula that yields an array caches its top-left scalar (non-spilled behaviour).
                    var first = value.Flatten().FirstOrDefault();
                    value = first.Kind == FormulaValueKind.Array ? FormulaValue.Blank : first;
                    continue;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            break;
        }
    }

    private static string StripEquals(string formula) => formula.StartsWith('=') ? formula[1..] : formula;
}
