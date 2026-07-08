using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.Writing;

/// <summary>
///     Serializes a single worksheet to its <c>&lt;worksheet&gt;</c> XML. For M1 this either writes
///     back the preserved raw element (updating only the tab colour) or emits a minimal empty sheet.
///     Later milestones rewrite the <c>&lt;sheetData&gt;</c>, columns, and merged cells from the model.
/// </summary>
internal static partial class WorksheetWriter
{
    public static byte[] Write(Worksheet sheet)
    {
        var root = sheet.RawElement != null
            ? new XElement(sheet.RawElement)
            : CreateEmptyWorksheet();

        ApplyTabColor(sheet, root);
        RewriteSheetData(sheet, root);
        RewriteLayout(sheet, root);

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).ToUtf8Bytes();
    }

    private static XElement CreateEmptyWorksheet() =>
        new(
            SmlNames.Worksheet,
            new XAttribute("xmlns", SmlNames.X.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "r", SmlNames.R.NamespaceName),
            new XElement(SmlNames.SheetData)
        );

    private static void ApplyTabColor(Worksheet sheet, XElement root)
    {
        // sheetPr must be the first child element of worksheet when present.
        var existing = root.Child(SmlNames.SheetPr);

        if (sheet.TabColor is null)
        {
            existing?.Child(SmlNames.TabColor)?.Remove();
            return;
        }

        var sheetPr = existing;
        if (sheetPr == null)
        {
            sheetPr = new XElement(SmlNames.SheetPr);
            root.AddFirst(sheetPr);
        }

        sheetPr.Child(SmlNames.TabColor)?.Remove();
        sheetPr.AddFirst(new XElement(SmlNames.TabColor, new XAttribute(SmlNames.AttrRgb, SmlColor.ToHexArgb(sheet.TabColor.Value))));
    }
}
