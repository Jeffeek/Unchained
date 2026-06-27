using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;

namespace Unchained.Pptx.Writing;

/// <summary>
///     Serializes <see cref="LineFormat" /> objects to DrawingML <c>&lt;a:ln&gt;</c> XML elements.
/// </summary>
internal static class LineWriter
{
    /// <summary>
    ///     Appends an <c>&lt;a:ln&gt;</c> element from <paramref name="line" /> to <paramref name="parent" />.
    ///     When the line has no fill (type None and no explicit width) the element is still written
    ///     with <c>&lt;a:noFill/&gt;</c> to suppress any inherited outline.
    /// </summary>
    public static void Write(XElement parent, LineFormat line)
    {
        var ln = new XElement(DmlNames.Line);

        if (line.WidthPoints.HasValue)
        {
            ln.Add(
                new XAttribute(
                    DmlNames.AttributeLineWidth,
                    (int)(line.WidthPoints.Value * Emu.EmusPerPoint)
                )
            );
        }

        // Fill
        FillWriter.Write(ln, line.Fill);

        // Dash style
        if (line.DashStyle != LineDashStyle.Solid)
        {
            ln.Add(
                new XElement(
                    DmlNames.PresetDash,
                    new XAttribute(DmlNames.AttributeValue, LineStyles.DashStyleToString(line.DashStyle))
                )
            );
        }

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
        new(
            elementName,
            new XAttribute("type", LineStyles.ArrowTypeToString(arrow.HeadType)),
            new XAttribute("w", LineStyles.ArrowSizeToString(arrow.Width)),
            new XAttribute("len", LineStyles.ArrowSizeToString(arrow.Length))
        );
}
