using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;

namespace Unchained.Pptx.Writing;

/// <summary>
///     Serializes an <see cref="EffectFormat" /> to a DrawingML <c>&lt;a:effectLst&gt;</c> element,
///     or <see langword="null" /> when no effects are set. Child order follows the OOXML schema
///     (blur, fillOverlay, glow, innerShdw, outerShdw, reflection, softEdge).
/// </summary>
internal static class EffectWriter
{
    private static readonly XNamespace A = DmlNames.Dml;

    public static XElement? Write(EffectFormat effects)
    {
        if (effects.IsEmpty) return null;

        var lst = new XElement(A + "effectLst");

        if (effects.Blur is { } blur)
        {
            lst.Add(new XElement(A + "blur",
                new XAttribute("rad", blur.Radius.Value),
                new XAttribute("grow", blur.GrowBounds ? "1" : "0")));
        }

        if (effects.Glow is { } glow)
        {
            var el = new XElement(A + "glow", new XAttribute("rad", glow.Radius.Value));
            el.Add(ColorWriter.Write(glow.Color));
            lst.Add(el);
        }

        if (effects.InnerShadow is { } inner)
        {
            var el = new XElement(A + "innerShdw",
                new XAttribute("blurRad", inner.BlurRadius.Value),
                new XAttribute("dist", inner.Distance.Value),
                new XAttribute("dir", DegreesToAngle(inner.DirectionDegrees)));
            el.Add(ColorWriter.Write(inner.Color));
            lst.Add(el);
        }

        if (effects.OuterShadow is { } outer)
        {
            var el = new XElement(A + "outerShdw",
                new XAttribute("blurRad", outer.BlurRadius.Value),
                new XAttribute("dist", outer.Distance.Value),
                new XAttribute("dir", DegreesToAngle(outer.DirectionDegrees)),
                new XAttribute("sx", PercentToThousandths(outer.ScaleHorizontalPercent)),
                new XAttribute("sy", PercentToThousandths(outer.ScaleVerticalPercent)),
                new XAttribute("algn", outer.Alignment),
                new XAttribute("rotWithShape", outer.RotateWithShape ? "1" : "0"));
            el.Add(ColorWriter.Write(outer.Color));
            lst.Add(el);
        }

        if (effects.Reflection is { } refl)
        {
            lst.Add(new XElement(A + "reflection",
                new XAttribute("blurRad", refl.BlurRadius.Value),
                new XAttribute("stA", PercentToThousandths(refl.StartOpacityPercent)),
                new XAttribute("endA", PercentToThousandths(refl.EndOpacityPercent)),
                new XAttribute("dist", refl.Distance.Value),
                new XAttribute("dir", DegreesToAngle(refl.DirectionDegrees))));
        }

        if (effects.SoftEdge is { } soft)
            lst.Add(new XElement(A + "softEdge", new XAttribute("rad", soft.Radius.Value)));

        return lst;
    }

    private static string DegreesToAngle(double degrees) =>
        ((int)Math.Round(degrees * 60000)).ToString(CultureInfo.InvariantCulture);

    private static string PercentToThousandths(double percent) =>
        ((int)Math.Round(percent * 1000)).ToString(CultureInfo.InvariantCulture);
}
