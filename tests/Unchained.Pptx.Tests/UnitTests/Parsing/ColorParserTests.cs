using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

public sealed class ColorParserTests
{
    private static XElement Wrap(XElement colorChild) => new(DmlNames.Dml + "wrap", colorChild);

    [Fact]
    public void Parse_SrgbClr_ReturnsRgb()
    {
        var xml = Wrap(new XElement(DmlNames.SrgbColor, new XAttribute("val", "4472C4")));
        var color = ColorParser.Parse(xml);
        color.Type.ShouldBe(ColorSpecType.Rgb);
        color.Resolve(null).ShouldBe(0xFF4472C4u);
    }

    [Fact]
    public void Parse_SrgbClrWithAlpha_ReadsAlpha()
    {
        var srgb = new XElement(DmlNames.SrgbColor,
            new XAttribute("val", "FF0000"),
            new XElement(DmlNames.Alpha, new XAttribute("val", "50000")));
        var color = ColorParser.Parse(Wrap(srgb));
        // 50000/100000 * 255 ≈ 128.
        var alpha = (color.Rgb >> 24) & 0xFF;
        ((int)alpha).ShouldBeInRange(126, 130);
    }

    [Fact]
    public void Parse_SchemeClr_ReturnsThemeSlot()
    {
        var xml = Wrap(new XElement(DmlNames.SchemeColor, new XAttribute("val", "accent1")));
        var color = ColorParser.Parse(xml);
        color.Type.ShouldBe(ColorSpecType.ThemeSlot);
        color.ThemeSlot.ShouldBe(ThemeColorSlot.Accent1);
    }

    [Fact]
    public void Parse_SchemeClrWithLumMod_ReadsModifier()
    {
        var scheme = new XElement(DmlNames.SchemeColor,
            new XAttribute("val", "dk1"),
            new XElement(DmlNames.LuminanceModifier, new XAttribute("val", "50000")));
        var color = ColorParser.Parse(Wrap(scheme));
        color.LuminanceModifier.ShouldBe(0.5, 0.001);
    }

    [Fact]
    public void Parse_SystemColor_UsesLastClr()
    {
        var sys = new XElement(DmlNames.SystemColor,
            new XAttribute("val", "windowText"),
            new XAttribute("lastClr", "00FF00"));
        var color = ColorParser.Parse(Wrap(sys));
        color.Resolve(null).ShouldBe(0xFF00FF00u);
    }

    [Fact]
    public void Parse_PresetColor_MapsKnownName()
    {
        var prst = new XElement(DmlNames.PresetColor, new XAttribute("val", "red"));
        var color = ColorParser.Parse(Wrap(prst));
        color.Resolve(null).ShouldBe(0xFFFF0000u);
    }

    [Fact]
    public void Parse_NoColorChild_ReturnsGreyFallback()
    {
        var color = ColorParser.Parse(new XElement(DmlNames.Dml + "empty"));
        color.Resolve(null).ShouldBe(0xFF808080u);
    }

    [
        Theory,
        InlineData(ThemeColorSlot.Dark2),
        InlineData(ThemeColorSlot.Light2),
        InlineData(ThemeColorSlot.Accent6),
        InlineData(ThemeColorSlot.Hyperlink),
        InlineData(ThemeColorSlot.FollowedHyperlink)
    ]
    public void RoundTrip_ThemeSlot_ThroughWriterAndParser(ThemeColorSlot slot)
    {
        var written = ColorWriter.Write(ColorSpec.FromTheme(slot));
        var parsed = ColorParser.Parse(new XElement(DmlNames.Dml + "wrap", written));
        parsed.ThemeSlot.ShouldBe(slot);
    }

    [Fact]
    public void RoundTrip_Rgb_ThroughWriterAndParser()
    {
        var written = ColorWriter.Write(ColorSpec.FromRgb(0x12, 0x34, 0x56));
        var parsed = ColorParser.Parse(new XElement(DmlNames.Dml + "wrap", written));
        parsed.Resolve(null).ShouldBe(0xFF123456u);
    }
}
