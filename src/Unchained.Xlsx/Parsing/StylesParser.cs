using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Styles;

namespace Unchained.Xlsx.Parsing;

/// <summary>
///     Parses <c>xl/styles.xml</c> into a <see cref="StyleBook" />: the number-format, font, fill,
///     border, <c>cellStyleXfs</c>, <c>cellXfs</c>, and <c>cellStyles</c> tables.
/// </summary>
internal static class StylesParser
{
    public static StyleBook Parse(byte[] bytes)
    {
        var book = new StyleBook();
        var root = OoXmlHelper.ParseXml(bytes).Root;
        if (root == null)
            return StyleBook.CreateDefault();

        ReadNumberFormats(root, book);
        ReadFonts(root, book);
        ReadFills(root, book);
        ReadBorders(root, book);
        ReadXfs(root.Child(SmlNames.CellStyleXfs), book, isCellStyle: true);
        ReadXfs(root.Child(SmlNames.CellXfs), book, isCellStyle: false);
        ReadNamedStyles(root, book);

        book.RebuildLookups();
        return book;
    }

    private static void ReadNumberFormats(XElement root, StyleBook book)
    {
        var numFmts = root.Child(SmlNames.NumFmts);
        if (numFmts == null)
            return;

        foreach (var numFmt in numFmts.Children(SmlNames.NumFmt))
        {
            var id = numFmt.GetAttrInt("numFmtId");
            var code = numFmt.GetAttr("formatCode");
            if (id.HasValue && code != null)
                book.AddNumberFormatRaw(new NumberFormat(id.Value, code));
        }
    }

    private static void ReadFonts(XElement root, StyleBook book)
    {
        var fonts = root.Child(SmlNames.Fonts);
        if (fonts == null)
            return;

        foreach (var fontElement in fonts.Children(SmlNames.Font))
            book.AddFontRaw(ReadFont(fontElement));
    }

    private static CellFont ReadFont(XElement element)
    {
        var font = new CellFont
        {
            Bold = element.Child(SmlNames.FontBold) != null,
            Italic = element.Child(SmlNames.FontItalic) != null,
            Strikethrough = element.Child(SmlNames.FontStrike) != null,
            Outline = element.Child(SmlNames.FontOutline) != null,
            Shadow = element.Child(SmlNames.FontShadow) != null,
            Condense = element.Child(SmlNames.FontCondense) != null,
            Extend = element.Child(SmlNames.FontExtend) != null,
            SizePoints = element.Child(SmlNames.FontSize)?.GetAttrDouble(DmlNames.AttributeValue) ?? 11,
            Name = element.Child(SmlNames.FontName)?.GetAttr(DmlNames.AttributeValue) ?? "Calibri",
            Scheme = element.Child(SmlNames.FontScheme)?.GetAttr(DmlNames.AttributeValue),
            Color = SmlColor.FromHexArgb(element.Child(SmlNames.Color)?.GetAttr("rgb"))
        };

        var underline = element.Child(SmlNames.FontUnderline);
        if (underline != null)
            font.Underline = SmlEnums.ParseUnderline(underline.GetAttr(DmlNames.AttributeValue));

        var vertAlign = element.Child(SmlNames.FontVertAlign);
        if (vertAlign != null)
            font.VerticalAlignment = SmlEnums.ParseFontVerticalAlignment(vertAlign.GetAttr(DmlNames.AttributeValue));

        return font;
    }

    private static void ReadFills(XElement root, StyleBook book)
    {
        var fills = root.Child(SmlNames.Fills);
        if (fills == null)
            return;

        foreach (var fillElement in fills.Children(SmlNames.Fill))
            book.AddFillRaw(ReadFill(fillElement));
    }

    private static CellFill ReadFill(XElement element)
    {
        var patternFill = element.Child(SmlNames.PatternFill);
        return patternFill == null
            ? new CellFill()
            : new CellFill
            {
                PatternType = SmlEnums.ParseFillPattern(patternFill.GetAttr("patternType")),
                ForegroundColor = SmlColor.FromHexArgb(patternFill.Child(SmlNames.FgColor)?.GetAttr("rgb")),
                BackgroundColor = SmlColor.FromHexArgb(patternFill.Child(SmlNames.BgColor)?.GetAttr("rgb"))
            };
    }

    private static void ReadBorders(XElement root, StyleBook book)
    {
        var borders = root.Child(SmlNames.Borders);
        if (borders == null)
            return;

        foreach (var borderElement in borders.Children(SmlNames.Border))
            book.AddBorderRaw(ReadBorder(borderElement));
    }

    private static CellBorder ReadBorder(XElement element) =>
        new()
        {
            Left = ReadEdge(element.Child(SmlNames.Left)),
            Right = ReadEdge(element.Child(SmlNames.Right)),
            Top = ReadEdge(element.Child(SmlNames.Top)),
            Bottom = ReadEdge(element.Child(SmlNames.Bottom)),
            Diagonal = ReadEdge(element.Child(SmlNames.Diagonal)),
            DiagonalUp = element.GetAttrBool("diagonalUp") == true,
            DiagonalDown = element.GetAttrBool("diagonalDown") == true
        };

    private static BorderLine ReadEdge(XElement? edge) =>
        edge == null
            ? BorderLine.None
            : new BorderLine
            {
                Style = SmlEnums.ParseBorderStyle(edge.GetAttr("style")),
                Color = SmlColor.FromHexArgb(edge.Child(SmlNames.Color)?.GetAttr("rgb"))
            };

    private static void ReadXfs(XElement? table, StyleBook book, bool isCellStyle)
    {
        if (table == null)
            return;

        foreach (var xf in table.Children(SmlNames.Xf).Select(ReadXf))
        {
            if (isCellStyle)
                book.AddCellStyleXfRaw(xf);
            else
                book.AddCellXfRaw(xf);
        }
    }

    private static CellXf ReadXf(XElement element)
    {
        var xf = new CellXf
        {
            NumberFormatId = element.GetAttrInt("numFmtId", 0),
            FontId = element.GetAttrInt("fontId", 0),
            FillId = element.GetAttrInt("fillId", 0),
            BorderId = element.GetAttrInt("borderId", 0),
            XfId = element.GetAttrInt("xfId", 0),
            ApplyNumberFormat = element.GetAttrBool("applyNumberFormat") == true,
            ApplyFont = element.GetAttrBool("applyFont") == true,
            ApplyFill = element.GetAttrBool("applyFill") == true,
            ApplyBorder = element.GetAttrBool("applyBorder") == true,
            ApplyAlignment = element.GetAttrBool("applyAlignment") == true
        };

        var alignment = element.Child(SmlNames.Alignment);
        if (alignment != null)
            xf.Alignment = ReadAlignment(alignment);

        return xf;
    }

    private static CellAlignment ReadAlignment(XElement element) =>
        new()
        {
            Horizontal = SmlEnums.ParseHorizontal(element.GetAttr("horizontal")),
            Vertical = SmlEnums.ParseVertical(element.GetAttr("vertical")),
            WrapText = element.GetAttrBool("wrapText") == true,
            ShrinkToFit = element.GetAttrBool("shrinkToFit") == true,
            TextRotation = element.GetAttrInt("textRotation", 0),
            Indent = element.GetAttrInt("indent", 0),
            ReadingOrder = SmlEnums.ParseReadingOrder(element.GetAttrInt("readingOrder")),
            JustifyLastLine = element.GetAttrBool("justifyLastLine") == true
        };

    private static void ReadNamedStyles(XElement root, StyleBook book)
    {
        var cellStyles = root.Child(SmlNames.CellStyles);
        if (cellStyles == null)
            return;

        foreach (var styleElement in cellStyles.Children(SmlNames.CellStyle))
        {
            var name = styleElement.GetAttr("name") ?? "Normal";
            var xfId = styleElement.GetAttrInt("xfId", 0);
            var builtInId = styleElement.GetAttrInt("builtinId") ?? -1;
            book.AddNamedStyleRaw(new NamedCellStyle(name, xfId, builtInId));
        }
    }
}
