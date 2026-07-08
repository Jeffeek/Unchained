using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Styles;

namespace Unchained.Xlsx.Writing;

/// <summary>Serializes a <see cref="StyleBook" /> to <c>xl/styles.xml</c>.</summary>
internal static class StylesWriter
{
    public static byte[] Write(StyleBook book)
    {
        var root = new XElement(
            SmlNames.StyleSheet,
            new XAttribute("xmlns", SmlNames.X.NamespaceName)
        );

        WriteNumberFormats(root, book);
        root.Add(WriteFonts(book));
        root.Add(WriteFills(book));
        root.Add(WriteBorders(book));
        root.Add(WriteXfTable(SmlNames.CellStyleXfs, book.CellStyleXfs));
        root.Add(WriteXfTable(SmlNames.CellXfs, book.CellXfs));
        WriteNamedStyles(root, book);

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).ToUtf8Bytes();
    }

    private static void WriteNumberFormats(XContainer root, StyleBook book)
    {
        var custom = book.NumberFormats.Where(static f => !f.IsBuiltIn).ToList();
        if (custom.Count == 0)
            return;

        var numFmts = new XElement(SmlNames.NumFmts, new XAttribute("count", custom.Count));
        foreach (var format in custom)
        {
            numFmts.Add(
                new XElement(
                    SmlNames.NumFmt,
                    new XAttribute("numFmtId", format.FormatId.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("formatCode", format.FormatCode)
                )
            );
        }

        root.Add(numFmts);
    }

    private static XElement WriteFonts(StyleBook book)
    {
        var fonts = new XElement(SmlNames.Fonts, new XAttribute("count", book.Fonts.Count));
        foreach (var font in book.Fonts)
            fonts.Add(WriteFont(font));
        return fonts;
    }

    private static XElement WriteFont(CellFont font)
    {
        var element = new XElement(SmlNames.Font);
        if (font.Bold) element.Add(new XElement(SmlNames.FontBold));
        if (font.Italic) element.Add(new XElement(SmlNames.FontItalic));
        if (font.Strikethrough) element.Add(new XElement(SmlNames.FontStrike));
        if (font.Outline) element.Add(new XElement(SmlNames.FontOutline));
        if (font.Shadow) element.Add(new XElement(SmlNames.FontShadow));
        if (font.Condense) element.Add(new XElement(SmlNames.FontCondense));
        if (font.Extend) element.Add(new XElement(SmlNames.FontExtend));

        if (font.Underline != FontUnderline.None)
        {
            var underline = new XElement(SmlNames.FontUnderline);
            var literal = SmlEnums.ToLiteral(font.Underline);
            if (literal is not null and not "single")
                underline.SetAttributeValue(DmlNames.AttributeValue, literal);
            element.Add(underline);
        }

        if (font.VerticalAlignment != FontVerticalAlignment.None)
        {
            element.Add(
                new XElement(
                    SmlNames.FontVertAlign,
                    new XAttribute(DmlNames.AttributeValue, SmlEnums.ToLiteral(font.VerticalAlignment)!)
                )
            );
        }

        element.Add(
            new XElement(
                SmlNames.FontSize,
                new XAttribute(DmlNames.AttributeValue, font.SizePoints.ToString(CultureInfo.InvariantCulture))
            )
        );

        if (font.Color is { } color)
            element.Add(new XElement(SmlNames.Color, new XAttribute(SmlNames.AttrRgb, SmlColor.ToHexArgb(color))));

        element.Add(new XElement(SmlNames.FontName, new XAttribute(DmlNames.AttributeValue, font.Name)));

        if (font.Scheme != null)
            element.Add(new XElement(SmlNames.FontScheme, new XAttribute(DmlNames.AttributeValue, font.Scheme)));

        return element;
    }

    private static XElement WriteFills(StyleBook book)
    {
        var fills = new XElement(SmlNames.Fills, new XAttribute("count", book.Fills.Count));
        foreach (var fill in book.Fills)
            fills.Add(WriteFill(fill));
        return fills;
    }

    private static XElement WriteFill(CellFill fill)
    {
        var patternFill = new XElement(
            SmlNames.PatternFill,
            new XAttribute("patternType", SmlEnums.ToLiteral(fill.PatternType))
        );

        if (fill.ForegroundColor is { } fg)
            patternFill.Add(new XElement(SmlNames.FgColor, new XAttribute(SmlNames.AttrRgb, SmlColor.ToHexArgb(fg))));
        if (fill.BackgroundColor is { } bg)
            patternFill.Add(new XElement(SmlNames.BgColor, new XAttribute(SmlNames.AttrRgb, SmlColor.ToHexArgb(bg))));

        return new XElement(SmlNames.Fill, patternFill);
    }

    private static XElement WriteBorders(StyleBook book)
    {
        var borders = new XElement(SmlNames.Borders, new XAttribute("count", book.Borders.Count));
        foreach (var border in book.Borders)
            borders.Add(WriteBorder(border));
        return borders;
    }

    private static XElement WriteBorder(CellBorder border)
    {
        var element = new XElement(SmlNames.Border);
        if (border.DiagonalUp) element.SetAttributeValue("diagonalUp", "1");
        if (border.DiagonalDown) element.SetAttributeValue("diagonalDown", "1");

        element.Add(WriteEdge(SmlNames.Left, border.Left));
        element.Add(WriteEdge(SmlNames.Right, border.Right));
        element.Add(WriteEdge(SmlNames.Top, border.Top));
        element.Add(WriteEdge(SmlNames.Bottom, border.Bottom));
        element.Add(WriteEdge(SmlNames.Diagonal, border.Diagonal));
        return element;
    }

    private static XElement WriteEdge(XName name, BorderLine line)
    {
        var element = new XElement(name);
        var style = SmlEnums.ToLiteral(line.Style);
        if (style == null)
            return element;

        element.SetAttributeValue(SmlNames.AttrStyle, style);
        if (line.Color is { } color)
            element.Add(new XElement(SmlNames.Color, new XAttribute(SmlNames.AttrRgb, SmlColor.ToHexArgb(color))));
        return element;
    }

    private static XElement WriteXfTable(XName tableName, IReadOnlyCollection<CellXf> xfs)
    {
        var table = new XElement(tableName, new XAttribute("count", xfs.Count));
        foreach (var xf in xfs)
            table.Add(WriteXf(xf));
        return table;
    }

    private static XElement WriteXf(CellXf xf)
    {
        var element = new XElement(
            SmlNames.Xf,
            new XAttribute("numFmtId", xf.NumberFormatId.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("fontId", xf.FontId.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("fillId", xf.FillId.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("borderId", xf.BorderId.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("xfId", xf.XfId.ToString(CultureInfo.InvariantCulture))
        );

        if (xf.ApplyNumberFormat) element.SetAttributeValue("applyNumberFormat", "1");
        if (xf.ApplyFont) element.SetAttributeValue("applyFont", "1");
        if (xf.ApplyFill) element.SetAttributeValue("applyFill", "1");
        if (xf.ApplyBorder) element.SetAttributeValue("applyBorder", "1");
        if (xf.ApplyAlignment || !xf.Alignment.IsDefault) element.SetAttributeValue("applyAlignment", "1");

        if (!xf.Alignment.IsDefault)
            element.Add(WriteAlignment(xf.Alignment));

        return element;
    }

    private static XElement WriteAlignment(CellAlignment alignment)
    {
        var element = new XElement(SmlNames.Alignment);

        var horizontal = SmlEnums.ToLiteral(alignment.Horizontal);
        if (horizontal != null) element.SetAttributeValue("horizontal", horizontal);

        var vertical = SmlEnums.ToLiteral(alignment.Vertical);
        if (vertical != null) element.SetAttributeValue("vertical", vertical);

        if (alignment.WrapText) element.SetAttributeValue("wrapText", "1");
        if (alignment.ShrinkToFit) element.SetAttributeValue("shrinkToFit", "1");
        if (alignment.TextRotation != 0) element.SetAttributeValue("textRotation", alignment.TextRotation.ToString(CultureInfo.InvariantCulture));
        if (alignment.Indent != 0) element.SetAttributeValue("indent", alignment.Indent.ToString(CultureInfo.InvariantCulture));
        if (alignment.ReadingOrder != ReadingOrder.ContextDependent)
            element.SetAttributeValue("readingOrder", SmlEnums.ToLiteral(alignment.ReadingOrder).ToString(CultureInfo.InvariantCulture));
        if (alignment.JustifyLastLine) element.SetAttributeValue("justifyLastLine", "1");

        return element;
    }

    private static void WriteNamedStyles(XContainer root, StyleBook book)
    {
        if (book.NamedStyles.Count == 0)
            return;

        var cellStyles = new XElement(SmlNames.CellStyles, new XAttribute("count", book.NamedStyles.Count));
        foreach (var style in book.NamedStyles)
        {
            var element = new XElement(
                SmlNames.CellStyle,
                new XAttribute("name", style.Name),
                new XAttribute("xfId", style.XfId.ToString(CultureInfo.InvariantCulture))
            );
            if (style.IsBuiltIn)
                element.SetAttributeValue("builtinId", style.BuiltInId.ToString(CultureInfo.InvariantCulture));
            cellStyles.Add(element);
        }

        root.Add(cellStyles);
    }
}
