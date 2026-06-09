using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;

namespace Unchained.Pptx.Parsing;

/// <summary>
/// Parses a DrawingML <c>&lt;a:effectLst&gt;</c> element into an <see cref="EffectFormat"/>.
/// Angles are stored in OOXML 60,000ths of a degree; percentages in 1,000ths of a percent.
/// </summary>
internal static class EffectParser
{
    private static readonly XNamespace A = DmlNames.Dml;

    public static void Parse(XElement? spPr, EffectFormat effects)
    {
        var effectLst = spPr?.Element(A + "effectLst");
        if (effectLst is null) return;

        if (effectLst.Element(A + "outerShdw") is { } outer)
            effects.OuterShadow = ParseOuterShadow(outer);
        if (effectLst.Element(A + "innerShdw") is { } inner)
            effects.InnerShadow = ParseInnerShadow(inner);
        if (effectLst.Element(A + "glow") is { } glow)
            effects.Glow = ParseGlow(glow);
        if (effectLst.Element(A + "reflection") is { } refl)
            effects.Reflection = ParseReflection(refl);
        if (effectLst.Element(A + "softEdge") is { } soft)
            effects.SoftEdge = new SoftEdgeEffect { Radius = new Emu(soft.GetAttrLong("rad", 0)) };
        if (effectLst.Element(A + "blur") is { } blur)
            effects.Blur = new BlurEffect
            {
                Radius = new Emu(blur.GetAttrLong("rad", 0)),
                GrowBounds = blur.GetAttrBool("grow") ?? true
            };
    }

    private static OuterShadowEffect ParseOuterShadow(XElement el) => new()
    {
        Color = ColorParser.Parse(el),
        BlurRadius = new Emu(el.GetAttrLong("blurRad", 0)),
        Distance = new Emu(el.GetAttrLong("dist", 0)),
        DirectionDegrees = AngleToDegrees(el.GetAttrInt("dir")),
        ScaleHorizontalPercent = PercentOrDefault(el.GetAttrInt("sx"), 100),
        ScaleVerticalPercent = PercentOrDefault(el.GetAttrInt("sy"), 100),
        Alignment = el.GetAttr("algn", "tl"),
        RotateWithShape = el.GetAttrBool("rotWithShape") ?? false
    };

    private static InnerShadowEffect ParseInnerShadow(XElement el) => new()
    {
        Color = ColorParser.Parse(el),
        BlurRadius = new Emu(el.GetAttrLong("blurRad", 0)),
        Distance = new Emu(el.GetAttrLong("dist", 0)),
        DirectionDegrees = AngleToDegrees(el.GetAttrInt("dir"))
    };

    private static GlowEffect ParseGlow(XElement el) => new()
    {
        Color = ColorParser.Parse(el),
        Radius = new Emu(el.GetAttrLong("rad", 0))
    };

    private static ReflectionEffect ParseReflection(XElement el) => new()
    {
        BlurRadius = new Emu(el.GetAttrLong("blurRad", 0)),
        StartOpacityPercent = PercentOrDefault(el.GetAttrInt("stA"), 100),
        EndOpacityPercent = PercentOrDefault(el.GetAttrInt("endA"), 0),
        Distance = new Emu(el.GetAttrLong("dist", 0)),
        DirectionDegrees = AngleToDegrees(el.GetAttrInt("dir"))
    };

    private static double AngleToDegrees(int? sixtyThousandths) =>
        sixtyThousandths is { } v ? v / 60000.0 : 0.0;

    private static double PercentOrDefault(int? thousandthsPercent, double fallback) =>
        thousandthsPercent is { } v ? v / 1000.0 : fallback;
}
