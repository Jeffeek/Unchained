using Shouldly;
using Unchained.Drawing;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Rendering;

public sealed class PngEncoderTests
{
    private static RasterBuffer SinglePixel(byte r, byte g, byte b)
    {
        var buf = new RasterBuffer(1, 1);
        buf.Clear(r, g, b);
        return buf;
    }

    [Fact]
    public void Encode_1x1Buffer_StartsWithPngSignature()
    {
        var png = PngEncoder.Encode(SinglePixel(255, 0, 0));
        png.Length.ShouldBeGreaterThan(8);
        png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    [Fact]
    public void Encode_1x1Buffer_IhdrContainsCorrectWidth()
    {
        var png = PngEncoder.Encode(SinglePixel(0, 255, 0));
        // IHDR starts at byte 16 (after 8-byte sig + 4-byte len + 4-byte "IHDR")
        // Width is at bytes 16..19
        var width = PdfTestConstants.PngWidth(png);
        width.ShouldBe(1);
    }

    [Fact]
    public void Encode_1x1Buffer_IhdrContainsCorrectHeight()
    {
        var png = PngEncoder.Encode(SinglePixel(0, 0, 255));
        var height = PdfTestConstants.PngHeight(png);
        height.ShouldBe(1);
    }

    [Fact]
    public void Encode_10x20Buffer_CorrectDimensions()
    {
        var buf = new RasterBuffer(10, 20);
        buf.Clear();
        var png = PngEncoder.Encode(buf);
        var width = PdfTestConstants.PngWidth(png);
        var height = PdfTestConstants.PngHeight(png);
        width.ShouldBe(10);
        height.ShouldBe(20);
    }

    [Fact]
    public void Encode_Buffer_EndsWithIENDChunk()
    {
        var png = PngEncoder.Encode(SinglePixel(128, 128, 128));
        // Last 12 bytes: 4-byte len (0) + "IEND" + 4-byte CRC
        var iendStart = png.Length - 12;
        iendStart.ShouldBeGreaterThanOrEqualTo(0);

        var iendType = new[] { png[iendStart + 4], png[iendStart + 5], png[iendStart + 6], png[iendStart + 7] };
        iendType.ShouldBe("IEND"u8.ToArray());
    }

    [Fact]
    public void Encode_NonEmptyBuffer_ProducesNonEmptyResult()
    {
        var buf = new RasterBuffer(5, 5);
        buf.Clear(200, 200, 200);
        PngEncoder.Encode(buf).Length.ShouldBeGreaterThan(0);
    }
}
