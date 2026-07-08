using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Core.Xml;

namespace Unchained.Pptx.Writing;

/// <summary>
///     Serializes <see cref="FillFormat" /> objects to DrawingML fill XML elements.
/// </summary>
internal static class FillWriter
{
    /// <summary>
    ///     Appends the appropriate fill element(s) from <paramref name="fill" />
    ///     to <paramref name="parent" />.
    /// </summary>
    public static void Write(XElement parent, FillFormat fill)
    {
        switch (fill.Type)
        {
            case FillType.None:
                parent.Add(new XElement(DmlNames.NoFill));
            break;

            case FillType.Solid when fill.Solid != null:
            {
                var solidEl = new XElement(DmlNames.SolidFill);
                solidEl.Add(ColorWriter.Write(fill.Solid.Color));
                parent.Add(solidEl);
                break;
            }

            case FillType.Gradient when fill.Gradient != null:
                parent.Add(WriteGradient(fill.Gradient));
            break;

            case FillType.Pattern when fill.Pattern != null:
                parent.Add(WritePattern(fill.Pattern));
            break;

            case FillType.Picture when fill.Picture != null:
                parent.Add(WritePicture(fill.Picture));
            break;

            case FillType.Group:
                parent.Add(new XElement(DmlNames.GroupFill));
            break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static XElement WriteGradient(GradientFill gradient)
    {
        var gradFill = new XElement(DmlNames.GradientFill);
        var gsLst = new XElement(DmlNames.GradientStopList);

        foreach (var stop in gradient.Stops)
        {
            var gs = new XElement(
                DmlNames.GradientStop,
                new XAttribute(DmlNames.AttributePosition, (int)(stop.Position * OoxmlScaling.PercentScale))
            );
            gs.Add(ColorWriter.Write(stop.Color));
            gsLst.Add(gs);
        }

        gradFill.Add(gsLst);

        if (!gradient.IsLinear) return gradFill;

        var ang = OoXmlHelper.DegreesToOoxmlRotation(gradient.LinearAngleDegrees);
        gradFill.Add(
            new XElement(
                DmlNames.LinearGradient,
                new XAttribute(DmlNames.AttributeRotation, ang),
                new XAttribute("scaled", "0")
            )
        );

        return gradFill;
    }

    private static XElement WritePattern(PatternFill pattern)
    {
        var pattFill = new XElement(
            DmlNames.PatternFill,
            new XAttribute(DmlNames.AttributePreset, LineStyles.PatternPresetToString(pattern.Preset))
        );

        var fg = new XElement(DmlNames.SolidFill);
        fg.Add(ColorWriter.Write(pattern.ForegroundColor));
        pattFill.Add(new XElement(DmlNames.Dml + "fgClr", fg));

        var bg = new XElement(DmlNames.SolidFill);
        bg.Add(ColorWriter.Write(pattern.BackgroundColor));
        pattFill.Add(new XElement(DmlNames.Dml + "bgClr", bg));

        return pattFill;
    }

    private static XElement WritePicture(PictureFill picture)
    {
        var blipFill = new XElement(DmlNames.BlipFill);
        if (picture.Image != null)
        {
            var blip = new XElement(DmlNames.Blip);
            if (!string.IsNullOrEmpty(picture.Image.RelationshipId))
                blip.Add(new XAttribute(PmlNames.RelationshipEmbed, picture.Image.RelationshipId));
            blipFill.Add(blip);
        }

        blipFill.Add(
            new XElement(
                DmlNames.Stretch,
                new XElement(DmlNames.FillRect)
            )
        );
        return blipFill;
    }
}
