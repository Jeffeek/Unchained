using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Writing;

public sealed class FillWriterTests
{
    private static XElement WriteToParent(FillFormat fill)
    {
        var parent = new XElement("parent");
        FillWriter.Write(parent, fill);
        return parent;
    }

    [Fact]
    public void Write_NoneFill_EmitsNoFill()
    {
        var fill = new FillFormat();
        fill.SetNone();
        var parent = WriteToParent(fill);
        parent.Elements().Single().Name.LocalName.ShouldBe("noFill");
    }

    [Fact]
    public void Write_SolidFill_EmitsSolidFillWithColor()
    {
        var fill = new FillFormat();
        fill.SetSolid(ColorSpec.FromRgb(0xFF, 0x00, 0x00));
        var parent = WriteToParent(fill);
        var solid = parent.Elements().Single();
        solid.Name.LocalName.ShouldBe("solidFill");
        solid.Elements().Single().Name.LocalName.ShouldBe("srgbClr");
    }

    [Fact]
    public void Write_GradientFill_EmitsStops()
    {
        var fill = new FillFormat { Type = FillType.Gradient };
        var gradient = new GradientFill { IsLinear = true, LinearAngleDegrees = 90 };
        gradient.Stops.Add(new GradientStop(0.0, ColorSpec.FromRgb(0, 0, 0)));
        gradient.Stops.Add(new GradientStop(1.0, ColorSpec.FromRgb(255, 255, 255)));
        fill.Gradient = gradient;

        var parent = WriteToParent(fill);
        var grad = parent.Elements().Single();
        grad.Name.LocalName.ShouldBe("gradFill");
        var stops = grad.Descendants().Where(static e => e.Name.LocalName == "gs").ToList();
        stops.Count.ShouldBe(2);
        // Position 1.0 → 100000.
        stops[1].Attribute("pos")!.Value.ShouldBe("100000");
        grad.Elements().Any(static e => e.Name.LocalName == "lin").ShouldBeTrue();
    }

    [Fact]
    public void Write_RadialGradient_OmitsLinElement()
    {
        var fill = new FillFormat { Type = FillType.Gradient };
        var gradient = new GradientFill { IsLinear = false };
        gradient.Stops.Add(new GradientStop(0.0, ColorSpec.FromRgb(0, 0, 0)));
        fill.Gradient = gradient;

        var parent = WriteToParent(fill);
        var grad = parent.Elements().Single();
        grad.Elements().Any(static e => e.Name.LocalName == "lin").ShouldBeFalse();
    }

    [Fact]
    public void Write_PatternFill_EmitsPresetAndColors()
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
        var parent = WriteToParent(fill);
        var patt = parent.Elements().Single();
        patt.Name.LocalName.ShouldBe("pattFill");
        patt.Attribute("prst")!.Value.ShouldBe("horz");
        patt.Elements().Any(static e => e.Name.LocalName == "fgClr").ShouldBeTrue();
        patt.Elements().Any(static e => e.Name.LocalName == "bgClr").ShouldBeTrue();
    }

    [Fact]
    public void Write_GroupFill_EmitsGrpFill()
    {
        var fill = new FillFormat { Type = FillType.Group };
        var parent = WriteToParent(fill);
        parent.Elements().Single().Name.LocalName.ShouldBe("grpFill");
    }

    [Fact]
    public void Write_PictureFill_EmitsBlipFillWithStretch()
    {
        var image = new Unchained.Ooxml.Media.EmbeddedImage("image/png", new byte[] { 1 });
        var fill = new FillFormat
        {
            Type = FillType.Picture,
            Picture = new PictureFill { Image = image }
        };
        var parent = WriteToParent(fill);
        var blip = parent.Elements().Single();
        blip.Name.LocalName.ShouldBe("blipFill");
        blip.Descendants().Any(static e => e.Name.LocalName == "stretch").ShouldBeTrue();
    }
}
