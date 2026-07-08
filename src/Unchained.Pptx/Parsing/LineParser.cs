using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Parses DrawingML line property elements (<c>&lt;a:ln&gt;</c>) into <see cref="LineFormat" /> objects.
/// </summary>
internal static class LineParser
{
    /// <summary>
    ///     Reads <c>&lt;a:ln&gt;</c> from <paramref name="parent" /> and populates <paramref name="line" />.
    ///     If no <c>&lt;a:ln&gt;</c> element is present, the line is left at its default (no outline).
    /// </summary>
    public static void Parse(XElement parent, LineFormat line)
    {
        var ln = parent.Element(DmlNames.Line);
        if (ln == null) return;

        // Width in EMU → convert to points (1 pt = 12700 EMU)
        var widthEmu = ln.GetAttrInt(DmlNames.AttributeLineWidth);
        if (widthEmu.HasValue)
            line.WidthPoints = widthEmu.Value / (double)Emu.EmusPerPoint;

        // Dash style
        var dash = ln.Element(DmlNames.PresetDash);
        if (dash != null)
            line.DashStyle = LineStyles.ParseDashStyle(dash.GetAttr(DmlNames.AttributeValue, LineStyles.DashSolid));

        // Fill (colour of the line itself)
        FillParser.Parse(ln, line.Fill);

        // Head arrowhead
        var head = ln.Element(DmlNames.HeadEnd);
        if (head != null)
        {
            line.HeadArrow.HeadType = LineStyles.ParseArrowType(head.GetAttr("type", "none"));
            line.HeadArrow.Width = LineStyles.ParseArrowSize(head.GetAttr("w", "med"));
            line.HeadArrow.Length = LineStyles.ParseArrowSize(head.GetAttr("len", "med"));
        }

        // Tail arrowhead
        var tail = ln.Element(DmlNames.TailEnd);
        if (tail == null) return;

        line.TailArrow.HeadType = LineStyles.ParseArrowType(tail.GetAttr("type", "none"));
        line.TailArrow.Width = LineStyles.ParseArrowSize(tail.GetAttr("w", "med"));
        line.TailArrow.Length = LineStyles.ParseArrowSize(tail.GetAttr("len", "med"));
    }

    /// <summary>
    ///     Reads line properties directly from <paramref name="lineEl" /> (e.g. an <c>&lt;a:lnL&gt;</c>
    ///     table cell border element) and populates <paramref name="line" />.
    /// </summary>
    public static void ParseElement(XElement lineEl, LineFormat line)
    {
        var widthEmu = lineEl.GetAttrInt(DmlNames.AttributeLineWidth);
        if (widthEmu.HasValue)
            line.WidthPoints = widthEmu.Value / (double)Emu.EmusPerPoint;

        var dash = lineEl.Element(DmlNames.PresetDash);
        if (dash != null)
            line.DashStyle = LineStyles.ParseDashStyle(dash.GetAttr(DmlNames.AttributeValue, LineStyles.DashSolid));

        FillParser.Parse(lineEl, line.Fill);
    }
}
