using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Writing;

public sealed class Shape3DWriterTests
{
    [Fact]
    public void Write_Empty_ReturnsNull() => Shape3DWriter.Write(new Shape3DFormat()).ShouldBeNull();

    [Fact]
    public void Write_ExtrusionHeight_EmitsAttribute()
    {
        var threeD = new Shape3DFormat { ExtrusionHeight = new Emu(50_000) };
        var el = Shape3DWriter.Write(threeD);
        el.ShouldNotBeNull();
        el.Name.LocalName.ShouldBe("sp3d");
        el.Attribute("extrusionH")!.Value.ShouldBe("50000");
    }

    [Fact]
    public void Write_ContourWidthAndMaterial_EmitsAttributes()
    {
        var threeD = new Shape3DFormat
        {
            ContourWidth = new Emu(12_700),
            Material = "metal"
        };
        var el = Shape3DWriter.Write(threeD);
        el.ShouldNotBeNull();
        el.Attribute("contourW")!.Value.ShouldBe("12700");
        el.Attribute("prstMaterial")!.Value.ShouldBe("metal");
    }

    [Fact]
    public void Write_TopAndBottomBevel_EmitsBevelElements()
    {
        var threeD = new Shape3DFormat
        {
            TopBevel = new BevelFormat { Width = new Emu(38_100), Height = new Emu(38_100), Preset = "circle" },
            BottomBevel = new BevelFormat { Width = new Emu(10_000), Height = new Emu(10_000), Preset = "angle" }
        };
        var el = Shape3DWriter.Write(threeD);
        el.ShouldNotBeNull();
        var top = el.Elements().Single(static e => e.Name.LocalName == "bevelT");
        top.Attribute("prst")!.Value.ShouldBe("circle");
        top.Attribute("w")!.Value.ShouldBe("38100");
        var bottom = el.Elements().Single(static e => e.Name.LocalName == "bevelB");
        bottom.Attribute("prst")!.Value.ShouldBe("angle");
    }

    [Fact]
    public void Write_ExtrusionAndContourColors_EmitsColorElements()
    {
        var threeD = new Shape3DFormat
        {
            ExtrusionHeight = new Emu(1),
            ExtrusionColor = ColorSpec.FromRgb(0xFF, 0x00, 0x00),
            ContourColor = ColorSpec.FromRgb(0x00, 0xFF, 0x00)
        };
        var el = Shape3DWriter.Write(threeD);
        el.ShouldNotBeNull();
        el.Elements().Any(static e => e.Name.LocalName == "extrusionClr").ShouldBeTrue();
        el.Elements().Any(static e => e.Name.LocalName == "contourClr").ShouldBeTrue();
    }
}
