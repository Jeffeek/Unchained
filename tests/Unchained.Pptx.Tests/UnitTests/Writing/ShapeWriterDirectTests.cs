using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Writing;

/// <summary>
///     Branch coverage for <see cref="ShapeWriter" /> driven directly: the full preset-geometry
///     enum→token map, accessibility attributes (alt text, alt-text title, decorative extension),
///     click hyperlinks, and the unknown-shape <c>RawElement</c> passthrough.
/// </summary>
public sealed class ShapeWriterDirectTests
{
    private static AutoShape Auto(AutoShapeType type) => new()
    {
        ShapeType = type,
        X = new Emu(0),
        Y = new Emu(0),
        Width = Emu.FromInches(1),
        Height = Emu.FromInches(1)
    };

    private static string PresetOf(XContainer sp) =>
        sp.Descendants(DmlNames.PresetGeometry).Single().Attribute(DmlNames.AttributePreset)!.Value;

    [
        Theory,
        InlineData(AutoShapeType.Rectangle, "rect"),
        InlineData(AutoShapeType.RoundedRectangle, "roundRect"),
        InlineData(AutoShapeType.Ellipse, "ellipse"),
        InlineData(AutoShapeType.IsoscelesTriangle, "triangle"),
        InlineData(AutoShapeType.RightTriangle, "rtTriangle"),
        InlineData(AutoShapeType.Diamond, "diamond"),
        InlineData(AutoShapeType.Parallelogram, "parallelogram"),
        InlineData(AutoShapeType.Trapezoid, "trapezoid"),
        InlineData(AutoShapeType.Pentagon, "pentagon"),
        InlineData(AutoShapeType.Hexagon, "hexagon"),
        InlineData(AutoShapeType.Heptagon, "heptagon"),
        InlineData(AutoShapeType.Octagon, "octagon"),
        InlineData(AutoShapeType.Star4, "star4"),
        InlineData(AutoShapeType.Star5, "star5"),
        InlineData(AutoShapeType.Star6, "star6"),
        InlineData(AutoShapeType.Star8, "star8"),
        InlineData(AutoShapeType.RightArrow, "rightArrow"),
        InlineData(AutoShapeType.LeftArrow, "leftArrow"),
        InlineData(AutoShapeType.UpArrow, "upArrow"),
        InlineData(AutoShapeType.DownArrow, "downArrow"),
        InlineData(AutoShapeType.Plus, "plus"),
        InlineData(AutoShapeType.Donut, "donut"),
        InlineData(AutoShapeType.Heart, "heart"),
        InlineData(AutoShapeType.LightningBolt, "lightningBolt"),
        InlineData(AutoShapeType.Sun, "sun"),
        InlineData(AutoShapeType.Moon, "moon"),
        InlineData(AutoShapeType.Cloud, "cloud"),
        InlineData(AutoShapeType.Arc, "arc"),
        InlineData(AutoShapeType.Wave, "wave"),
        InlineData(AutoShapeType.FlowChartProcess, "flowChartProcess"),
        InlineData(AutoShapeType.FlowChartDecision, "flowChartDecision"),
        InlineData(AutoShapeType.FlowChartTerminator, "flowChartTerminator"),
        InlineData(AutoShapeType.MathPlus, "mathPlus"),
        InlineData(AutoShapeType.MathMinus, "mathMinus"),
        InlineData(AutoShapeType.MathMultiply, "mathMultiply"),
        InlineData(AutoShapeType.MathDivide, "mathDivide"),
        InlineData(AutoShapeType.MathEqual, "mathEqual"),
        InlineData(AutoShapeType.MathNotEqual, "mathNotEqual")
    ]
    public void Write_PresetGeometry_MapsEveryShapeType(AutoShapeType type, string expected)
    {
        var sp = ShapeWriter.Write(Auto(type));
        sp.ShouldNotBeNull();
        PresetOf(sp).ShouldBe(expected);
    }

    [Fact]
    public void Write_TextBox_AddsTxBoxAttribute()
    {
        var shape = Auto(AutoShapeType.Rectangle);
        shape.IsTextBox = true;
        var sp = ShapeWriter.Write(shape);
        sp!.Descendants(PmlNames.Pml + "cNvSpPr")
            .Single()
            .Attribute("txBox")!.Value.ShouldBe("1");
    }

    [Fact]
    public void Write_AltTextAndTitle_EmittedOnCNvPr()
    {
        var shape = Auto(AutoShapeType.Rectangle);
        shape.AltText = "described";
        shape.AltTextTitle = "the title";

        var cNvPr = ShapeWriter.Write(shape)!.Descendants(PmlNames.CommonNonVisualProperties).Single();
        cNvPr.Attribute("title")!.Value.ShouldBe("the title");
        cNvPr.Attribute(DmlNames.AttributeDescription)!.Value.ShouldBe("described");
    }

    [Fact]
    public void Write_Decorative_EmitsDecorativeExtension()
    {
        var shape = Auto(AutoShapeType.Rectangle);
        shape.IsDecorative = true;

        var sp = ShapeWriter.Write(shape);
        sp!.Descendants().Any(static e => e.Name.LocalName == "decorative").ShouldBeTrue();
    }

    [Fact]
    public void Write_ClickHyperlink_EmitsHlinkClick()
    {
        var shape = Auto(AutoShapeType.Rectangle);
        shape.ClickAction = new HyperlinkAction { Url = "https://example.com" };

        var sp = ShapeWriter.Write(shape);
        sp!.Descendants().Any(static e => e.Name.LocalName == "hlinkClick").ShouldBeTrue();
    }

    [Fact]
    public void Write_RotationAndFlips_EmittedOnXfrm()
    {
        var shape = Auto(AutoShapeType.Rectangle);
        shape.RotationDegrees = 45;
        shape.FlipHorizontal = true;
        shape.FlipVertical = true;

        var xfrm = ShapeWriter.Write(shape)!.Descendants(DmlNames.Transform).First();
        xfrm.Attribute(DmlNames.AttributeRotation).ShouldNotBeNull();
        xfrm.Attribute(DmlNames.AttributeFlipHorizontal)!.Value.ShouldBe("1");
        xfrm.Attribute(DmlNames.AttributeFlipVertical)!.Value.ShouldBe("1");
    }
}
