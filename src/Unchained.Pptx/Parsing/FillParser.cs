using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Core.Xml;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Parses DrawingML fill elements into <see cref="FillFormat" /> objects.
/// </summary>
internal static class FillParser
{
    /// <summary>
    ///     Reads fill information from <paramref name="parent" /> and populates
    ///     <paramref name="fill" />. Supports solid, gradient, pattern, picture, no-fill, and group-fill.
    /// </summary>
    public static void Parse(XElement parent, FillFormat fill)
    {
        if (parent.Element(DmlNames.NoFill) != null || parent.Element(DmlNames.GroupFill) != null)
        {
            fill.SetNone();
            return;
        }

        var solid = parent.Element(DmlNames.SolidFill);
        if (solid != null)
        {
            fill.SetSolid(ColorParser.Parse(solid));
            return;
        }

        var gradient = parent.Element(DmlNames.GradientFill);
        if (gradient != null)
        {
            ParseGradient(gradient, fill);
            return;
        }

        var pattern = parent.Element(DmlNames.PatternFill);
        if (pattern != null)
        {
            ParsePattern(pattern, fill);
            return;
        }

        var blip = parent.Element(DmlNames.BlipFill);
        if (blip != null) ParsePicture(blip, fill);
    }

    // ── Sub-parsers ───────────────────────────────────────────────────────────

    private static void ParseGradient(XContainer gradientElement, FillFormat fill)
    {
        var gf = fill.SetGradient();

        var linear = gradientElement.Element(DmlNames.LinearGradient);
        if (linear != null)
        {
            gf.IsLinear = true;
            var ang = linear.GetAttrInt(DmlNames.AttributeRotation, 0);
            gf.LinearAngleDegrees = OoXmlHelper.OoxmlRotationToDegrees(ang);
        }

        var stopList = gradientElement.Element(DmlNames.GradientStopList);
        if (stopList == null) return;

        foreach (var gs in stopList.Elements(DmlNames.GradientStop))
        {
            var pos = gs.GetAttrInt(DmlNames.AttributePosition, 0) / (double)OoxmlScaling.PercentScale;
            var color = ColorParser.Parse(gs);
            gf.Stops.Add(new GradientStop(pos, color));
        }
    }

    private static void ParsePattern(XElement patternElement, FillFormat fill)
    {
        var preset = patternElement.GetAttr(DmlNames.AttributePreset, "pct5");
        var foreground = patternElement.Element(DmlNames.SolidFill) is { } fg
            ? ColorParser.Parse(fg)
            : default;

        // The second child is <a:bgClr>, which wraps a color element per ECMA-376.
        // The color element may be <solidFill> (spec-correct) or directly a colour element.
        var bgClr = patternElement.Elements().Skip(1).FirstOrDefault();
        var background = bgClr != null ? ReadBgClrColor(bgClr) : default;

        fill.Type = FillType.Pattern;
        fill.Pattern = new PatternFill
        {
            Preset = LineStyles.ParsePatternPreset(preset),
            ForegroundColor = foreground,
            BackgroundColor = background
        };
    }

    private static ColorSpec ReadBgClrColor(XElement bgClr)
    {
        // Per ECMA-376 §20.1.8.4, <bgClr> wraps a colour element.
        // Try <solidFill> first (spec-correct), then fall back to direct colour child.
        var solidFill = bgClr.Element(DmlNames.SolidFill);
        return ColorParser.Parse(
            solidFill ??
            bgClr // Direct colour child (legacy / malformed but seen in practice).
        );
    }

    private static void ParsePicture(XContainer blipElement, FillFormat fill)
    {
        // Image data is resolved later by the SlideParser when it has the OPC package.
        // Store the r:embed rId so the second pass can look up the image part.
        var blip = blipElement.Element(DmlNames.Blip);
        var rId = (string?)blip?.Attribute(PmlNames.RelationshipEmbed)
                  ?? (string?)blip?.Attribute(PmlNames.RelationshipId);

        fill.Type = FillType.Picture;
        fill.Picture = new PictureFill { RelationshipId = rId };
    }
}
