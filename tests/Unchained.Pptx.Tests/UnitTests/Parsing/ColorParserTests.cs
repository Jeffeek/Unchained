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
        var srgb = new XElement(
            DmlNames.SrgbColor,
            new XAttribute("val", "FF0000"),
            new XElement(DmlNames.Alpha, new XAttribute("val", "50000"))
        );
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
        var scheme = new XElement(
            DmlNames.SchemeColor,
            new XAttribute("val", "dk1"),
            new XElement(DmlNames.LuminanceModifier, new XAttribute("val", "50000"))
        );
        var color = ColorParser.Parse(Wrap(scheme));
        color.LuminanceModifier.ShouldBe(0.5, 0.001);
    }

    [Fact]
    public void Parse_SystemColor_UsesLastClr()
    {
        var sys = new XElement(
            DmlNames.SystemColor,
            new XAttribute("val", "windowText"),
            new XAttribute("lastClr", "00FF00")
        );
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

    [Fact]
    public void Parse_SrgbClrWithInvalidHex_FallsThroughToGrey()
    {
        var xml = Wrap(new XElement(DmlNames.SrgbColor, new XAttribute("val", "NOTHEX")));
        var color = ColorParser.Parse(xml);
        color.Resolve(null).ShouldBe(0xFF808080u);
    }

    [Fact]
    public void Parse_SystemColorWithInvalidLastClr_FallsThroughToGrey()
    {
        var sys = new XElement(
            DmlNames.SystemColor,
            new XAttribute("val", "windowText"),
            new XAttribute("lastClr", "ZZZZZZ")
        );
        var color = ColorParser.Parse(Wrap(sys));
        color.Resolve(null).ShouldBe(0xFF808080u);
    }

    [Fact]
    public void Parse_SchemeClrWithLumOff_ReadsOffset()
    {
        var scheme = new XElement(
            DmlNames.SchemeColor,
            new XAttribute("val", "accent2"),
            new XElement(DmlNames.LuminanceOffset, new XAttribute("val", "20000"))
        );
        var color = ColorParser.Parse(Wrap(scheme));
        color.LuminanceOffset.ShouldBe(0.2, 0.001);
    }

    [Fact]
    public void Parse_UnknownSchemeSlot_DefaultsToDark1()
    {
        var xml = Wrap(new XElement(DmlNames.SchemeColor, new XAttribute("val", "bogus")));
        var color = ColorParser.Parse(xml);
        color.ThemeSlot.ShouldBe(ThemeColorSlot.Dark1);
    }

    [
        Theory,
        InlineData("dk1", ThemeColorSlot.Dark1),
        InlineData("lt1", ThemeColorSlot.Light1),
        InlineData("dk2", ThemeColorSlot.Dark2),
        InlineData("lt2", ThemeColorSlot.Light2),
        InlineData("accent1", ThemeColorSlot.Accent1),
        InlineData("accent2", ThemeColorSlot.Accent2),
        InlineData("accent3", ThemeColorSlot.Accent3),
        InlineData("accent4", ThemeColorSlot.Accent4),
        InlineData("accent5", ThemeColorSlot.Accent5),
        InlineData("accent6", ThemeColorSlot.Accent6),
        InlineData("hlink", ThemeColorSlot.Hyperlink),
        InlineData("folHlink", ThemeColorSlot.FollowedHyperlink)
    ]
    public void Parse_SchemeSlot_MapsAllSlots(string name, ThemeColorSlot expected)
    {
        var xml = Wrap(new XElement(DmlNames.SchemeColor, new XAttribute("val", name)));
        ColorParser.Parse(xml).ThemeSlot.ShouldBe(expected);
    }

    [
        Theory,
        InlineData("white", 0xFFFFFFFFu),
        InlineData("black", 0xFF000000u),
        InlineData("green", 0xFF008000u),
        InlineData("blue", 0xFF0000FFu),
        InlineData("yellow", 0xFFFFFF00u),
        InlineData("cyan", 0xFF00FFFFu),
        InlineData("magenta", 0xFFFF00FFu),
        InlineData("orange", 0xFFFFA500u),
        InlineData("purple", 0xFF800080u),
        InlineData("gray", 0xFF808080u),
        InlineData("grey", 0xFF808080u),
        InlineData("unknownname", 0xFF808080u)
    ]
    public void Parse_PresetColor_MapsAllKnownNames(string name, uint expected)
    {
        var prst = new XElement(DmlNames.PresetColor, new XAttribute("val", name));
        var color = ColorParser.Parse(Wrap(prst));
        color.Resolve(null).ShouldBe(expected);
    }
}
