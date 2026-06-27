using Unchained.Ooxml.Drawing;
using Unchained.Studio.Studio.Xlsx;

namespace Unchained.Studio.Tests.Helpers;

public sealed class XlsxColorTests
{
    [Fact]
    public void ToHex_Null_ReturnsFallback() => XlsxColor.ToHex(null).ShouldBe("#000000");

    [Fact]
    public void ToHex_NullWithCustomFallback_ReturnsCustom() => XlsxColor.ToHex(null, "#FFFFFF").ShouldBe("#FFFFFF");

    [Fact]
    public void ToHex_Rgb_FormatsUppercaseHex()
    {
        var color = ColorSpec.FromRgb(0x12, 0x34, 0x56);
        XlsxColor.ToHex(color).ShouldBe("#123456");
    }

    [Fact]
    public void ToHex_Red_FormatsCorrectly() => XlsxColor.ToHex(ColorSpec.FromRgb(255, 0, 0)).ShouldBe("#FF0000");

    [
        Theory,
        InlineData("#123456", 0x12, 0x34, 0x56),
        InlineData("123456", 0x12, 0x34, 0x56),
        InlineData("#FF0000", 0xFF, 0x00, 0x00)
    ]
    public void FromHex_ParsesRgb(string hex, byte r, byte g, byte b)
    {
        var color = XlsxColor.FromHex(hex);
        var argb = color.Resolve(null);
        ((byte)((argb >> 16) & 0xFF)).ShouldBe(r);
        ((byte)((argb >> 8) & 0xFF)).ShouldBe(g);
        ((byte)(argb & 0xFF)).ShouldBe(b);
    }

    [Fact]
    public void FromHex_ArgbEightChars_StripsAlpha()
    {
        // 8-char ARGB → RGB is taken from the last 6.
        var color = XlsxColor.FromHex("#FF123456");
        var argb = color.Resolve(null);
        ((byte)((argb >> 16) & 0xFF)).ShouldBe((byte)0x12);
        ((byte)((argb >> 8) & 0xFF)).ShouldBe((byte)0x34);
        ((byte)(argb & 0xFF)).ShouldBe((byte)0x56);
    }

    [
        Theory,
        InlineData("not-a-color"),
        InlineData("#12"),
        InlineData("")
    ]
    public void FromHex_Invalid_ReturnsBlack(string hex)
    {
        var color = XlsxColor.FromHex(hex);
        var argb = color.Resolve(null);
        (argb & 0x00FFFFFF).ShouldBe(0u);
    }

    [Fact]
    public void RoundTrip_PreservesColor()
    {
        var original = ColorSpec.FromRgb(0xAB, 0xCD, 0xEF);
        var hex = XlsxColor.ToHex(original);
        var parsed = XlsxColor.FromHex(hex);
        XlsxColor.ToHex(parsed).ShouldBe(hex);
    }
}
