using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Writing;

public sealed class ColorWriterTests
{
    [Fact]
    public void Write_Rgb_ProducesSrgbClrWithHex()
    {
        var el = ColorWriter.Write(ColorSpec.FromRgb(0x44, 0x72, 0xC4));
        el.Name.LocalName.ShouldBe("srgbClr");
        el.Attribute("val")!.Value.ShouldBe("4472C4");
    }

    [Fact]
    public void Write_RgbWithAlpha_AddsAlphaChild()
    {
        var el = ColorWriter.Write(ColorSpec.FromArgb(0x80, 0xFF, 0x00, 0x00));
        el.Name.LocalName.ShouldBe("srgbClr");
        el.Attribute("val")!.Value.ShouldBe("FF0000");
        var alpha = el.Elements().Single(static e => e.Name.LocalName == "alpha");
        // 0x80 / 255 * 100000 ≈ 50196.
        int.Parse(alpha.Attribute("val")!.Value).ShouldBeInRange(49000, 51000);
    }

    [Fact]
    public void Write_OpaqueRgb_HasNoAlphaChild()
    {
        var el = ColorWriter.Write(ColorSpec.FromRgb(0x10, 0x20, 0x30));
        el.Elements().ShouldBeEmpty();
    }

    [
        Theory,
        InlineData(ThemeColorSlot.Dark1, "dk1"),
        InlineData(ThemeColorSlot.Light1, "lt1"),
        InlineData(ThemeColorSlot.Dark2, "dk2"),
        InlineData(ThemeColorSlot.Light2, "lt2"),
        InlineData(ThemeColorSlot.Accent1, "accent1"),
        InlineData(ThemeColorSlot.Accent6, "accent6"),
        InlineData(ThemeColorSlot.Hyperlink, "hlink"),
        InlineData(ThemeColorSlot.FollowedHyperlink, "folHlink")
    ]
    public void Write_ThemeSlot_ProducesSchemeClr(ThemeColorSlot slot, string expected)
    {
        var el = ColorWriter.Write(ColorSpec.FromTheme(slot));
        el.Name.LocalName.ShouldBe("schemeClr");
        el.Attribute("val")!.Value.ShouldBe(expected);
    }

    [Fact]
    public void Write_ThemeSlotWithLuminanceModifier_AddsLumMod()
    {
        // ReSharper disable once RedundantArgumentDefaultValue
        var color = ColorSpec.FromTheme(ThemeColorSlot.Accent1, 0.5, 0.0);
        var el = ColorWriter.Write(color);
        var lumMod = el.Elements().SingleOrDefault(static e => e.Name.LocalName == "lumMod");
        lumMod.ShouldNotBeNull();
        lumMod.Attribute("val")!.Value.ShouldBe("50000");
    }

    [Fact]
    public void Write_ThemeSlotWithLuminanceOffset_AddsLumOff()
    {
        var color = ColorSpec.FromTheme(ThemeColorSlot.Accent1, 1.0, 0.2);
        var el = ColorWriter.Write(color);
        var lumOff = el.Elements().SingleOrDefault(static e => e.Name.LocalName == "lumOff");
        lumOff.ShouldNotBeNull();
        lumOff.Attribute("val")!.Value.ShouldBe("20000");
    }
}
