using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

public sealed class Shape3DParserTests
{
    [Fact]
    public void Parse_NoSp3d_LeavesEmpty()
    {
        var threeD = new Shape3DFormat();
        Shape3DParser.Parse(new XElement(DmlNames.Dml + "spPr"), threeD);
        threeD.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Parse_NullParent_LeavesEmpty()
    {
        var threeD = new Shape3DFormat();
        Shape3DParser.Parse(null, threeD);
        threeD.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Parse_Attributes_ReadExtrusionContourMaterial()
    {
        var sp3d = new XElement(
            DmlNames.Dml + "sp3d",
            new XAttribute("extrusionH", "50000"),
            new XAttribute("contourW", "12700"),
            new XAttribute("prstMaterial", "metal")
        );
        var parent = new XElement(DmlNames.Dml + "spPr", sp3d);
        var threeD = new Shape3DFormat();
        Shape3DParser.Parse(parent, threeD);

        threeD.ExtrusionHeight.Value.ShouldBe(50_000);
        threeD.ContourWidth.Value.ShouldBe(12_700);
        threeD.Material.ShouldBe("metal");
    }

    [Fact]
    public void Parse_Bevels_ReadTopAndBottom()
    {
        var sp3d = new XElement(
            DmlNames.Dml + "sp3d",
            new XElement(
                DmlNames.Dml + "bevelT",
                new XAttribute("w", "38100"),
                new XAttribute("h", "38100"),
                new XAttribute("prst", "circle")
            ),
            new XElement(
                DmlNames.Dml + "bevelB",
                new XAttribute("w", "10000"),
                new XAttribute("h", "5000"),
                new XAttribute("prst", "angle")
            )
        );
        var parent = new XElement(DmlNames.Dml + "spPr", sp3d);
        var threeD = new Shape3DFormat();
        Shape3DParser.Parse(parent, threeD);

        threeD.TopBevel.ShouldNotBeNull();
        threeD.TopBevel.Width.Value.ShouldBe(38_100);
        threeD.TopBevel.Preset.ShouldBe("circle");
        threeD.BottomBevel.ShouldNotBeNull();
        threeD.BottomBevel.Preset.ShouldBe("angle");
    }

    [Fact]
    public void Parse_ExtrusionAndContourColours_AreParsed()
    {
        var sp3d = new XElement(
            DmlNames.Dml + "sp3d",
            new XElement(DmlNames.Dml + "extrusionClr", new XElement(DmlNames.Dml + "srgbClr", new XAttribute("val", "FF0000"))),
            new XElement(DmlNames.Dml + "contourClr", new XElement(DmlNames.Dml + "srgbClr", new XAttribute("val", "00FF00")))
        );
        var parent = new XElement(DmlNames.Dml + "spPr", sp3d);
        var threeD = new Shape3DFormat();
        Shape3DParser.Parse(parent, threeD);

        threeD.ExtrusionColor.ShouldNotBeNull();
        threeD.ContourColor.ShouldNotBeNull();
    }

    [Fact]
    public void RoundTrip_ThroughWriterAndParser()
    {
        var original = new Shape3DFormat
        {
            ExtrusionHeight = new Emu(40_000),
            Material = "plastic",
            TopBevel = new BevelFormat { Width = new Emu(20_000), Height = new Emu(20_000), Preset = "slope" }
        };
        var written = Shape3DWriter.Write(original);
        written.ShouldNotBeNull();

        var parent = new XElement(DmlNames.Dml + "spPr", written);
        var parsed = new Shape3DFormat();
        Shape3DParser.Parse(parent, parsed);

        parsed.ExtrusionHeight.Value.ShouldBe(40_000);
        parsed.Material.ShouldBe("plastic");
        parsed.TopBevel.ShouldNotBeNull();
        parsed.TopBevel.Preset.ShouldBe("slope");
    }
}
