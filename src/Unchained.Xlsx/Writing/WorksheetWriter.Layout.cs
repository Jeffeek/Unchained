using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.PageSetup;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.Writing;

internal static partial class WorksheetWriter
{
    // The CT_Worksheet child element sequence (ECMA-376 §18.3.1.99), used to insert regenerated
    // elements at a schema-valid position.
    private static readonly string[] WorksheetChildOrder =
    [
        "sheetPr", "dimension", "sheetViews", "sheetFormatPr", "cols", "sheetData",
        "sheetCalcPr", "sheetProtection", "protectedRanges", "scenarios", "autoFilter",
        "sortState", "dataConsolidate", "customSheetViews", "mergeCells", "phoneticPr",
        "conditionalFormatting", "dataValidations", "hyperlinks", "printOptions",
        "pageMargins", "pageSetup", "headerFooter", "rowBreaks", "colBreaks",
        "customProperties", "cellWatches", "ignoredErrors", "smartTags", "drawing",
        "legacyDrawing", "legacyDrawingHF", "picture", "oleObjects", "controls",
        "webPublishItems", "tableParts", "extLst"
    ];

    private static void RewriteLayout(Worksheet sheet, XElement root)
    {
        if (sheet.LayoutMaterialised)
        {
            WriteView(sheet, root);
            WriteProtection(sheet, root);
            WriteAutoFilter(sheet, root);
            WriteMargins(sheet, root);
            WritePageSetup(sheet, root);
            WriteHeaderFooter(sheet, root);
        }

        RewriteTableParts(sheet, root);
        RewriteDrawing(sheet, root);
    }

    private static void RewriteDrawing(Worksheet sheet, XElement root)
    {
        if (!sheet.DrawingsMaterialised)
            return;

        root.Child(SmlNames.Drawing)?.Remove();

        if (sheet.DrawingsOrNull!.Count == 0 || string.IsNullOrEmpty(sheet.DrawingRelationshipId))
            return;

        InsertOrdered(
            root,
            new XElement(
                SmlNames.Drawing,
                new XAttribute(SmlNames.R + "id", sheet.DrawingRelationshipId)
            )
        );
    }

    private static void RewriteTableParts(Worksheet sheet, XElement root)
    {
        if (!sheet.TablesMaterialised)
            return;

        root.Child(SmlNames.TableParts)?.Remove();

        var tables = sheet.TablesOrNull!.All;
        if (tables.Count == 0)
            return;

        var tableParts = new XElement(
            SmlNames.TableParts,
            new XAttribute("count", tables.Count.ToString(CultureInfo.InvariantCulture))
        );
        foreach (var table in tables)
            tableParts.Add(new XElement(SmlNames.TablePart, new XAttribute(SmlNames.R + "id", table.RelationshipId)));

        InsertOrdered(root, tableParts);
    }

    private static void WriteView(Worksheet sheet, XElement root)
    {
        var view = sheet.ViewOrNull;
        if (view == null)
            return;

        root.Child(SmlNames.SheetViews)?.Remove();

        var sheetView = new XElement(
            SmlNames.SheetView,
            new XAttribute("workbookViewId", "0")
        );
        if (!view.ShowGridLines) sheetView.SetAttributeValue("showGridLines", "0");
        if (!view.ShowRowColHeaders) sheetView.SetAttributeValue("showRowColHeaders", "0");
        if (view.ShowFormulas) sheetView.SetAttributeValue("showFormulas", "1");
        if (view.ZoomScale != 100) sheetView.SetAttributeValue("zoomScale", view.ZoomScale.ToString(CultureInfo.InvariantCulture));

        if (view.FrozenPanes is { } frozen && (frozen.FrozenRows > 0 || frozen.FrozenColumns > 0))
        {
            var topLeft = new CellReference(frozen.FrozenRows + 1, frozen.FrozenColumns + 1).ToA1();
            var pane = new XElement(
                SmlNames.Pane,
                new XAttribute("state", "frozen"),
                new XAttribute("topLeftCell", topLeft)
            );
            if (frozen.FrozenColumns > 0) pane.SetAttributeValue("xSplit", frozen.FrozenColumns.ToString(CultureInfo.InvariantCulture));
            if (frozen.FrozenRows > 0) pane.SetAttributeValue("ySplit", frozen.FrozenRows.ToString(CultureInfo.InvariantCulture));
            sheetView.Add(pane);
        }

        if (view.ActiveCell is { } active)
            sheetView.Add(new XElement(SmlNames.Selection, new XAttribute("activeCell", active.ToA1()), new XAttribute("sqref", active.ToA1())));

        InsertOrdered(root, new XElement(SmlNames.SheetViews, sheetView));
    }

    private static void WriteProtection(Worksheet sheet, XElement root)
    {
        var protection = sheet.ProtectionOrNull;
        root.Child(SmlNames.SheetProtection)?.Remove();
        if (protection is not { IsProtected: true })
            return;

        var element = new XElement(SmlNames.SheetProtection, new XAttribute("sheet", "1"));
        if (protection.PasswordHash != null) element.SetAttributeValue("password", protection.PasswordHash);
        if (!protection.AllowSelectLockedCells) element.SetAttributeValue("selectLockedCells", "1");
        if (!protection.AllowSelectUnlockedCells) element.SetAttributeValue("selectUnlockedCells", "1");
        if (!protection.AllowFormatCells) element.SetAttributeValue("formatCells", "1");
        if (!protection.AllowInsertRows) element.SetAttributeValue("insertRows", "1");
        if (!protection.AllowInsertColumns) element.SetAttributeValue("insertColumns", "1");
        if (!protection.AllowDeleteRows) element.SetAttributeValue("deleteRows", "1");
        if (!protection.AllowDeleteColumns) element.SetAttributeValue("deleteColumns", "1");
        if (!protection.AllowSort) element.SetAttributeValue("sort", "1");
        if (!protection.AllowAutoFilter) element.SetAttributeValue("autoFilter", "1");

        InsertOrdered(root, element);
    }

    private static void WriteAutoFilter(Worksheet sheet, XElement root)
    {
        root.Child(SmlNames.AutoFilter)?.Remove();
        if (sheet.AutoFilterOrNull is not { } range)
            return;

        InsertOrdered(root, new XElement(SmlNames.AutoFilter, new XAttribute("ref", range.ToA1())));
    }

    private static void WriteMargins(Worksheet sheet, XElement root)
    {
        var margins = sheet.PageMarginsOrNull;
        if (margins == null)
            return;

        root.Child(SmlNames.PageMargins)?.Remove();
        InsertOrdered(
            root,
            new XElement(
                SmlNames.PageMargins,
                new XAttribute("left", margins.Left.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("right", margins.Right.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("top", margins.Top.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("bottom", margins.Bottom.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("header", margins.Header.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("footer", margins.Footer.ToString(CultureInfo.InvariantCulture))
            )
        );
    }

    private static void WritePageSetup(Worksheet sheet, XElement root)
    {
        var setup = sheet.PageSetupOrNull;
        if (setup == null)
            return;

        root.Child(SmlNames.PageSetup)?.Remove();
        var element = new XElement(SmlNames.PageSetup);
        if (setup.PaperSize > 0) element.SetAttributeValue("paperSize", setup.PaperSize.ToString(CultureInfo.InvariantCulture));
        if (setup.Scale > 0) element.SetAttributeValue("scale", setup.Scale.ToString(CultureInfo.InvariantCulture));
        if (setup.FitToWidth > 0) element.SetAttributeValue("fitToWidth", setup.FitToWidth.ToString(CultureInfo.InvariantCulture));
        if (setup.FitToHeight > 0) element.SetAttributeValue("fitToHeight", setup.FitToHeight.ToString(CultureInfo.InvariantCulture));
        if (setup.FirstPageNumber > 0)
        {
            element.SetAttributeValue("firstPageNumber", setup.FirstPageNumber.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue("useFirstPageNumber", "1");
        }

        if (setup.Orientation != PageOrientation.Default)
            element.SetAttributeValue("orientation", setup.Orientation == PageOrientation.Portrait ? "portrait" : "landscape");
        if (setup.BlackAndWhite) element.SetAttributeValue("blackAndWhite", "1");
        if (setup.Draft) element.SetAttributeValue("draft", "1");
        if (setup.PrintOrder == PrintOrder.OverThenDown) element.SetAttributeValue("pageOrder", "overThenDown");

        InsertOrdered(root, element);
    }

    private static void WriteHeaderFooter(Worksheet sheet, XElement root)
    {
        var hf = sheet.HeaderFooterOrNull;
        if (hf == null)
            return;

        root.Child(SmlNames.HeaderFooter)?.Remove();
        var element = new XElement(SmlNames.HeaderFooter);
        if (hf.DifferentFirstPage) element.SetAttributeValue("differentFirst", "1");
        if (hf.DifferentOddEven) element.SetAttributeValue("differentOddEven", "1");
        if (!hf.ScaleWithDocument) element.SetAttributeValue("scaleWithDoc", "0");
        if (!hf.AlignWithMargins) element.SetAttributeValue("alignWithMargins", "0");

        AddHeaderFooterChild(element, SmlNames.OddHeader, hf.OddHeader);
        AddHeaderFooterChild(element, SmlNames.OddFooter, hf.OddFooter);
        AddHeaderFooterChild(element, SmlNames.EvenHeader, hf.EvenHeader);
        AddHeaderFooterChild(element, SmlNames.EvenFooter, hf.EvenFooter);
        AddHeaderFooterChild(element, SmlNames.FirstHeader, hf.FirstPageHeader);
        AddHeaderFooterChild(element, SmlNames.FirstFooter, hf.FirstPageFooter);

        if (element.HasElements || element.HasAttributes)
            InsertOrdered(root, element);
    }

    private static void AddHeaderFooterChild(XContainer parent, XName name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            parent.Add(new XElement(name, value));
    }

    /// <summary>Inserts <paramref name="element" /> at the schema-correct position among the worksheet children.</summary>
    private static void InsertOrdered(XContainer root, XElement element)
    {
        var targetIndex = Array.IndexOf(WorksheetChildOrder, element.Name.LocalName);
        if (targetIndex < 0)
        {
            root.Add(element);
            return;
        }

        // Find the first existing child whose order index is greater than ours and insert before it.
        foreach (var existing in from existing in root.Elements()
                                 let existingIndex = Array.IndexOf(WorksheetChildOrder, existing.Name.LocalName)
                                 where existingIndex > targetIndex
                                 select existing)
        {
            existing.AddBeforeSelf(element);
            return;
        }

        root.Add(element);
    }
}
