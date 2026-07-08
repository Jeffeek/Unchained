using System.Text;
using Shouldly;
using Unchained.Drawing.Constants;
using Unchained.Drawing.Encoders;
using Xunit;

namespace Unchained.Drawing.Tests.Encoders;

public sealed class PngEncoderTests
{
    /// <summary>The 8-byte PNG file signature (magic bytes).</summary>
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>Extracts the pixel width from a PNG byte array (IHDR bytes 16–19).</summary>
    private static int PngWidth(IReadOnlyList<byte> png) =>
        (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];

    /// <summary>Extracts the pixel height from a PNG byte array (IHDR bytes 20–23).</summary>
    private static int PngHeight(IReadOnlyList<byte> png) =>
        (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];

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
        png[..8].ShouldBe(PngSignature);
    }

    [Fact]
    public void Encode_1x1Buffer_IhdrContainsCorrectWidth()
    {
        var png = PngEncoder.Encode(SinglePixel(0, 255, 0));
        // IHDR starts at byte 16 (after 8-byte sig + 4-byte len + 4-byte "IHDR")
        // Width is at bytes 16..19
        var width = PngWidth(png);
        width.ShouldBe(1);
    }

    [Fact]
    public void Encode_1x1Buffer_IhdrContainsCorrectHeight()
    {
        var png = PngEncoder.Encode(SinglePixel(0, 0, 255));
        var height = PngHeight(png);
        height.ShouldBe(1);
    }

    [Fact]
    public void Encode_10x20Buffer_CorrectDimensions()
    {
        var buf = new RasterBuffer(10, 20);
        buf.Clear();
        var png = PngEncoder.Encode(buf);
        var width = PngWidth(png);
        var height = PngHeight(png);
        width.ShouldBe(10);
        height.ShouldBe(20);
    }

    [Fact]
    public void Encode_256x128Buffer_IHDRBytesAreCorrect()
    {
        var buf = new RasterBuffer(256, 128);
        buf.Clear(128, 128, 128);
        var png = PngEncoder.Encode(buf);
        // IHDR width at bytes 16..19 = [0, 0, 1, 0] (256 = 0x0100)
        png[16].ShouldBe((byte)0);
        png[17].ShouldBe((byte)0);
        png[18].ShouldBe((byte)1);
        png[19].ShouldBe((byte)0);
        // IHDR height at bytes 20..23 = [0, 0, 0, 128] (128 = 0x80)
        png[20].ShouldBe((byte)0);
        png[21].ShouldBe((byte)0);
        png[22].ShouldBe((byte)0);
        png[23].ShouldBe((byte)128);
    }

    [Fact]
    public void Encode_Buffer_EndsWithIENDChunk()
    {
        var png = PngEncoder.Encode(SinglePixel(128, 128, 128));
        // Last 12 bytes: 4-byte len (0) + "IEND" + 4-byte CRC
        var iendStart = png.Length - 12;
        iendStart.ShouldBeGreaterThanOrEqualTo(0);

        var iendType = new[] { png[iendStart + 4], png[iendStart + 5], png[iendStart + 6], png[iendStart + 7] };
        iendType.ShouldBe(Encoding.ASCII.GetBytes(PngConstants.IEND));
    }

    [Fact]
    public void Encode_NonEmptyBuffer_ProducesNonEmptyResult()
    {
        var buf = new RasterBuffer(5, 5);
        buf.Clear(200, 200, 200);
        PngEncoder.Encode(buf).Length.ShouldBeGreaterThan(0);
    }
}
