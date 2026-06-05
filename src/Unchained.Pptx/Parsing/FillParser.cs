using System.Xml.Linq;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Drawing;
using Unchained.Pptx.Models.Drawing;
using Unchained.Pptx.Models.Shapes;

namespace Unchained.Pptx.Parsing;

/// <summary>
/// Parses DrawingML fill elements into <see cref="FillFormat"/> objects.
/// </summary>
internal static class FillParser
{
    /// <summary>
    /// Reads fill information from <paramref name="parent"/> and populates
    /// <paramref name="fill"/>. Supports solid, gradient, pattern, picture, no-fill, and group-fill.
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
        if (blip != null)
        {
            ParsePicture(blip, fill);
        }
    }

    // ── Sub-parsers ───────────────────────────────────────────────────────────

    private static void ParseGradient(XElement gradientElement, FillFormat fill)
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
            var pos = gs.GetAttrInt(DmlNames.AttributePosition, 0) / 100_000.0;
            var color = ColorParser.Parse(gs);
            gf.Stops.Add(new GradientStop(pos, color));
        }
    }

    private static void ParsePattern(XElement patternElement, FillFormat fill)
    {
        var preset = patternElement.GetAttr("prst", "pct5");
        var foreground = patternElement.Element(DmlNames.SolidFill) is { } fg
            ? ColorParser.Parse(fg)
            : default;

        var background = patternElement.Elements().Skip(1).FirstOrDefault() is { } bg
            ? ColorParser.Parse(bg)
            : default;

        fill.Type = FillType.Pattern;
        fill.Pattern = new PatternFill
        {
            Preset = ParsePatternPreset(preset),
            ForegroundColor = foreground,
            BackgroundColor = background
        };
    }

    private static void ParsePicture(XElement blipElement, FillFormat fill)
    {
        // Image data is resolved later by the SlideParser when it has the OPC package.
        // For now, mark the fill type so writers know what to emit.
        fill.Type = FillType.Picture;
        fill.Picture = new PictureFill();
    }

    private static PatternPreset ParsePatternPreset(string value) => value switch
    {
        "pct5" => PatternPreset.Percent5,
        "pct10" => PatternPreset.Percent10,
        "pct20" => PatternPreset.Percent20,
        "pct25" => PatternPreset.Percent25,
        "pct30" => PatternPreset.Percent30,
        "pct40" => PatternPreset.Percent40,
        "pct50" => PatternPreset.Percent50,
        "pct60" => PatternPreset.Percent60,
        "pct70" => PatternPreset.Percent70,
        "pct75" => PatternPreset.Percent75,
        "pct80" => PatternPreset.Percent80,
        "pct90" => PatternPreset.Percent90,
        "horz" => PatternPreset.HorizontalLines,
        "vert" => PatternPreset.VerticalLines,
        "ltHorz" => PatternPreset.LightHorizontal,
        "ltVert" => PatternPreset.LightVertical,
        "ltDnDiag" => PatternPreset.LightDownwardDiagonal,
        "ltUpDiag" => PatternPreset.LightUpwardDiagonal,
        "dkHorz" => PatternPreset.DarkHorizontal,
        "dkVert" => PatternPreset.DarkVertical,
        "dkDnDiag" => PatternPreset.DarkDownwardDiagonal,
        "dkUpDiag" => PatternPreset.DarkUpwardDiagonal,
        "wdDnDiag" => PatternPreset.WideDownwardDiagonal,
        "wdUpDiag" => PatternPreset.WideUpwardDiagonal,
        "dashHorz" => PatternPreset.DashedHorizontal,
        "dashVert" => PatternPreset.DashedVertical,
        "dashDnDiag" => PatternPreset.DashedDownwardDiagonal,
        "dashUpDiag" => PatternPreset.DashedUpwardDiagonal,
        "smConfetti" => PatternPreset.SmallConfetti,
        "lgConfetti" => PatternPreset.LargeConfetti,
        "zigZag" => PatternPreset.Zigzag,
        "wave" => PatternPreset.Wave,
        "diagBrick" => PatternPreset.DiagonalBrick,
        "horzBrick" => PatternPreset.HorizontalBrick,
        "weave" => PatternPreset.Weave,
        "plaid" => PatternPreset.Plaid,
        "divot" => PatternPreset.Divot,
        "dotGrid" => PatternPreset.DottedGrid,
        "dotDmnd" => PatternPreset.DottedDiamond,
        "shingle" => PatternPreset.Shingle,
        "trellis" => PatternPreset.Trellis,
        "sphere" => PatternPreset.Sphere,
        "smGrid" => PatternPreset.SmallGrid,
        "lgGrid" => PatternPreset.LargeGrid,
        "smCheck" => PatternPreset.SmallCheckerBoard,
        "lgCheck" => PatternPreset.LargeCheckerBoard,
        "openDmnd" => PatternPreset.OutlinedDiamond,
        "solidDmnd" => PatternPreset.SolidDiamond,
        _ => PatternPreset.Percent5
    };
}
