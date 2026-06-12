using Shouldly;
using Unchained.Drawing.Constants;
using Unchained.Drawing.Decoders;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Rendering;

public sealed class JpegDecoderTests
{
    // A 16×16 baseline (4:4:4) JPEG with four coloured quadrants — red, green, blue, near-white.
    // Embedded as base64 so the test is self-contained and portable (no external file paths).
    private const string SampleJpegBase64 =
        "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAAQABADAREAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDxKvyo/v09yr8/P8RT4vr/AGSP6VP32r+Aj6g//9k=";

    [Fact]
    public void NonJpegBytes_ReturnsNull() =>
        JpegDecoder.TryDecodeToRgb([0x89, 0x50, 0x4E, 0x47], out _, out _).ShouldBeNull();

    [Fact]
    public void EmptyInput_ReturnsNull() =>
        JpegDecoder.TryDecodeToRgb(ReadOnlySpan<byte>.Empty, out _, out _).ShouldBeNull();

    [Fact]
    public void TruncatedJpeg_ReturnsNullNotThrow() =>
        JpegDecoder.TryDecodeToRgb([JpegMarkers.MarkerPrefix, JpegMarkers.Soi, JpegMarkers.MarkerPrefix, JpegMarkers.App0Jfif, JpegMarkers.ByteStuff], out _, out _).ShouldBeNull();

    [Fact]
    public void BaselineJpeg_DecodesToFullRgbBuffer()
    {
        var jpeg = Convert.FromBase64String(SampleJpegBase64);

        var rgb = JpegDecoder.TryDecodeToRgb(jpeg, out var w, out var h);

        rgb.ShouldNotBeNull();
        w.ShouldBe(16);
        h.ShouldBe(16);
        rgb.Length.ShouldBe(w * h * 3);
        // A multi-colour image must not decode to a single flat colour.
        rgb.Distinct().Count().ShouldBeGreaterThan(3);
    }

    [Fact]
    public void BaselineJpeg_RecoversQuadrantColours()
    {
        var jpeg = Convert.FromBase64String(SampleJpegBase64);
        var rgb = JpegDecoder.TryDecodeToRgb(jpeg, out var w, out _);
        rgb.ShouldNotBeNull();

        // Sample the centre of each quadrant. JPEG is lossy, so assert the dominant channel
        // rather than exact values.
        var (tlR, tlG, tlB) = Pixel(rgb, w, 4, 4);   // red quadrant
        tlR.ShouldBeGreaterThan(tlG);
        tlR.ShouldBeGreaterThan(tlB);

        var (trR, trG, trB) = Pixel(rgb, w, 12, 4);  // green quadrant
        trG.ShouldBeGreaterThan(trR);
        trG.ShouldBeGreaterThan(trB);

        var (blR, blG, blB) = Pixel(rgb, w, 4, 12);  // blue quadrant
        blB.ShouldBeGreaterThan(blR);
        blB.ShouldBeGreaterThan(blG);
    }

    private static (int R, int G, int B) Pixel(byte[] rgb, int width, int x, int y)
    {
        var i = (y * width + x) * 3;
        return (rgb[i], rgb[i + 1], rgb[i + 2]);
    }
}
