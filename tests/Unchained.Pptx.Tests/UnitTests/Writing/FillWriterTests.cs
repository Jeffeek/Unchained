using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Writing;

/// <summary>
///     Round-trips <see cref="FillFormat" /> through <see cref="FillWriter" /> and
///     <see cref="FillParser" /> for every fill kind, covering both serializers.
/// </summary>
public sealed class FillWriterTests
{
    private static FillFormat RoundTrip(FillFormat fill)
    {
        var parent = new XElement(DmlNames.Dml + "spPr");
        FillWriter.Write(parent, fill);
        var result = new FillFormat();
        FillParser.Parse(parent, result);
        return result;
    }

    [Fact]
    public void Write_None_EmitsNoFill()
    {
        var parent = new XElement(DmlNames.Dml + "spPr");
        var fill = new FillFormat();
        fill.SetNone();
        FillWriter.Write(parent, fill);
        parent.Element(DmlNames.NoFill).ShouldNotBeNull();
    }

    [Fact]
    public void RoundTrip_None_Preserved() =>
        RoundTrip(new FillFormat()).Type.ShouldBe(FillType.None);

    [Fact]
    public void RoundTrip_Solid_PreservesColor()
    {
        var fill = new FillFormat();
        fill.SetSolid(ColorSpec.FromRgb(0x33, 0x66, 0x99));

        var result = RoundTrip(fill);
        result.Type.ShouldBe(FillType.Solid);
        result.Solid!.Color.Resolve(null).ShouldBe(0xFF336699u);
    }

    [Fact]
    public void RoundTrip_SolidThemeColor_PreservesSlot()
    {
        var fill = new FillFormat();
        fill.SetSolid(ColorSpec.FromTheme(ThemeColorSlot.Accent3));

        var result = RoundTrip(fill);
        result.Solid!.Color.Type.ShouldBe(ColorSpecType.ThemeSlot);
        result.Solid.Color.ThemeSlot.ShouldBe(ThemeColorSlot.Accent3);
    }

    [Fact]
    public void RoundTrip_Gradient_PreservesStops()
    {
        var fill = new FillFormat { Type = FillType.Gradient };
        var grad = new GradientFill { IsLinear = true, LinearAngleDegrees = 90 };
        grad.Stops.Add(new GradientStop(0.0, ColorSpec.FromRgb(0, 0, 0)));
        grad.Stops.Add(new GradientStop(1.0, ColorSpec.FromRgb(255, 255, 255)));
        fill.Gradient = grad;

        var result = RoundTrip(fill);
        result.Type.ShouldBe(FillType.Gradient);
        result.Gradient!.Stops.Count.ShouldBe(2);
        result.Gradient.IsLinear.ShouldBeTrue();
        result.Gradient.Stops[1].Position.ShouldBe(1.0, 0.001);
    }

    [Fact]
    public void RoundTrip_Pattern_PreservesPreset()
    {
        var fill = new FillFormat
        {
            Type = FillType.Pattern,
            Pattern = new PatternFill
            {
                Preset = PatternPreset.HorizontalLines,
                ForegroundColor = ColorSpec.FromRgb(0, 0, 0),
                BackgroundColor = ColorSpec.FromRgb(255, 255, 255)
            }
        };

        var result = RoundTrip(fill);
        result.Type.ShouldBe(FillType.Pattern);
        result.Pattern!.Preset.ShouldBe(PatternPreset.HorizontalLines);
    }

    [Fact]
    public void RoundTrip_Picture_PreservesType()
    {
        var fill = new FillFormat { Type = FillType.Picture, Picture = new PictureFill() };

        var result = RoundTrip(fill);
        result.Type.ShouldBe(FillType.Picture);
        result.Picture.ShouldNotBeNull();
    }

    [Fact]
    public void Write_Group_EmitsGrpFill()
        {
        var parent = new XElement(DmlNames.Dml + "spPr");
        FillWriter.Write(parent, new FillFormat { Type = FillType.Group });
        parent.Elements().Any(static e => e.Name.LocalName == "grpFill").ShouldBeTrue();
    }
}
