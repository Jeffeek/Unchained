using Shouldly;
using Unchained.Ooxml.Drawing;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Drawing;

public sealed class ColorSpecResolveTests
{
    [Fact]
    public void Resolve_Rgb_NoTransform_ReturnsRawArgb() =>
        ColorSpec.FromRgb(0x12, 0x34, 0x56).Resolve(null).ShouldBe(0xFF123456u);

    [Fact]
    public void Resolve_ThemeSlot_ThroughScheme_ReturnsSlotColor()
    {
        var scheme = new ColorScheme { Accent1 = ColorSpec.FromRgb(0x44, 0x72, 0xC4) };
        ColorSpec.FromTheme(ThemeColorSlot.Accent1).Resolve(scheme).ShouldBe(0xFF4472C4u);
    }

    [Fact]
    public void Resolve_LuminanceModifierZero_DarkensToBlack()
    {
        // White with a luminance multiplier of 0 → black (alpha preserved).
        var color = ColorSpec.FromTheme(ThemeColorSlot.Light1, luminanceModifier: 0.0);
        var scheme = new ColorScheme { Light1 = ColorSpec.FromRgb(0xFF, 0xFF, 0xFF) };
        color.Resolve(scheme).ShouldBe(0xFF000000u);
    }

    [Fact]
    public void Resolve_LuminanceOffsetOne_LightensToWhite()
    {
        // Black with a luminance offset of +1.0 → white.
        var color = ColorSpec.FromTheme(ThemeColorSlot.Dark1, luminanceModifier: 1.0, luminanceOffset: 1.0);
        var scheme = new ColorScheme { Dark1 = ColorSpec.FromRgb(0x00, 0x00, 0x00) };
        color.Resolve(scheme).ShouldBe(0xFFFFFFFFu);
    }

    [Fact]
    public void Resolve_LuminanceModifier_PreservesHue()
    {
        // Dimming a pure-red theme colour keeps it red-ish (G and B stay 0).
        var color = ColorSpec.FromTheme(ThemeColorSlot.Accent1, luminanceModifier: 0.5);
        var scheme = new ColorScheme { Accent1 = ColorSpec.FromRgb(0xFF, 0x00, 0x00) };
        var argb = color.Resolve(scheme);
        var red = (argb >> 16) & 0xFF;
        var green = (argb >> 8) & 0xFF;
        var blue = argb & 0xFF;
        red.ShouldBeGreaterThan(0u);
        green.ShouldBe(0u);
        blue.ShouldBe(0u);
    }

    [Fact]
    public void ToString_Rgb_ShowsHex() =>
        ColorSpec.FromArgb(0xFF, 0x12, 0x34, 0x56).ToString().ShouldBe("#FF123456");

    [Fact]
    public void ToString_Theme_ShowsSlotAndTransforms()
    {
        var s = ColorSpec.FromTheme(ThemeColorSlot.Accent2, luminanceModifier: 0.75, luminanceOffset: 0.1).ToString();
        s.ShouldContain("Accent2");
        s.ShouldContain("mod=");
        s.ShouldContain("off=");
    }

    [Fact]
    public void Inequality_DifferentColors_True() =>
        (ColorSpec.FromRgb(1, 2, 3) != ColorSpec.FromRgb(4, 5, 6)).ShouldBeTrue();

    [Fact]
    public void GetHashCode_EqualColors_Match()
    {
        var a = ColorSpec.FromRgb(0x10, 0x20, 0x30);
        var b = ColorSpec.FromRgb(0x10, 0x20, 0x30);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Equals_BoxedObject_Works()
    {
        object boxed = ColorSpec.FromRgb(1, 2, 3);
        ColorSpec.FromRgb(1, 2, 3).Equals(boxed).ShouldBeTrue();
        // ReSharper disable once SuspiciousTypeConversion.Global
        ColorSpec.FromRgb(1, 2, 3).Equals("not a color").ShouldBeFalse();
    }
}
