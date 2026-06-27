using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Cell;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.Writing;

internal static partial class WorksheetWriter
{
    /// <summary>
    ///     Rewrites <c>&lt;sheetData&gt;</c> and <c>&lt;cols&gt;</c> from the materialised model. When the
    ///     sheet's cells were never accessed, the preserved raw elements are left untouched.
    /// </summary>
    private static void RewriteSheetData(Worksheet sheet, XElement root)
    {
        if (sheet.ColumnsMaterialised)
            RewriteColumns(sheet, root);

        if (sheet.MergedCellsMaterialised)
            RewriteMergedCells(sheet, root);

        if (sheet.DataValidationsMaterialised)
            RewriteDataValidations(sheet, root);

        if (!sheet.CellsMaterialised)
            return;

        var sharedStrings = sheet.Document.SharedStrings;

        var sheetData = new XElement(SmlNames.SheetData);
        foreach (var row in sheet.RowsInternal.AllRows.OrderBy(static r => r.RowNumber))
        {
            if (row.IsEffectivelyEmpty)
                continue;

            sheetData.Add(BuildRow(row, sharedStrings));
        }

        root.Child(SmlNames.SheetData)?.ReplaceWith(sheetData);
        if (root.Child(SmlNames.SheetData) == null)
            InsertSheetData(root, sheetData);

        UpdateDimension(root, sheet);
    }

    private static XElement BuildRow(Row row, SharedStrings.SharedStringsTable sharedStrings)
    {
        var rowElement = new XElement(SmlNames.Row,
            new XAttribute("r", row.RowNumber.ToString(CultureInfo.InvariantCulture)));

        if (row.Height.HasValue)
        {
            rowElement.SetAttributeValue("ht", row.Height.Value.ToString(CultureInfo.InvariantCulture));
            if (row.IsCustomHeight)
                rowElement.SetAttributeValue("customHeight", "1");
        }

        if (row.IsHidden) rowElement.SetAttributeValue("hidden", "1");
        if (row.IsCollapsed) rowElement.SetAttributeValue("collapsed", "1");
        if (row.OutlineLevel > 0) rowElement.SetAttributeValue("outlineLevel", row.OutlineLevel.ToString(CultureInfo.InvariantCulture));
        if (row.StyleIndex is { } style)
        {
            rowElement.SetAttributeValue("s", style.ToString(CultureInfo.InvariantCulture));
            rowElement.SetAttributeValue("customFormat", "1");
        }

        foreach (var cell in row.CellsInternal.OrderBy(static c => c.Column))
        {
            if (cell.IsEffectivelyEmpty)
                continue;

            rowElement.Add(BuildCell(cell, sharedStrings));
        }

        return rowElement;
    }

    private static XElement BuildCell(Cell.Cell cell, SharedStrings.SharedStringsTable sharedStrings)
    {
        var element = new XElement(SmlNames.Cell, new XAttribute("r", cell.Reference.ToA1()));

        if (cell.StyleIndex != 0)
            element.SetAttributeValue("s", cell.StyleIndex.ToString(CultureInfo.InvariantCulture));

        // Formula cells: emit <f> plus optional cached <v>.
        if (cell.CellType == CellType.Formula)
        {
            WriteFormula(element, cell, sharedStrings);
            return element;
        }

        switch (cell.CellType)
        {
            case CellType.Number:
                element.Add(new XElement(SmlNames.CellValue, Num(cell.Number)));
                break;
            case CellType.String:
            {
                var index = sharedStrings.GetOrAdd(cell.Text ?? string.Empty);
                element.SetAttributeValue("t", "s");
                element.Add(new XElement(SmlNames.CellValue, index.ToString(CultureInfo.InvariantCulture)));
                break;
            }
            case CellType.Boolean:
                element.SetAttributeValue("t", "b");
                element.Add(new XElement(SmlNames.CellValue, cell.Number != 0 ? "1" : "0"));
                break;
            case CellType.Error:
                element.SetAttributeValue("t", "e");
                element.Add(new XElement(SmlNames.CellValue, (cell.Error ?? CellError.Value).ToLiteral()));
                break;
            case CellType.Empty:
            case CellType.Formula:
            default:
                break;
        }

        return element;
    }

    private static void WriteFormula(XElement element, Cell.Cell cell, SharedStrings.SharedStringsTable sharedStrings)
    {
        var formula = new XElement(SmlNames.Formula, cell.Formula ?? string.Empty);
        if (cell is { IsArrayFormula: true, ArrayFormulaRange: { } range })
        {
            formula.SetAttributeValue("t", "array");
            formula.SetAttributeValue("ref", range.ToA1());
        }

        element.Add(formula);

        // Cached result, written so consumers see a value before recalculation.
        if (cell.Text != null)
        {
            element.SetAttributeValue("t", "str");
            element.Add(new XElement(SmlNames.CellValue, cell.Text));
        }
        else if (cell.Error != null)
        {
            element.SetAttributeValue("t", "e");
            element.Add(new XElement(SmlNames.CellValue, cell.Error.Value.ToLiteral()));
        }
        else
            element.Add(new XElement(SmlNames.CellValue, Num(cell.Number)));
    }

    private static void RewriteColumns(Worksheet sheet, XElement root)
    {
        root.Child(SmlNames.Cols)?.Remove();

        var ordered = sheet.ColumnsInternal.Ordered.ToList();
        if (ordered.Count == 0)
            return;

        var cols = new XElement(SmlNames.Cols);
        foreach (var column in ordered)
        {
            var colElement = new XElement(SmlNames.Col,
                new XAttribute("min", column.Min.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("max", column.Max.ToString(CultureInfo.InvariantCulture)));

            if (column.Width.HasValue)
            {
                colElement.SetAttributeValue("width", column.Width.Value.ToString(CultureInfo.InvariantCulture));
                if (column.IsCustomWidth)
                    colElement.SetAttributeValue("customWidth", "1");
            }

            if (column.IsHidden) colElement.SetAttributeValue("hidden", "1");
            if (column.IsCollapsed) colElement.SetAttributeValue("collapsed", "1");
            if (column.OutlineLevel > 0) colElement.SetAttributeValue("outlineLevel", column.OutlineLevel.ToString(CultureInfo.InvariantCulture));
            if (column.StyleIndex is { } style) colElement.SetAttributeValue("style", style.ToString(CultureInfo.InvariantCulture));

            cols.Add(colElement);
        }

        // <cols> must appear before <sheetData>.
        var sheetData = root.Child(SmlNames.SheetData);
        if (sheetData != null)
            sheetData.AddBeforeSelf(cols);
        else
            root.Add(cols);
    }

    private static void RewriteMergedCells(Worksheet sheet, XElement root)
    {
        root.Child(SmlNames.MergeCells)?.Remove();

        var ranges = sheet.MergedCellsInternal.ToList();
        if (ranges.Count == 0)
            return;

        var mergeCells = new XElement(SmlNames.MergeCells,
            new XAttribute("count", ranges.Count.ToString(CultureInfo.InvariantCulture)));
        foreach (var range in ranges)
            mergeCells.Add(new XElement(SmlNames.MergeCell, new XAttribute("ref", range.ToA1())));

        var sheetData = root.Child(SmlNames.SheetData);
        if (sheetData != null)
            sheetData.AddAfterSelf(mergeCells);
        else
            root.Add(mergeCells);
    }

    private static void RewriteDataValidations(Worksheet sheet, XElement root)
    {
        root.Child(SmlNames.DataValidations)?.Remove();

        if (sheet.DataValidationsInternal.Count == 0)
            return;

        var container = Worksheet.WriteValidations(sheet.DataValidationsInternal);

        // <dataValidations> appears after <mergeCells> / <sheetData>; place it after mergeCells when
        // present, otherwise after sheetData.
        var anchor = root.Child(SmlNames.MergeCells) ?? root.Child(SmlNames.SheetData);
        if (anchor != null)
            anchor.AddAfterSelf(container);
        else
            root.Add(container);
    }

    private static void InsertSheetData(XElement root, XElement sheetData)
    {
        var cols = root.Child(SmlNames.Cols);
        if (cols != null)
            cols.AddAfterSelf(sheetData);
        else
            root.Add(sheetData);
    }

    private static void UpdateDimension(XElement root, Worksheet sheet)
    {
        var used = sheet.GetUsedRange();
        var dimension = root.Child(SmlNames.Dimension);
        if (used is null)
        {
            dimension?.SetAttributeValue("ref", "A1");
            return;
        }

        if (dimension == null)
        {
            dimension = new XElement(SmlNames.Dimension, new XAttribute("ref", used.Value.ToA1()));
            root.AddFirst(dimension);
        }
        else
            dimension.SetAttributeValue("ref", used.Value.ToA1());
    }

    private static string Num(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);
}
