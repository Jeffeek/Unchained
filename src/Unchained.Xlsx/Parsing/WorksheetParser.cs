using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Cell;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Formulas;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.SharedStrings;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.Parsing;

/// <summary>
///     Parses the <c>&lt;sheetData&gt;</c> rows and cells of a worksheet into the sparse
///     <see cref="RowCollection" /> model. Defensive about missing reference attributes, as
///     real-world producers (Google Sheets, LibreOffice) often omit them.
/// </summary>
internal static class WorksheetParser
{
    public static void ParseCells(Worksheet sheet, XElement root, RowCollection rows)
    {
        var sheetData = root.Child(SmlNames.SheetData);
        if (sheetData == null)
            return;

        var sharedStrings = sheet.Document.SharedStrings;
        var fallbackRow = 0;

        // Shared-formula masters by si index, captured as (master cell, master formula text).
        var sharedMasters = new Dictionary<int, (CellReference Origin, string Formula)>();

        foreach (var rowElement in sheetData.Children(SmlNames.Row))
        {
            fallbackRow++;
            var rowNumber = rowElement.GetAttrInt("r") ?? fallbackRow;
            fallbackRow = rowNumber;

            var row = rows.GetOrCreateRow(rowNumber);
            ReadRowProperties(rowElement, row);

            var fallbackColumn = 0;
            foreach (var cellElement in rowElement.Children(SmlNames.Cell))
            {
                fallbackColumn++;
                var reference = ResolveCellReference(cellElement, rowNumber, ref fallbackColumn);
                var cell = new Cell.Cell(sheet, reference)
                {
                    StyleIndex = cellElement.GetAttrInt("s", 0)
                };

                ReadCellValue(cellElement, cell, sharedStrings, sharedMasters);
                row.AddCell(cell);
            }
        }
    }

    private static void ReadRowProperties(XElement rowElement, Row row)
    {
        var height = rowElement.GetAttrDouble("ht");
        if (height.HasValue)
            row.Height = height;
        row.IsCustomHeight = rowElement.GetAttrBool("customHeight") == true;
        row.IsHidden = rowElement.GetAttrBool("hidden") == true;
        row.IsCollapsed = rowElement.GetAttrBool("collapsed") == true;
        row.OutlineLevel = rowElement.GetAttrInt("outlineLevel", 0);
        var style = rowElement.GetAttrInt("s");
        if (rowElement.GetAttrBool("customFormat") == true && style.HasValue)
            row.StyleIndex = style;
    }

    private static CellReference ResolveCellReference(XElement cellElement, int rowNumber, ref int fallbackColumn)
    {
        var raw = cellElement.GetAttr("r");
        if (raw == null || !CellReference.TryFromA1(raw, out var parsed))
            return new CellReference(rowNumber, fallbackColumn);

        fallbackColumn = parsed.Column;
        return parsed;
    }

    private static void ReadCellValue(
        XElement cellElement,
        Cell.Cell cell,
        SharedStringsTable sharedStrings,
        IDictionary<int, (CellReference Origin, string Formula)> sharedMasters
    )
    {
        var type = cellElement.GetAttr("t");
        var formulaElement = cellElement.Child(SmlNames.Formula);
        var valueElement = cellElement.Child(SmlNames.CellValue);

        if (formulaElement != null)
        {
            // ReSharper disable once BadListLineBreaks
            ReadFormulaCell(
                cell,
                formulaElement,
                valueElement,
                type,
                sharedStrings,
                sharedMasters
            );
            return;
        }

        switch (type)
        {
            case "s":
            {
                var index = ParseInt(valueElement?.Value);
                cell.SetStringInternal(sharedStrings.Get(index) ?? string.Empty);
                break;
            }
            case "inlineStr":
            {
                var inline = cellElement.Child(SmlNames.InlineString);
                cell.SetStringInternal(ExtractInlineText(inline));
                break;
            }
            case "str":
                cell.SetStringInternal(valueElement?.Value ?? string.Empty);
            break;
            case "b":
                cell.SetBooleanInternal(valueElement?.Value == "1");
            break;
            case "e":
                cell.SetErrorInternal(CellErrorExtensions.FromLiteral(valueElement?.Value) ?? CellError.Value);
            break;
            default:
            {
                if (valueElement != null)
                    cell.SetNumberInternal(ParseDouble(valueElement.Value));
                break;
            }
        }
    }

    private static void ReadFormulaCell(
        Cell.Cell cell,
        XElement formulaElement,
        XElement? valueElement,
        string? type,
        SharedStringsTable sharedStrings,
        IDictionary<int, (CellReference Origin, string Formula)> sharedMasters
    )
    {
        cell.CellType = CellType.Formula;

        var formulaType = formulaElement.GetAttr("t");
        if (formulaType == "array")
        {
            cell.IsArrayFormula = true;
            var refAttr = formulaElement.GetAttr("ref");
            if (refAttr != null)
                cell.ArrayFormulaRange = CellRange.FromA1(refAttr);
        }

        var text = formulaElement.Value;

        if (formulaType == "shared")
        {
            var si = formulaElement.GetAttrInt("si");
            if (!string.IsNullOrEmpty(text))
            {
                // Master cell — record its formula for continuations to expand against.
                if (si.HasValue)
                    sharedMasters[si.Value] = (cell.Reference, text);
                cell.Formula = text;
            }
            else if (si.HasValue && sharedMasters.TryGetValue(si.Value, out var master))
            {
                // Continuation cell — shift the master formula by the row/column offset.
                var rowDelta = cell.Reference.Row - master.Origin.Row;
                var colDelta = cell.Reference.Column - master.Origin.Column;
                cell.Formula = FormulaShifter.ShiftRelative(master.Formula, rowDelta, colDelta);
            }
            else
                cell.Formula = string.Empty;
        }
        else
            cell.Formula = string.IsNullOrEmpty(text) ? string.Empty : text;

        // Cached result: store so NumberValue / StringValue work without evaluation.
        if (valueElement == null)
            return;

        switch (type)
        {
            case "str":
                cell.Text = valueElement.Value;
            break;
            case "b":
                cell.Number = valueElement.Value == "1" ? 1 : 0;
            break;
            case "e":
                cell.Error = CellErrorExtensions.FromLiteral(valueElement.Value);
            break;
            case "s":
                cell.Text = sharedStrings.Get(ParseInt(valueElement.Value)) ?? string.Empty;
            break;
            default:
                cell.Number = ParseDouble(valueElement.Value);
            break;
        }
    }

    private static string ExtractInlineText(XElement? inlineString)
    {
        if (inlineString == null)
            return string.Empty;

        var direct = inlineString.Child(SmlNames.Text);
        return direct != null && !inlineString.Children(SmlNames.RichRun).Any()
            ? direct.Value
            : string
                .Concat(
                    inlineString.Children(SmlNames.RichRun)
                        .Select(static r => r.Child(SmlNames.Text)?.Value ?? string.Empty)
                );
    }

    private static int ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static double ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;
}
