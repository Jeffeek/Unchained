using Shouldly;
using Xunit;

namespace Unchained.Drawing.Primitives.Tests;

/// <summary>Unit tests for <see cref="ColorMath" /> — pure colour-component arithmetic.</summary>
public sealed class ColorMathTests
{
    [Fact]
    public void CmykToRgb_PureBlack_K1_ProducesZero()
    {
        var (r, g, b) = ColorMath.CmykToRgb(0, 0, 0, 1);
        r.ShouldBe(0);
        g.ShouldBe(0);
        b.ShouldBe(0);
    }

    [Fact]
    public void CmykToRgb_AllZero_ProducesWhite()
    {
        var (r, g, b) = ColorMath.CmykToRgb(0, 0, 0, 0);
        r.ShouldBe(1);
        g.ShouldBe(1);
        b.ShouldBe(1);
    }

    [Fact]
    public void CmykToRgb_PureCyan_ProducesCyanRgb()
    {
        var (r, g, b) = ColorMath.CmykToRgb(1, 0, 0, 0);
        r.ShouldBe(0);
        g.ShouldBe(1);
        b.ShouldBe(1);
    }

    [
        Theory,
        InlineData(0.0, (byte)0),
        InlineData(1.0, (byte)255),
        InlineData(0.5, (byte)128), // 127.5 rounds to 128 (away-from-zero)
        InlineData(0.5019607843, (byte)128)
    ]
    public void ToByteRounded_RoundsToNearest(double value, byte expected) =>
        ColorMath.ToByteRounded(value).ShouldBe(expected);

    [
        Theory,
        InlineData(-1.0, (byte)0),
        InlineData(2.0, (byte)255)
    ]
    public void ToByteRounded_ClampsOutOfRange(double value, byte expected) =>
        ColorMath.ToByteRounded(value).ShouldBe(expected);

    [Fact]
    public void UnpackArgb_SplitsAllFourChannels()
    {
        var (a, r, g, b) = ColorMath.UnpackArgb(0x12345678u);
        a.ShouldBe((byte)0x12);
        r.ShouldBe((byte)0x34);
        g.ShouldBe((byte)0x56);
        b.ShouldBe((byte)0x78);
    }

    [Fact]
    public void UnpackArgb_OpaqueWhite()
    {
        var (a, r, g, b) = ColorMath.UnpackArgb(0xFFFFFFFFu);
        a.ShouldBe((byte)0xFF);
        r.ShouldBe((byte)0xFF);
        g.ShouldBe((byte)0xFF);
        b.ShouldBe((byte)0xFF);
    }
}
