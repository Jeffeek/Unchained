using Shouldly;
using Unchained.Ooxml.Drawing;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Drawing;

public sealed class ColorSpecTests
{
    [Fact]
    public void FromRgb_TypeIsRgb()
    {
        var color = ColorSpec.FromRgb(0xFF, 0x00, 0x00);
        color.Type.ShouldBe(ColorSpecType.Rgb);
    }

    [Fact]
    public void FromRgb_Resolve_ReturnsCorrectArgb()
    {
        var color = ColorSpec.FromRgb(0xFF, 0x00, 0x00);
        color.Resolve(null).ShouldBe(0xFFFF0000u);
    }

    [Fact]
    public void FromArgb_Resolve_IncludesAlpha()
    {
        var color = ColorSpec.FromArgb(0x80, 0xFF, 0x00, 0x00);
        color.Resolve(null).ShouldBe(0x80FF0000u);
    }

    [Fact]
    public void FromTheme_TypeIsThemeSlot()
    {
        var color = ColorSpec.FromTheme(ThemeColorSlot.Accent1);
        color.Type.ShouldBe(ColorSpecType.ThemeSlot);
        color.ThemeSlot.ShouldBe(ThemeColorSlot.Accent1);
    }

    [Fact]
    public void FromTheme_DefaultModifiers_AreIdentity()
    {
        var color = ColorSpec.FromTheme(ThemeColorSlot.Dark1);
        color.LuminanceModifier.ShouldBe(1.0);
        color.LuminanceOffset.ShouldBe(0.0);
    }

    [Fact]
    public void FromTheme_WithScheme_ResolvesToSchemeColor()
    {
        var scheme = new ColorScheme
        {
            Accent1 = ColorSpec.FromRgb(0x44, 0x72, 0xC4)
        };
        var color = ColorSpec.FromTheme(ThemeColorSlot.Accent1);
        var resolved = color.Resolve(scheme);
        resolved.ShouldBe(0xFF4472C4u);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = ColorSpec.FromRgb(0x10, 0x20, 0x30);
        var b = ColorSpec.FromRgb(0x10, 0x20, 0x30);
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = ColorSpec.FromRgb(0x10, 0x20, 0x30);
        var b = ColorSpec.FromRgb(0xFF, 0x00, 0x00);
        (a == b).ShouldBeFalse();
    }

    [Fact]
    public void FromTheme_NullScheme_ReturnsFallbackGrey()
    {
        var color = ColorSpec.FromTheme(ThemeColorSlot.Accent1);
        var resolved = color.Resolve(null);
        resolved.ShouldBe(0xFF808080u);
    }
}
