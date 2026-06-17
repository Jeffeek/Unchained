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

    [Fact]
    public void RectWithStroke_PaintsBorder()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<rect x='2' y='2' width='16' height='16' fill='none' stroke='#000000'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeTrue();
    }

    [Fact]
    public void CircleWithStroke_PaintsOutline()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<circle cx='10' cy='10' r='8' fill='none' stroke='#000000'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeTrue();
    }

    [Fact]
    public void Polyline_StrokePaintsPixels()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<polyline points='2,2 10,18 18,2' stroke='#000000' fill='none'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeTrue();
    }

    [Fact]
    public void Polygon_WithStroke_ClosesShape()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<polygon points='2,2 18,2 10,18' fill='none' stroke='#FF0000'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeTrue();
    }

    [Fact]
    public void PolygonTooFewPoints_LeavesBackground()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<polygon points='5,5' fill='#FF0000'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeFalse();
    }

    [Fact]
    public void Path_CubicBezier_PaintsPixels()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<path d='M2 10 C2 2 18 2 18 10 Z' fill='#102030'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeTrue();
    }

    [Fact]
    public void Path_QuadraticAndSmoothAndArc_PaintsPixels()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<path d='M2 2 Q10 18 18 2 S18 18 2 18 A4 4 0 0 1 10 10 Z' fill='#445566'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeTrue();
    }

    [Fact]
    public void Path_HorizontalAndVerticalLines_PaintsPixels()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<path d='M2 2 H18 V18 H2 Z' fill='#777777'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeTrue();
    }

    [Fact]
    public void Path_RelativeCommands_PaintsPixels()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<path d='m2 2 l16 0 l0 16 l-16 0 z' fill='#abcdef'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeTrue();
    }

    [Fact]
    public void Path_StrokeOnly_PaintsPixels()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<path d='M2 2 L18 18' fill='none' stroke='#000000'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeTrue();
    }

    [Fact]
    public void Path_EmptyData_LeavesBackground()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<path d='' fill='#000000'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeFalse();
    }

    [Fact]
    public void RgbFunctionColor_Resolved()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<rect x='0' y='0' width='20' height='20' fill='rgb(10, 20, 30)'/></svg>"
        );
        rgb.ShouldNotBeNull();
        PixelAt(rgb, 20, 10, 10).ShouldBe(((byte)10, (byte)20, (byte)30));
    }

    [Fact]
    public void HexShorthandColor_Expanded()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<rect x='0' y='0' width='20' height='20' fill='#0f0'/></svg>"
        );
        rgb.ShouldNotBeNull();
        PixelAt(rgb, 20, 10, 10).ShouldBe(((byte)0, (byte)255, (byte)0));
    }

    [Fact]
    public void UnknownNamedColor_FallsBackToDarkGrey()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<rect x='0' y='0' width='20' height='20' fill='chartreuse'/></svg>"
        );
        rgb.ShouldNotBeNull();
        PixelAt(rgb, 20, 10, 10).ShouldBe(((byte)80, (byte)80, (byte)80));
    }

    [
        Theory,
        InlineData("blue", 0, 0, 255),
        InlineData("yellow", 255, 255, 0),
        InlineData("orange", 255, 165, 0),
        InlineData("purple", 128, 0, 128),
        InlineData("grey", 128, 128, 128),
        InlineData("white", 255, 255, 255)
    ]
    public void NamedColors_MapToExpectedRgb(
        string name,
        byte r,
        byte g,
        byte b
    )
    {
        var rgb = Decode(
            $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            $"<rect x='0' y='0' width='20' height='20' fill='{name}'/></svg>"
        );
        rgb.ShouldNotBeNull();
        PixelAt(rgb, 20, 10, 10).ShouldBe((r, g, b));
    }

    [Fact]
    public void Opacity_BlendsTowardBackground()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<rect x='0' y='0' width='20' height='20' fill='#000000' opacity='0.5'/></svg>"
        );
        rgb.ShouldNotBeNull();
        // 50% black over white background → mid grey, not pure black or white.
        var (r, _, _) = PixelAt(rgb, 20, 10, 10);
        ((int)r).ShouldBeInRange(100, 160);
    }

    [Fact]
    public void StyleAttribute_SetsStroke()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<line x1='0' y1='0' x2='20' y2='20' style='stroke:#000000'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeTrue();
    }

    [Fact]
    public void ViewBoxOffset_TranslatesContent()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='10 10 20 20'>" +
            "<rect x='10' y='10' width='20' height='20' fill='#FF0000'/></svg>"
        );
        rgb.ShouldNotBeNull();
        PixelAt(rgb, 20, 10, 10).ShouldBe(((byte)255, (byte)0, (byte)0));
    }

    [Fact]
    public void PercentageAndPixelLengths_Parsed()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' width='20px' height='20px' viewBox='0 0 20 20'>" +
            "<rect x='0' y='0' width='20' height='20' fill='#00FF00'/></svg>"
        );
        rgb.ShouldNotBeNull();
        PixelAt(rgb, 20, 10, 10).ShouldBe(((byte)0, (byte)255, (byte)0));
    }

    [Fact]
    public void ZeroSizeRect_Skipped()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<rect x='5' y='5' width='0' height='0' fill='#FF0000'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeFalse();
    }

    [Fact]
    public void ZeroRadiusCircle_Skipped()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<circle cx='10' cy='10' r='0' fill='#FF0000'/></svg>"
        );
        rgb.ShouldNotBeNull();
        AnyNonWhite(rgb).ShouldBeFalse();
    }

    [Fact]
    public void NestedGroups_PropagateInheritance()
    {
        var rgb = Decode(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>" +
            "<g fill='#0000FF'><g><rect x='0' y='0' width='20' height='20'/></g></g></svg>"
        );
        rgb.ShouldNotBeNull();
        PixelAt(rgb, 20, 10, 10).ShouldBe(((byte)0, (byte)0, (byte)255));
    }

    [Fact]
    public void EmptyInput_ReturnsNull() =>
        SvgDecoder.TryDecodeToRgb(ReadOnlySpan<byte>.Empty, 10, 10, out _, out _).ShouldBeNull();
}
