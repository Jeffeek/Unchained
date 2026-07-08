using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Media;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

public sealed class FillParserTests
{
    [Fact]
    public void Parse_NoFill_SetsNone()
    {
        var parent = new XElement(DmlNames.Dml + "spPr", new XElement(DmlNames.NoFill));
        var fill = new FillFormat();
        FillParser.Parse(parent, fill);
        fill.Type.ShouldBe(FillType.None);
    }

    [Fact]
    public void Parse_GroupFill_SetsNone()
    {
        var parent = new XElement(DmlNames.Dml + "spPr", new XElement(DmlNames.GroupFill));
        var fill = new FillFormat();
        FillParser.Parse(parent, fill);
        fill.Type.ShouldBe(FillType.None);
    }

    [Fact]
    public void Parse_SolidFill_SetsSolidColor()
    {
        var solid = new XElement(
            DmlNames.SolidFill,
            new XElement(DmlNames.SrgbColor, new XAttribute(DmlNames.AttributeValue, "FF0000"))
        );
        var parent = new XElement(DmlNames.Dml + "spPr", solid);
        var fill = new FillFormat();
        FillParser.Parse(parent, fill);
        fill.Type.ShouldBe(FillType.Solid);
        fill.Solid.ShouldNotBeNull();
        fill.Solid.Color.Resolve(null).ShouldBe(0xFFFF0000u);
    }

    [Fact]
    public void Parse_GradientFill_ReadsStops()
    {
        var gradient = new XElement(
            DmlNames.GradientFill,
            new XElement(
                DmlNames.GradientStopList,
                new XElement(
                    DmlNames.GradientStop,
                    new XAttribute("pos", "0"),
                    new XElement(DmlNames.SrgbColor, new XAttribute(DmlNames.AttributeValue, "000000"))
                ),
                new XElement(
                    DmlNames.GradientStop,
                    new XAttribute("pos", "100000"),
                    new XElement(DmlNames.SrgbColor, new XAttribute(DmlNames.AttributeValue, "FFFFFF"))
                )
            )
        );
        var parent = new XElement(DmlNames.Dml + "spPr", gradient);
        var fill = new FillFormat();
        FillParser.Parse(parent, fill);

        fill.Type.ShouldBe(FillType.Gradient);
        fill.Gradient.ShouldNotBeNull();
        fill.Gradient.Stops.Count.ShouldBe(2);
        fill.Gradient.Stops[1].Position.ShouldBe(1.0, 0.001);
    }

    [Fact]
    public void Parse_PatternFill_ReadsPreset()
    {
        var pattern = new XElement(
            DmlNames.PatternFill,
            new XAttribute("prst", "horz"),
            new XElement(DmlNames.SolidFill, new XElement(DmlNames.SrgbColor, new XAttribute(DmlNames.AttributeValue, "000000")))
        );
        var parent = new XElement(DmlNames.Dml + "spPr", pattern);
        var fill = new FillFormat();
        FillParser.Parse(parent, fill);

        fill.Type.ShouldBe(FillType.Pattern);
        fill.Pattern.ShouldNotBeNull();
        fill.Pattern.Preset.ShouldBe(PatternPreset.HorizontalLines);
    }

    [Fact]
    public void Parse_BlipFill_SetsPictureType()
    {
        var blip = new XElement(DmlNames.BlipFill, new XElement(DmlNames.Blip));
        var parent = new XElement(DmlNames.Dml + "spPr", blip);
        var fill = new FillFormat();
        FillParser.Parse(parent, fill);
        fill.Type.ShouldBe(FillType.Picture);
        fill.Picture.ShouldNotBeNull();
    }

    [Fact]
    public void RoundTrip_SolidFill_ThroughWriterAndParser()
    {
        var original = new FillFormat();
        original.SetSolid(ColorSpec.FromRgb(0x33, 0x66, 0x99));

        var parent = new XElement(DmlNames.Dml + "spPr");
        FillWriter.Write(parent, original);
        var parsed = new FillFormat();
        FillParser.Parse(parent, parsed);

        parsed.Type.ShouldBe(FillType.Solid);
        parsed.Solid!.Color.Resolve(null).ShouldBe(0xFF336699u);
    }

    [Fact]
    public void RoundTrip_GradientStops_ThroughWriterAndParser()
    {
        var original = new FillFormat { Type = FillType.Gradient };
        var grad = new GradientFill { IsLinear = true, LinearAngleDegrees = 90 };
        grad.Stops.Add(new GradientStop(0.0, ColorSpec.FromRgb(0, 0, 0)));
        grad.Stops.Add(new GradientStop(1.0, ColorSpec.FromRgb(255, 255, 255)));
        original.Gradient = grad;

        var parent = new XElement(DmlNames.Dml + "spPr");
        FillWriter.Write(parent, original);
        var parsed = new FillFormat();
        FillParser.Parse(parent, parsed);

        parsed.Type.ShouldBe(FillType.Gradient);
        parsed.Gradient!.Stops.Count.ShouldBe(2);
        parsed.Gradient.IsLinear.ShouldBeTrue();
    }

    [Fact]
    public void Parse_GradientWithLinearAngle_ReadsAngle()
    {
        var gradient = new XElement(
            DmlNames.GradientFill,
            new XElement(
                DmlNames.GradientStopList,
                new XElement(
                    DmlNames.GradientStop,
                    new XAttribute("pos", "0"),
                    new XElement(DmlNames.SrgbColor, new XAttribute(DmlNames.AttributeValue, "000000"))
                )
            ),
            new XElement(
                DmlNames.LinearGradient,
                new XAttribute(DmlNames.AttributeRotation, 5_400_000),
                new XAttribute("scaled", "0")
            )
        );
        var parent = new XElement(DmlNames.Dml + "spPr", gradient);
        var fill = new FillFormat();
        FillParser.Parse(parent, fill);

        fill.Type.ShouldBe(FillType.Gradient);
        fill.Gradient!.IsLinear.ShouldBeTrue();
        // 5_400_000 60_000ths-of-a-degree = 90°.
        fill.Gradient.LinearAngleDegrees.ShouldBe(90.0, 0.001);
    }

    [Fact]
    public void Parse_GradientWithoutStopList_StillSetsGradientType()
    {
        var gradient = new XElement(DmlNames.GradientFill);
        var parent = new XElement(DmlNames.Dml + "spPr", gradient);
        var fill = new FillFormat();
        FillParser.Parse(parent, fill);

        fill.Type.ShouldBe(FillType.Gradient);
        fill.Gradient!.Stops.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_PatternFill_ReadsForegroundAndBackground()
    {
        // Foreground is a direct <a:solidFill> child; background is wrapped in <a:bgClr>.
        var pattern = new XElement(
            DmlNames.PatternFill,
            new XAttribute("prst", "cross"),
            new XElement(DmlNames.SolidFill, new XElement(DmlNames.SrgbColor, new XAttribute(DmlNames.AttributeValue, "112233"))),
            new XElement(DmlNames.Dml + "bgClr", new XElement(DmlNames.SolidFill, new XElement(DmlNames.SrgbColor, new XAttribute(DmlNames.AttributeValue, "445566"))))
        );
        var parent = new XElement(DmlNames.Dml + "spPr", pattern);
        var fill = new FillFormat();
        FillParser.Parse(parent, fill);

        fill.Type.ShouldBe(FillType.Pattern);
        fill.Pattern!.ForegroundColor.Resolve(null).ShouldBe(0xFF112233u);
        fill.Pattern.BackgroundColor.Resolve(null).ShouldBe(0xFF445566u);
    }

    [Fact]
    public void Parse_PatternFill_BgClrWithSchemeClr_ParsesCorrectly()
    {
        // Per ECMA-376, bgClr wraps a colour element such as schemeClr (not srgbClr directly).
        var pattern = new XElement(
            DmlNames.PatternFill,
            new XAttribute("prst", "pct50"),
            new XElement(DmlNames.SolidFill, new XElement(DmlNames.SrgbColor, new XAttribute(DmlNames.AttributeValue, "AABBCC"))),
            new XElement(DmlNames.Dml + "bgClr", new XElement(DmlNames.SchemeColor, new XAttribute(DmlNames.AttributeValue, "accent1")))
        );
        var parent = new XElement(DmlNames.Dml + "spPr", pattern);
        var fill = new FillFormat();
        FillParser.Parse(parent, fill);

        fill.Type.ShouldBe(FillType.Pattern);
        fill.Pattern!.ForegroundColor.Resolve(null).ShouldBe(0xFFAABBCCu);
        fill.Pattern.BackgroundColor.Type.ShouldBe(ColorSpecType.ThemeSlot);
        fill.Pattern.BackgroundColor.ThemeSlot.ShouldBe(ThemeColorSlot.Accent1);
    }

    [Fact]
    public void Parse_PatternFill_UnknownPreset_FallsBackToPercent5()
    {
        var pattern = new XElement(DmlNames.PatternFill, new XAttribute("prst", "totallyUnknown"));
        var parent = new XElement(DmlNames.Dml + "spPr", pattern);
        var fill = new FillFormat();
        FillParser.Parse(parent, fill);

        fill.Type.ShouldBe(FillType.Pattern);
        fill.Pattern!.Preset.ShouldBe(PatternPreset.Percent5);
    }

    [
        Theory,
        InlineData("pct5", PatternPreset.Percent5),
        InlineData("pct10", PatternPreset.Percent10),
        InlineData("pct20", PatternPreset.Percent20),
        InlineData("pct25", PatternPreset.Percent25),
        InlineData("pct30", PatternPreset.Percent30),
        InlineData("pct40", PatternPreset.Percent40),
        InlineData("pct50", PatternPreset.Percent50),
        InlineData("pct60", PatternPreset.Percent60),
        InlineData("pct70", PatternPreset.Percent70),
        InlineData("pct75", PatternPreset.Percent75),
        InlineData("pct80", PatternPreset.Percent80),
        InlineData("pct90", PatternPreset.Percent90),
        InlineData("horz", PatternPreset.HorizontalLines),
        InlineData("vert", PatternPreset.VerticalLines),
        InlineData("ltHorz", PatternPreset.LightHorizontal),
        InlineData("ltVert", PatternPreset.LightVertical),
        InlineData("ltDnDiag", PatternPreset.LightDownwardDiagonal),
        InlineData("ltUpDiag", PatternPreset.LightUpwardDiagonal),
        InlineData("dkHorz", PatternPreset.DarkHorizontal),
        InlineData("dkVert", PatternPreset.DarkVertical),
        InlineData("dkDnDiag", PatternPreset.DarkDownwardDiagonal),
        InlineData("dkUpDiag", PatternPreset.DarkUpwardDiagonal),
        InlineData("wdDnDiag", PatternPreset.WideDownwardDiagonal),
        InlineData("wdUpDiag", PatternPreset.WideUpwardDiagonal),
        InlineData("dashHorz", PatternPreset.DashedHorizontal),
        InlineData("dashVert", PatternPreset.DashedVertical),
        InlineData("dashDnDiag", PatternPreset.DashedDownwardDiagonal),
        InlineData("dashUpDiag", PatternPreset.DashedUpwardDiagonal),
        InlineData("smConfetti", PatternPreset.SmallConfetti),
        InlineData("lgConfetti", PatternPreset.LargeConfetti),
        InlineData("zigZag", PatternPreset.Zigzag),
        InlineData("wave", PatternPreset.Wave),
        InlineData("diagBrick", PatternPreset.DiagonalBrick),
        InlineData("horzBrick", PatternPreset.HorizontalBrick),
        InlineData("weave", PatternPreset.Weave),
        InlineData("plaid", PatternPreset.Plaid),
        InlineData("divot", PatternPreset.Divot),
        InlineData("dotGrid", PatternPreset.DottedGrid),
        InlineData("dotDmnd", PatternPreset.DottedDiamond),
        InlineData("shingle", PatternPreset.Shingle),
        InlineData("trellis", PatternPreset.Trellis),
        InlineData("sphere", PatternPreset.Sphere),
        InlineData("smGrid", PatternPreset.SmallGrid),
        InlineData("lgGrid", PatternPreset.LargeGrid),
        InlineData("smCheck", PatternPreset.SmallCheckerBoard),
        InlineData("lgCheck", PatternPreset.LargeCheckerBoard),
        InlineData("openDmnd", PatternPreset.OutlinedDiamond),
        InlineData("solidDmnd", PatternPreset.SolidDiamond)
    ]
    public void Parse_PatternFill_MapsPresetTokens(string token, PatternPreset expected)
    {
        var pattern = new XElement(DmlNames.PatternFill, new XAttribute("prst", token));
        var parent = new XElement(DmlNames.Dml + "spPr", pattern);
        var fill = new FillFormat();
        FillParser.Parse(parent, fill);

        fill.Pattern!.Preset.ShouldBe(expected);
    }

    [Fact]
    public void Parse_BlipFill_WithEmbedId_CapturesRelationshipId()
    {
        var blip = new XElement(
            DmlNames.BlipFill,
            new XElement(DmlNames.Blip, new XAttribute(PmlNames.RelationshipEmbed, "rId7"))
        );
        var parent = new XElement(DmlNames.Dml + "spPr", blip);
        var fill = new FillFormat();
        FillParser.Parse(parent, fill);

        fill.Type.ShouldBe(FillType.Picture);
        fill.Picture!.RelationshipId.ShouldBe("rId7");
    }

    [Fact]
    public void Parse_EmptyParent_LeavesFillUnchanged()
    {
        var parent = new XElement(DmlNames.Dml + "spPr");
        var fill = new FillFormat();
        FillParser.Parse(parent, fill);
        fill.Type.ShouldBe(FillType.None);
    }

    [Fact]
    public void RoundTrip_PatternFill_PreservesPreset()
    {
        var original = new FillFormat
        {
            Type = FillType.Pattern,
            Pattern = new PatternFill
            {
                Preset = PatternPreset.HorizontalLines,
                ForegroundColor = ColorSpec.FromRgb(0x10, 0x20, 0x30),
                BackgroundColor = ColorSpec.FromRgb(0xA0, 0xB0, 0xC0)
            }
        };

        var parent = new XElement(DmlNames.Dml + "spPr");
        FillWriter.Write(parent, original);
        var parsed = new FillFormat();
        FillParser.Parse(parent, parsed);

        parsed.Type.ShouldBe(FillType.Pattern);
        parsed.Pattern!.Preset.ShouldBe(PatternPreset.HorizontalLines);
    }

    [Fact]
    public void Write_GroupFill_EmitsGroupFillElement()
    {
        var parent = new XElement(DmlNames.Dml + "spPr");
        FillWriter.Write(parent, new FillFormat { Type = FillType.Group });
        parent.Element(DmlNames.GroupFill).ShouldNotBeNull();
    }

    [Fact]
    public void Write_NoneFill_EmitsNoFillElement()
    {
        var parent = new XElement(DmlNames.Dml + "spPr");
        var fill = new FillFormat();
        fill.SetNone();
        FillWriter.Write(parent, fill);
        parent.Element(DmlNames.NoFill).ShouldNotBeNull();
    }

    [Fact]
    public void Write_PictureFillWithImage_EmitsBlipWithRelationshipAndStretch()
    {
        var fill = new FillFormat
        {
            Type = FillType.Picture,
            Picture = new PictureFill
            {
                Image = new EmbeddedImage("image/png", new byte[] { 1, 2, 3 }) { RelationshipId = "rId9" }
            }
        };

        var parent = new XElement(DmlNames.Dml + "spPr");
        FillWriter.Write(parent, fill);

        var blipFill = parent.Element(DmlNames.BlipFill);
        blipFill.ShouldNotBeNull();
        blipFill.Element(DmlNames.Blip).ShouldNotBeNull();
        blipFill.Element(DmlNames.Stretch).ShouldNotBeNull();
    }
}
