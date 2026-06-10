using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Xml;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Models.Shapes;

namespace Unchained.Pptx.Writing;

/// <summary>
/// Serializes <see cref="LineFormat"/> objects to DrawingML <c>&lt;a:ln&gt;</c> XML elements.
/// </summary>
internal static class LineWriter
{
    /// <summary>
    /// Appends an <c>&lt;a:ln&gt;</c> element from <paramref name="line"/> to <paramref name="parent"/>.
    /// When the line has no fill (type None and no explicit width) the element is still written
    /// with <c>&lt;a:noFill/&gt;</c> to suppress any inherited outline.
    /// </summary>
    public static void Write(XElement parent, LineFormat line)
    {
        var ln = new XElement(DmlNames.Line);

        if (line.WidthPoints.HasValue)
            ln.Add(new XAttribute(DmlNames.AttributeLineWidth,
                (int)(line.WidthPoints.Value * EmuConversions.EmuPerPoint)));

        // Fill
        FillWriter.Write(ln, line.Fill);

        // Dash style
        if (line.DashStyle != LineDashStyle.Solid)
            ln.Add(new XElement(DmlNames.PresetDash,
                new XAttribute(DmlNames.AttributeValue, DashStyleToString(line.DashStyle))));

        // Head arrow
        if (line.HeadArrow.HeadType != ArrowHeadType.None)
            ln.Add(WriteArrow(DmlNames.HeadEnd, line.HeadArrow));

        // Tail arrow
        if (line.TailArrow.HeadType != ArrowHeadType.None)
        {
            var tailArrow = new ArrowFormat
            {
                HeadType = line.TailArrow.HeadType,
                Width = line.TailArrow.Width,
                Length = line.TailArrow.Length
            };
            ln.Add(WriteArrow(DmlNames.TailEnd, tailArrow));
        }

        parent.Add(ln);
    }

    private static XElement WriteArrow(XName elementName, ArrowFormat arrow) =>
        new(elementName,
            new XAttribute("type", ArrowTypeToString(arrow.HeadType)),
            new XAttribute("w", ArrowSizeToString(arrow.Width)),
            new XAttribute("len", ArrowSizeToString(arrow.Length)));

    private static string DashStyleToString(LineDashStyle style) => style switch
    {
        LineDashStyle.Dot => "dot",
        LineDashStyle.Dash => "dash",
        LineDashStyle.DashDot => "dashDot",
        LineDashStyle.LongDash => "lgDash",
        LineDashStyle.LongDashDot => "lgDashDot",
        LineDashStyle.LongDashDotDot => "lgDashDotDot",
        LineDashStyle.SystemDash => "sysDash",
        LineDashStyle.SystemDot => "sysDot",
        LineDashStyle.SystemDashDot => "sysDashDot",
        LineDashStyle.SystemDashDotDot => "sysDashDotDot",
        _ => "solid"
    };

    private static string ArrowTypeToString(ArrowHeadType type) => type switch
    {
        ArrowHeadType.Triangle => "triangle",
        ArrowHeadType.Stealth => "stealth",
        ArrowHeadType.Diamond => "diamond",
        ArrowHeadType.Oval => "oval",
        ArrowHeadType.Open => "arrow",
        _ => "none"
    };

    private static string ArrowSizeToString(ArrowHeadSize size) => size switch
    {
        ArrowHeadSize.Small => "sm",
        ArrowHeadSize.Large => "lg",
        _ => "med"
    };
}
