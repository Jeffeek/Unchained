using System.Xml.Linq;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Themes;

namespace Unchained.Pptx.Writing;

/// <summary>
/// Serializes a <see cref="PptxTheme"/> to a DrawingML <c>&lt;a:theme&gt;</c> root element.
/// </summary>
internal static class ThemeWriter
{
    /// <summary>Returns an <c>&lt;a:theme&gt;</c> element for the given theme.</summary>
    public static XElement Write(PptxTheme theme)
    {
        var dml = DmlNames.Dml;

        var themeEl = new XElement(DmlNames.Theme,
            new XAttribute(XNamespace.Xmlns + "a", dml.NamespaceName),
            new XAttribute("name", theme.Name));

        var elements = new XElement(DmlNames.ThemeElements);

        elements.Add(WriteColorScheme(theme.Colors));
        elements.Add(WriteFontScheme(theme.Fonts));
        elements.Add(WriteFormatScheme());

        themeEl.Add(elements);
        themeEl.Add(new XElement(dml + "objectDefaults"));
        themeEl.Add(new XElement(dml + "extraClrSchemeLst"));

        return themeEl;
    }

    private static XElement WriteColorScheme(ColorScheme colors)
    {
        var scheme = new XElement(DmlNames.ColorScheme,
            new XAttribute("name", "Custom"));

        scheme.Add(WrapSlot(DmlNames.Dark1, colors.Dark1));
        scheme.Add(WrapSlot(DmlNames.Light1, colors.Light1));
        scheme.Add(WrapSlot(DmlNames.Dark2, colors.Dark2));
        scheme.Add(WrapSlot(DmlNames.Light2, colors.Light2));
        scheme.Add(WrapSlot(DmlNames.Accent1, colors.Accent1));
        scheme.Add(WrapSlot(DmlNames.Accent2, colors.Accent2));
        scheme.Add(WrapSlot(DmlNames.Accent3, colors.Accent3));
        scheme.Add(WrapSlot(DmlNames.Accent4, colors.Accent4));
        scheme.Add(WrapSlot(DmlNames.Accent5, colors.Accent5));
        scheme.Add(WrapSlot(DmlNames.Accent6, colors.Accent6));
        scheme.Add(WrapSlot(DmlNames.Hyperlink, colors.HyperlinkColor));
        scheme.Add(WrapSlot(DmlNames.FollowedHyperlink, colors.FollowedHyperlinkColor));

        return scheme;

        static XElement WrapSlot(XName name, ColorSpec color)
        {
            var el = new XElement(name);
            el.Add(ColorWriter.Write(color));
            return el;
        }
    }

    private static XElement WriteFontScheme(FontScheme fonts)
    {
        var scheme = new XElement(DmlNames.FontScheme,
            new XAttribute("name", fonts.Name.Length > 0 ? fonts.Name : "Office"));

        scheme.Add(WriteFontSet(DmlNames.MajorFont, fonts.MajorFont));
        scheme.Add(WriteFontSet(DmlNames.MinorFont, fonts.MinorFont));

        return scheme;
    }

    private static XElement WriteFontSet(XName elementName, ThemeFontSet fontSet)
    {
        var el = new XElement(elementName);

        el.Add(new XElement(DmlNames.LatinFont,
            new XAttribute(DmlNames.AttributeTypeface,
                fontSet.LatinFont.Length > 0 ? fontSet.LatinFont : "Calibri")));
        el.Add(new XElement(DmlNames.EastAsianFont,
            new XAttribute(DmlNames.AttributeTypeface, fontSet.EastAsianFont)));
        el.Add(new XElement(DmlNames.ComplexScriptFont,
            new XAttribute(DmlNames.AttributeTypeface, fontSet.ComplexScriptFont)));

        foreach (var (script, typeface) in fontSet.ScriptFonts)
            el.Add(new XElement(DmlNames.Dml + "font",
                new XAttribute("script", script),
                new XAttribute(DmlNames.AttributeTypeface, typeface)));

        return el;
    }

    private static XElement WriteFormatScheme()
    {
        var dml = DmlNames.Dml;
        var scheme = new XElement(DmlNames.FormatScheme,
            new XAttribute("name", "Office"));

        // Minimal fill, line, and effect style lists required by OOXML
        var fillStyleLst = new XElement(dml + "fillStyleLst");
        fillStyleLst.Add(new XElement(DmlNames.SolidFill,
            new XElement(DmlNames.SchemeColor, new XAttribute(DmlNames.AttributeValue, "phClr"))));
        fillStyleLst.Add(new XElement(DmlNames.GradientFill));
        fillStyleLst.Add(new XElement(DmlNames.GradientFill));
        scheme.Add(fillStyleLst);

        var lnStyleLst = new XElement(dml + "lnStyleLst");
        lnStyleLst.Add(new XElement(DmlNames.Line, new XAttribute(DmlNames.AttributeLineWidth, 6350),
            new XElement(DmlNames.SolidFill,
                new XElement(DmlNames.SchemeColor, new XAttribute(DmlNames.AttributeValue, "phClr")))));
        lnStyleLst.Add(new XElement(DmlNames.Line, new XAttribute(DmlNames.AttributeLineWidth, 12700)));
        lnStyleLst.Add(new XElement(DmlNames.Line, new XAttribute(DmlNames.AttributeLineWidth, 19050)));
        scheme.Add(lnStyleLst);

        var effectStyleLst = new XElement(dml + "effectStyleLst");
        effectStyleLst.Add(new XElement(dml + "effectStyle", new XElement(dml + "effectLst")));
        effectStyleLst.Add(new XElement(dml + "effectStyle", new XElement(dml + "effectLst")));
        effectStyleLst.Add(new XElement(dml + "effectStyle", new XElement(dml + "effectLst")));
        scheme.Add(effectStyleLst);

        var bgFillStyleLst = new XElement(dml + "bgFillStyleLst");
        bgFillStyleLst.Add(new XElement(DmlNames.SolidFill,
            new XElement(DmlNames.SchemeColor, new XAttribute(DmlNames.AttributeValue, "phClr"))));
        bgFillStyleLst.Add(new XElement(DmlNames.GradientFill));
        bgFillStyleLst.Add(new XElement(DmlNames.GradientFill));
        scheme.Add(bgFillStyleLst);

        return scheme;
    }
}
