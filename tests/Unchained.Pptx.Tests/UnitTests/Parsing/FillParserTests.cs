using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;
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
            new XElement(DmlNames.SrgbColor, new XAttribute("val", "FF0000"))
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
                    new XElement(DmlNames.SrgbColor, new XAttribute("val", "000000"))
                ),
                new XElement(
                    DmlNames.GradientStop,
                    new XAttribute("pos", "100000"),
                    new XElement(DmlNames.SrgbColor, new XAttribute("val", "FFFFFF"))
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
            new XElement(DmlNames.SolidFill, new XElement(DmlNames.SrgbColor, new XAttribute("val", "000000")))
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
}
