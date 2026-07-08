using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.PageSetup;
using Unchained.Xlsx.PageSetup;
using Unchained.Xlsx.Security;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.Parsing;

/// <summary>
///     Parses the layout-related elements of a worksheet — sheet views (grid lines, zoom, frozen
///     panes), sheet protection, page setup, margins, header/footer, and auto-filter.
/// </summary>
internal static class WorksheetLayoutParser
{
    public static void Parse(Worksheet sheet, XElement root)
    {
        ParseView(sheet, root);
        ParseProtection(sheet, root);
        ParsePageSetup(sheet, root);
        ParseMargins(sheet, root);
        ParseHeaderFooter(sheet, root);
        ParseAutoFilter(sheet, root);
    }

    private static void ParseView(Worksheet sheet, XElement root)
    {
        var sheetView = root.Child(SmlNames.SheetViews)?.Child(SmlNames.SheetView);
        if (sheetView == null)
            return;

        var view = new SheetView
        {
            ShowGridLines = sheetView.GetAttrBool("showGridLines") != false,
            ShowRowColHeaders = sheetView.GetAttrBool("showRowColHeaders") != false,
            ShowFormulas = sheetView.GetAttrBool("showFormulas") == true,
            ZoomScale = sheetView.GetAttrInt("zoomScale", 100)
        };

        var selection = sheetView.Child(SmlNames.Selection);
        var activeCell = selection?.GetAttr("activeCell");
        if (activeCell != null && CellReference.TryFromA1(activeCell, out var reference))
            view.ActiveCell = reference;

        var pane = sheetView.Child(SmlNames.Pane);
        if (pane?.GetAttr("state") == "frozen")
        {
            var xSplit = pane.GetAttrInt("xSplit", 0);
            var ySplit = pane.GetAttrInt("ySplit", 0);
            view.FrozenPanes = new FrozenPanes(ySplit, xSplit);
        }

        sheet.SetParsedView(view);
    }

    private static void ParseProtection(Worksheet sheet, XElement root)
    {
        var element = root.Child(SmlNames.SheetProtection);
        if (element == null)
            return;

        var protection = new SheetProtection
        {
            IsProtected = element.GetAttrBool(SmlNames.AttrSheet) == true,
            PasswordHash = element.GetAttr("password"),
            AllowSelectLockedCells = element.GetAttrBool("selectLockedCells") != true,
            AllowSelectUnlockedCells = element.GetAttrBool("selectUnlockedCells") != true,
            AllowFormatCells = element.GetAttrBool("formatCells") != true,
            AllowInsertRows = element.GetAttrBool("insertRows") != true,
            AllowInsertColumns = element.GetAttrBool("insertColumns") != true,
            AllowDeleteRows = element.GetAttrBool("deleteRows") != true,
            AllowDeleteColumns = element.GetAttrBool("deleteColumns") != true,
            AllowSort = element.GetAttrBool("sort") != true,
            AllowAutoFilter = element.GetAttrBool("autoFilter") != true
        };

        sheet.SetParsedProtection(protection);
    }

    private static void ParsePageSetup(Worksheet sheet, XElement root)
    {
        var element = root.Child(SmlNames.PageSetup);
        if (element == null)
            return;

        sheet.SetParsedPageSetup(
            new PageSetup.PageSetup
            {
                PaperSize = element.GetAttrInt("paperSize", 0),
                Orientation = element.GetAttr("orientation") switch
                {
                    "portrait" => PageOrientation.Portrait,
                    "landscape" => PageOrientation.Landscape,
                    _ => PageOrientation.Default
                },
                Scale = element.GetAttrInt("scale", 0),
                FitToWidth = element.GetAttrInt("fitToWidth", 0),
                FitToHeight = element.GetAttrInt("fitToHeight", 0),
                FirstPageNumber = element.GetAttrInt("firstPageNumber", 0),
                BlackAndWhite = element.GetAttrBool("blackAndWhite") == true,
                Draft = element.GetAttrBool("draft") == true,
                PrintOrder = element.GetAttr("pageOrder") == "overThenDown" ? PrintOrder.OverThenDown : PrintOrder.DownThenOver
            }
        );
    }

    private static void ParseMargins(Worksheet sheet, XElement root)
    {
        var element = root.Child(SmlNames.PageMargins);
        if (element == null)
            return;

        sheet.SetParsedPageMargins(
            new PageMargins
            {
                Left = element.GetAttrDouble("left") ?? 0.7,
                Right = element.GetAttrDouble("right") ?? 0.7,
                Top = element.GetAttrDouble("top") ?? 0.75,
                Bottom = element.GetAttrDouble("bottom") ?? 0.75,
                Header = element.GetAttrDouble("header") ?? 0.3,
                Footer = element.GetAttrDouble("footer") ?? 0.3
            }
        );
    }

    private static void ParseHeaderFooter(Worksheet sheet, XElement root)
    {
        var element = root.Child(SmlNames.HeaderFooter);
        if (element == null)
            return;

        sheet.SetParsedHeaderFooter(
            new HeaderFooterSetup
            {
                DifferentFirstPage = element.GetAttrBool("differentFirst") == true,
                DifferentOddEven = element.GetAttrBool("differentOddEven") == true,
                ScaleWithDocument = element.GetAttrBool("scaleWithDoc") != false,
                AlignWithMargins = element.GetAttrBool("alignWithMargins") != false,
                OddHeader = element.Child(SmlNames.OddHeader)?.Value,
                OddFooter = element.Child(SmlNames.OddFooter)?.Value,
                EvenHeader = element.Child(SmlNames.EvenHeader)?.Value,
                EvenFooter = element.Child(SmlNames.EvenFooter)?.Value,
                FirstPageHeader = element.Child(SmlNames.FirstHeader)?.Value,
                FirstPageFooter = element.Child(SmlNames.FirstFooter)?.Value
            }
        );
    }

    private static void ParseAutoFilter(Worksheet sheet, XElement root)
    {
        var reference = root.Child(SmlNames.AutoFilter)?.GetAttr("ref");
        if (reference != null)
            sheet.SetParsedAutoFilter(CellRange.FromA1(reference));
    }
}
