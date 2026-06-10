using Unchained.Ooxml;
using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Models.Shapes;

namespace Unchained.Pptx.Parsing;

/// <summary>
/// Parses DrawingML line property elements (<c>&lt;a:ln&gt;</c>) into <see cref="LineFormat"/> objects.
/// </summary>
internal static class LineParser
{
    /// <summary>
    /// Reads <c>&lt;a:ln&gt;</c> from <paramref name="parent"/> and populates <paramref name="line"/>.
    /// If no <c>&lt;a:ln&gt;</c> element is present, the line is left at its default (no outline).
    /// </summary>
    public static void Parse(XElement parent, LineFormat line)
    {
        var ln = parent.Element(DmlNames.Line);
        if (ln == null) return;

        // Width in EMU → convert to points (1 pt = 12700 EMU)
        var widthEmu = ln.GetAttrInt(DmlNames.AttributeLineWidth);
        if (widthEmu.HasValue)
            line.WidthPoints = widthEmu.Value / 12_700.0;

        // Dash style
        var dash = ln.Element(DmlNames.PresetDash);
        if (dash != null)
            line.DashStyle = ParseDashStyle(dash.GetAttr(DmlNames.AttributeValue, "solid"));

        // Fill (colour of the line itself)
        FillParser.Parse(ln, line.Fill);

        // Head arrowhead
        var head = ln.Element(DmlNames.HeadEnd);
        if (head != null)
        {
            line.HeadArrow.HeadType = ParseArrowType(head.GetAttr("type", "none"));
            line.HeadArrow.Width = ParseArrowSize(head.GetAttr("w", "med"));
            line.HeadArrow.Length = ParseArrowSize(head.GetAttr("len", "med"));
        }

        // Tail arrowhead
        var tail = ln.Element(DmlNames.TailEnd);
        if (tail != null)
        {
            line.TailArrow.HeadType = ParseArrowType(tail.GetAttr("type", "none"));
            line.TailArrow.Width = ParseArrowSize(tail.GetAttr("w", "med"));
            line.TailArrow.Length = ParseArrowSize(tail.GetAttr("len", "med"));
        }
    }

    /// <summary>
    /// Reads line properties directly from <paramref name="lineEl"/> (e.g. an <c>&lt;a:lnL&gt;</c>
    /// table cell border element) and populates <paramref name="line"/>.
    /// </summary>
    public static void ParseElement(XElement lineEl, LineFormat line)
    {
        var widthEmu = lineEl.GetAttrInt(DmlNames.AttributeLineWidth);
        if (widthEmu.HasValue)
            line.WidthPoints = widthEmu.Value / 12_700.0;

        var dash = lineEl.Element(DmlNames.PresetDash);
        if (dash != null)
            line.DashStyle = ParseDashStyle(dash.GetAttr(DmlNames.AttributeValue, "solid"));

        FillParser.Parse(lineEl, line.Fill);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static LineDashStyle ParseDashStyle(string value) => value switch
    {
        "solid" => LineDashStyle.Solid,
        "dot" => LineDashStyle.Dot,
        "dash" => LineDashStyle.Dash,
        "dashDot" => LineDashStyle.DashDot,
        "lgDash" => LineDashStyle.LongDash,
        "lgDashDot" => LineDashStyle.LongDashDot,
        "lgDashDotDot" => LineDashStyle.LongDashDotDot,
        "sysDash" => LineDashStyle.SystemDash,
        "sysDot" => LineDashStyle.SystemDot,
        "sysDashDot" => LineDashStyle.SystemDashDot,
        "sysDashDotDot" => LineDashStyle.SystemDashDotDot,
        _ => LineDashStyle.Solid
    };

    private static ArrowHeadType ParseArrowType(string value) => value switch
    {
        "triangle" => ArrowHeadType.Triangle,
        "stealth" => ArrowHeadType.Stealth,
        "diamond" => ArrowHeadType.Diamond,
        "oval" => ArrowHeadType.Oval,
        "arrow" or "open" => ArrowHeadType.Open,
        _ => ArrowHeadType.None
    };

    private static ArrowHeadSize ParseArrowSize(string value) => value switch
    {
        "sm" => ArrowHeadSize.Small,
        "lg" => ArrowHeadSize.Large,
        _ => ArrowHeadSize.Medium
    };
}
