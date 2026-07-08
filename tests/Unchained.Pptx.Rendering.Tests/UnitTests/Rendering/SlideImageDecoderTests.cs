using System.Text;
using Shouldly;
using Unchained.Drawing;
using Unchained.Drawing.Encoders;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Rendering.Engine.Rasterizers;
using Xunit;

namespace Unchained.Pptx.Rendering.Tests.UnitTests.Rendering;

/// <summary>
///     Branch coverage for <see cref="SlideImageDecoder.TryDecodeImageToRgb" /> across every format
///     detector: empty data, PNG, JPEG, SVG, and undecodable bytes.
/// </summary>
public sealed class SlideImageDecoderTests
{
    private static EmbeddedImage Image(byte[] data, string contentType = "application/octet-stream") =>
        new(contentType, data);

    [Fact]
    public void TryDecode_EmptyData_ReturnsNull()
    {
        var result = SlideImageDecoder.TryDecodeImageToRgb(Image([]), out var w, out var h);
        result.ShouldBeNull();
        w.ShouldBe(0);
        h.ShouldBe(0);
    }

    [Fact]
    public void TryDecode_Png_DecodesToRgb()
    {
        var buffer = new RasterBuffer(6, 4);
        buffer.Clear(10, 20, 30);
        var png = PngEncoder.Encode(buffer);

        var result = SlideImageDecoder.TryDecodeImageToRgb(Image(png, "image/png"), out var w, out var h);

        result.ShouldNotBeNull();
        w.ShouldBe(6);
        h.ShouldBe(4);
    }

    [Fact]
    public void TryDecode_Jpeg_DecodesToRgb()
    {
        var buffer = new RasterBuffer(16, 16);
        buffer.Clear(200, 100, 50);
        var jpeg = JpegEncoder.Encode(buffer);

        var result = SlideImageDecoder.TryDecodeImageToRgb(Image(jpeg, "image/jpeg"), out var w, out var h);

        result.ShouldNotBeNull();
        w.ShouldBe(16);
        h.ShouldBe(16);
    }

    [Fact]
    public void TryDecode_Svg_RendersAtFixedResolution()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"32\" height=\"32\">" +
                           "<rect x=\"0\" y=\"0\" width=\"32\" height=\"32\" fill=\"#3366CC\"/></svg>";
        var bytes = Encoding.UTF8.GetBytes(svg);

        var result = SlideImageDecoder.TryDecodeImageToRgb(Image(bytes, "image/svg+xml"), out var w, out var h);

        result.ShouldNotBeNull();
        w.ShouldBeGreaterThan(0);
        h.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void TryDecode_SvgWithXmlDeclaration_IsDetected()
    {
        const string svg = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                           "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"16\" height=\"16\">" +
                           "<circle cx=\"8\" cy=\"8\" r=\"6\" fill=\"red\"/></svg>";
        var bytes = Encoding.UTF8.GetBytes(svg);

        var result = SlideImageDecoder.TryDecodeImageToRgb(Image(bytes, "image/svg+xml"), out _, out _);

        result.ShouldNotBeNull();
    }

    [Fact]
    public void TryDecode_UndecodableBytes_ReturnsNull()
    {
        // GIF magic — not handled by any decoder branch.
        var gif = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 0, 1, 0 };
        var result = SlideImageDecoder.TryDecodeImageToRgb(Image(gif, "image/gif"), out var w, out var h);
        result.ShouldBeNull();
        w.ShouldBe(0);
        h.ShouldBe(0);
    }

    [Fact]
    public void BlitScaledRgb_ScalesSourceIntoDestination()
    {
        var dst = new RasterBuffer(8, 8);
        // 2x2 source, packed RGB.
        var src = new byte[]
        {
            255, 0, 0, 0, 255, 0,
            0, 0, 255, 255, 255, 0
        };

        SlideImageDecoder.BlitScaledRgb(
            dst,
            src,
            2,
            2,
            0,
            0,
            8,
            8
        );

        // Top-left should be the first source pixel (red).
        var (r, g, b) = dst.GetPixelRgb(0, 0);
        r.ShouldBe((byte)255);
        g.ShouldBe((byte)0);
        b.ShouldBe((byte)0);
    }
}
