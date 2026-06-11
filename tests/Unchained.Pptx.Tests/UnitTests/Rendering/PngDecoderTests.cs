using Shouldly;
using Unchained.Drawing;
using Unchained.Drawing.Constants;
using Unchained.Drawing.Decoders;
using Unchained.Drawing.Encoders;
using Unchained.Pptx.Rendering.Engine;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Rendering;

public sealed class PngDecoderTests
{
    [Fact]
    public void DecodesEncoderRoundTrip_RecoversPixels()
    {
        // Build a known 4×3 buffer with distinct colours, encode to PNG, decode back.
        var buffer = new RasterBuffer(4, 3);
        buffer.SetPixel(0, 0, 255, 0, 0, 255);   // red
        buffer.SetPixel(1, 0, 0, 255, 0, 255);   // green
        buffer.SetPixel(2, 0, 0, 0, 255, 255);   // blue
        buffer.SetPixel(3, 0, 255, 255, 0, 255); // yellow
        buffer.SetPixel(0, 2, 10, 20, 30, 255);

        var png = PngEncoder.Encode(buffer);

        var rgb = PngDecoder.TryDecodeToRgb(png, out var w, out var h);

        rgb.ShouldNotBeNull();
        w.ShouldBe(4);
        h.ShouldBe(3);

        // Red at (0,0)
        PixelAt(rgb, w, 0, 0).ShouldBe((255, 0, 0));
        PixelAt(rgb, w, 1, 0).ShouldBe((0, 255, 0));
        PixelAt(rgb, w, 2, 0).ShouldBe((0, 0, 255));
        PixelAt(rgb, w, 3, 0).ShouldBe((255, 255, 0));
        PixelAt(rgb, w, 0, 2).ShouldBe((10, 20, 30));
    }

    [Fact]
    public void NonPngBytes_ReturnsNull() =>
        PngDecoder.TryDecodeToRgb([JpegMarkers.MarkerPrefix, JpegMarkers.Soi, JpegMarkers.MarkerPrefix, JpegMarkers.App0Jfif], out _, out _).ShouldBeNull();

    [Fact]
    public void EmptyInput_ReturnsNull() =>
        PngDecoder.TryDecodeToRgb(ReadOnlySpan<byte>.Empty, out _, out _).ShouldBeNull();

    private static (int, int, int) PixelAt(byte[] rgb, int width, int x, int y)
    {
        var i = ((y * width) + x) * 3;
        return (rgb[i], rgb[i + 1], rgb[i + 2]);
    }
}
