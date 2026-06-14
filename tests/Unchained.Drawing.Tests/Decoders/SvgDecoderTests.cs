using System.Text;
using Shouldly;
using Unchained.Drawing.Decoders;
using Xunit;

namespace Unchained.Drawing.Tests.Decoders;

/// <summary>
///     Unit tests for <see cref="SvgDecoder" /> — rasterises SVG markup to an RGB buffer. Tests
///     feed hand-written SVG and assert dimensions and that painted shapes change pixels from the
///     white (255,255,255) background.
/// </summary>
public sealed class SvgDecoderTests
{
    private static byte[]? Decode(string svg, int w = 20, int h = 20) =>
        SvgDecoder.TryDecodeToRgb(Encoding.UTF8.GetBytes(svg), w, h, out _, out _);

    // ReSharper disable once BadListLineBreaks
    private static (byte R, byte G, byte B) PixelAt(
        IReadOnlyList<byte> rgb,
        int w,
        int x,
        int y
    )
    {
        var i = ((y * w) + x) * 3;
        return (rgb[i], rgb[i + 1], rgb[i + 2]);
    }

    private static bool AnyNonWhite(IEnumerable<byte> rgb) => rgb.Any(static t => t != 255);

    [Fact]
    public void InvalidXml_ReturnsNull() =>
        Decode("not xml at all <<<").ShouldBeNull();

    [Fact]
    public void EmptySvg_ProducesWhiteBuffer()
    {
        var rgb = Decode("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'></svg>");
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeFalse();
    }

    [Fact]
    public void Dimensions_MatchTarget()
    {
        SvgDecoder.TryDecodeToRgb(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'/>"u8,
            32,
            24,
            out var w,
            out var h
        );
        w.ShouldBe(32);
        h.ShouldBe(24);
    }

    [Fact]
    public void FilledRect_PaintsPixels()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<rect x='0' y='0' width='20' height='20' fill='#FF0000'/></svg>"
        );
        rgb.ShouldNotBeNull();
        PixelAt(rgb, 20, 10, 10).ShouldBe(((byte)255, (byte)0, (byte)0));
    }

    [Fact]
    public void Circle_PaintsCentre()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<circle cx='10' cy='10' r='8' fill='#00FF00'/></svg>"
        );
        rgb.ShouldNotBeNull();
        PixelAt(rgb, 20, 10, 10).ShouldBe(((byte)0, (byte)255, (byte)0));
    }

    [Fact]
    public void Ellipse_PaintsPixels()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<ellipse cx='10' cy='10' rx='9' ry='5' fill='#0000FF'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeTrue();
    }

    [Fact]
    public void Line_PaintsStroke()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<line x1='0' y1='0' x2='20' y2='20' stroke='#000000'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeTrue();
    }

    [Fact]
    public void Polygon_PaintsPixels()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<polygon points='2,2 18,2 10,18' fill='#FF00FF'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeTrue();
    }

    [Fact]
    public void Path_PaintsPixels()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<path d='M2 2 L18 2 L18 18 L2 18 Z' fill='#123456'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeTrue();
    }

    [Fact]
    public void Group_InheritsFill()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<g fill='#FF0000'><rect x='0' y='0' width='20' height='20'/></g></svg>"
        );
        rgb.ShouldNotBeNull();
        PixelAt(rgb, 20, 10, 10).ShouldBe(((byte)255, (byte)0, (byte)0));
    }

    [Fact]
    public void StyleAttribute_OverridesFill()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<rect x='0' y='0' width='20' height='20' style='fill:#00FF00'/></svg>"
        );
        rgb.ShouldNotBeNull();
        PixelAt(rgb, 20, 10, 10).ShouldBe(((byte)0, (byte)255, (byte)0));
    }

    [Fact]
    public void WidthHeightAttributes_UsedWhenNoViewBox()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' width='20' height='20'>" +
            "<rect x='0' y='0' width='20' height='20' fill='#808080'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeTrue();
    }

    [Fact]
    public void NamedColor_Resolved()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<rect x='0' y='0' width='20' height='20' fill='red'/></svg>"
        );
        rgb.ShouldNotBeNull();
        PixelAt(rgb, 20, 10, 10).ShouldBe(((byte)255, (byte)0, (byte)0));
    }

    [Fact]
    public void NoneFillAndStroke_LeavesBackground()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<rect x='5' y='5' width='10' height='10' fill='none' stroke='none'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeFalse();
    }
}
