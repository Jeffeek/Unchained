using System.Xml.Linq;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Core;
using Unchained.Pptx.Themes;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Parses a DrawingML theme part (<c>&lt;a:theme&gt;</c>) into a <see cref="PptxTheme" />.
/// </summary>
internal static class ThemeParser
{
    /// <summary>Parses a theme XML byte array into a <see cref="PptxTheme" />.</summary>
    public static PptxTheme Parse(byte[] bytes)
    {
        var doc = OoXmlHelper.ParseXml(bytes);
        return Parse(doc.Root ?? throw new PptxException("Theme XML has no root element."));
    }

    /// <summary>Parses a <c>&lt;a:theme&gt;</c> root element into a <see cref="PptxTheme" />.</summary>
    public static PptxTheme Parse(XElement root)
    {
        var theme = new PptxTheme
        {
            Name = root.GetAttr("name", string.Empty)
        };

        var elements = root.Element(DmlNames.ThemeElements);
        if (elements == null) return theme;

        var clrScheme = elements.Element(DmlNames.ColorScheme);
        if (clrScheme != null)
            theme.Colors = ParseColorScheme(clrScheme);

        var fontScheme = elements.Element(DmlNames.FontScheme);
        if (fontScheme != null)
            theme.Fonts = ParseFontScheme(fontScheme);

        return theme;
    }

    // ── Colour scheme ─────────────────────────────────────────────────────────

    private static ColorScheme ParseColorScheme(XContainer clrScheme)
    {
        var scheme = new ColorScheme
        {
            Dark1 = ParseSlot(clrScheme, DmlNames.Dark1),
            Light1 = ParseSlot(clrScheme, DmlNames.Light1),
            Dark2 = ParseSlot(clrScheme, DmlNames.Dark2),
            Light2 = ParseSlot(clrScheme, DmlNames.Light2),
            Accent1 = ParseSlot(clrScheme, DmlNames.Accent1),
            Accent2 = ParseSlot(clrScheme, DmlNames.Accent2),
            Accent3 = ParseSlot(clrScheme, DmlNames.Accent3),
            Accent4 = ParseSlot(clrScheme, DmlNames.Accent4),
            Accent5 = ParseSlot(clrScheme, DmlNames.Accent5),
            Accent6 = ParseSlot(clrScheme, DmlNames.Accent6),
            HyperlinkColor = ParseSlot(clrScheme, DmlNames.Hyperlink),
            FollowedHyperlinkColor = ParseSlot(clrScheme, DmlNames.FollowedHyperlink)
        };

        return scheme;
    }

    private static ColorSpec ParseSlot(XContainer parent, XName slotName)
    {
        var slot = parent.Element(slotName);
        return slot != null ? ColorParser.Parse(slot) : ColorSpec.FromRgb(0x80, 0x80, 0x80);
    }

    // ── Font scheme ───────────────────────────────────────────────────────────

    private static FontScheme ParseFontScheme(XElement fontScheme)
    {
        var scheme = new FontScheme
        {
            Name = fontScheme.GetAttr("name", string.Empty)
        };

        var major = fontScheme.Element(DmlNames.MajorFont);
        if (major != null) scheme.MajorFont = ParseFontSet(major);

        var minor = fontScheme.Element(DmlNames.MinorFont);
        if (minor != null) scheme.MinorFont = ParseFontSet(minor);

        return scheme;
    }

    private static ThemeFontSet ParseFontSet(XContainer fontSetEl)
    {
        var set = new ThemeFontSet
        {
            LatinFont = fontSetEl.Element(DmlNames.LatinFont)
                            ?.GetAttr(DmlNames.AttributeTypeface, string.Empty)
                        ?? string.Empty,
            EastAsianFont = fontSetEl.Element(DmlNames.EastAsianFont)
                                ?.GetAttr(DmlNames.AttributeTypeface, string.Empty)
                            ?? string.Empty,
            ComplexScriptFont = fontSetEl.Element(DmlNames.ComplexScriptFont)
                                    ?.GetAttr(DmlNames.AttributeTypeface, string.Empty)
                                ?? string.Empty
        };

        foreach (var font in fontSetEl.Elements(DmlNames.Dml + "font"))
        {
            var script = font.GetAttr("script");
            var typeface = font.GetAttr(DmlNames.AttributeTypeface);
            if (script != null && typeface != null)
                set.ScriptFonts[script] = typeface;
        }

        return set;
    }
}
