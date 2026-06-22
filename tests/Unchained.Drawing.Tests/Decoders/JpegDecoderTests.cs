using Shouldly;
using Unchained.Drawing.Constants;
using Unchained.Drawing.Decoders;
using Xunit;

namespace Unchained.Drawing.Tests.Decoders;

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
        JpegDecoder.TryDecodeToRgb(
                [
                    JpegConstants.MarkerPrefix, JpegConstants.Soi, JpegConstants.MarkerPrefix, JpegConstants.App0Jfif, JpegConstants.ByteStuff
                ],
                out _,
                out _
            )
            .ShouldBeNull();

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
        var (tlR, tlG, tlB) = Pixel(rgb, w, 4, 4); // red quadrant
        tlR.ShouldBeGreaterThan(tlG);
        tlR.ShouldBeGreaterThan(tlB);

        var (trR, trG, trB) = Pixel(rgb, w, 12, 4); // green quadrant
        trG.ShouldBeGreaterThan(trR);
        trG.ShouldBeGreaterThan(trB);

        var (blR, blG, blB) = Pixel(rgb, w, 4, 12); // blue quadrant
        blB.ShouldBeGreaterThan(blR);
        blB.ShouldBeGreaterThan(blG);
    }

    private static (int R, int G, int B) Pixel(
        IReadOnlyList<byte> rgb,
        int width,
        int x,
        int y
    )
    {
        var i = ((y * width) + x) * 3;
        return (rgb[i], rgb[i + 1], rgb[i + 2]);
    }

    // ── Marker / header error paths (crafted streams) ─────────────────────────

    [Fact]
    public void Eoi_BeforeScan_ReturnsNull() =>
        JpegDecoder.TryDecodeToRgb([JpegConstants.MarkerPrefix, JpegConstants.Soi, JpegConstants.MarkerPrefix, JpegConstants.Eoi], out _, out _).ShouldBeNull();

    [Fact]
    public void Sof2Progressive_ReturnsNull() =>
        JpegDecoder.TryDecodeToRgb([JpegConstants.MarkerPrefix, JpegConstants.Soi, JpegConstants.MarkerPrefix, JpegConstants.Sof2], out _, out _).ShouldBeNull();

    [Fact]
    public void NonMarkerBytesBetweenMarkers_AreSkipped() =>
        // 0x00 after SOI is not a marker prefix → skipped, then EOI ends.
        JpegDecoder.TryDecodeToRgb([JpegConstants.MarkerPrefix, JpegConstants.Soi, 0x00, 0x00, JpegConstants.MarkerPrefix, JpegConstants.Eoi], out _, out _).ShouldBeNull();

    [Fact]
    public void FillBytesBeforeMarker_AreSkipped() =>
        // Extra 0xFF fill bytes before the SOF2 marker exercise the fill-skip loop.
        JpegDecoder.TryDecodeToRgb([JpegConstants.MarkerPrefix, JpegConstants.Soi, JpegConstants.MarkerPrefix, JpegConstants.MarkerPrefix, JpegConstants.MarkerPrefix, JpegConstants.Sof2], out _, out _).ShouldBeNull();

    [Fact]
    public void RestartMarkerOutsideScan_IsSkipped() =>
        // RST0 (no payload) followed by EOI.
        JpegDecoder.TryDecodeToRgb([JpegConstants.MarkerPrefix, JpegConstants.Soi, JpegConstants.MarkerPrefix, JpegConstants.RstFirst, JpegConstants.MarkerPrefix, JpegConstants.Eoi], out _, out _)
            .ShouldBeNull();

    [Fact]
    public void StuffedByteMarker_IsSkipped() =>
        JpegDecoder.TryDecodeToRgb([JpegConstants.MarkerPrefix, JpegConstants.Soi, JpegConstants.MarkerPrefix, JpegConstants.ByteStuff, JpegConstants.MarkerPrefix, JpegConstants.Eoi], out _, out _)
            .ShouldBeNull();

    [Fact]
    public void UnknownSegment_IsSkipped() =>
        // APP0 segment with length 2 (no payload), then stream ends → loop exits with null.
        JpegDecoder.TryDecodeToRgb([JpegConstants.MarkerPrefix, JpegConstants.Soi, JpegConstants.MarkerPrefix, JpegConstants.App0Jfif, 0x00, 0x02], out _, out _).ShouldBeNull();

    [Fact]
    public void DefineRestartInterval_IsParsed() =>
        // DRI segment (len 4, interval 16), then EOI.
        JpegDecoder.TryDecodeToRgb(
                [JpegConstants.MarkerPrefix, JpegConstants.Soi, JpegConstants.MarkerPrefix, JpegConstants.Dri, 0x00, 0x04, 0x00, 0x10, JpegConstants.MarkerPrefix, JpegConstants.Eoi],
                out _,
                out _
            )
            .ShouldBeNull();

    [Fact]
    public void Sof0_NonEightBitPrecision_ReturnsNull()
    {
        // SOF0 len=11, precision=16 → unsupported.
        byte[] data =
        [
            JpegConstants.MarkerPrefix, JpegConstants.Soi,
            JpegConstants.MarkerPrefix, JpegConstants.Sof0, 0x00, 0x0B, 16, 0x00, 0x08, 0x00, 0x08, 0x01, 0x01, 0x11, 0x00
        ];
        JpegDecoder.TryDecodeToRgb(data, out _, out _).ShouldBeNull();
    }

    [Fact]
    public void Sof0_UnsupportedComponentCount_ReturnsNull()
    {
        // SOF0 with 2 components (only 1 or 3 supported).
        byte[] data =
        [
            JpegConstants.MarkerPrefix, JpegConstants.Soi,
            JpegConstants.MarkerPrefix, JpegConstants.Sof0, 0x00, 0x0E, 8, 0x00, 0x08, 0x00, 0x08, 0x02,
            0x01, 0x11, 0x00, 0x02, 0x11, 0x01
        ];
        JpegDecoder.TryDecodeToRgb(data, out _, out _).ShouldBeNull();
    }

    [Fact]
    public void Sos_WithoutPrecedingSof_ReturnsNull() =>
        // SOS reached while baseline=false → returns null immediately.
        JpegDecoder.TryDecodeToRgb([JpegConstants.MarkerPrefix, JpegConstants.Soi, JpegConstants.MarkerPrefix, JpegConstants.Sos], out _, out _).ShouldBeNull();

    [Fact]
    public void Sos_ReferencesUnknownComponent_ReturnsNull()
    {
        // Valid SOF0 (1 component id=1), then SOS referencing component id=99.
        byte[] data =
        [
            JpegConstants.MarkerPrefix, JpegConstants.Soi,
            JpegConstants.MarkerPrefix, JpegConstants.Sof0, 0x00, 0x0B, 8, 0x00, 0x08, 0x00, 0x08, 0x01, 0x01, 0x11, 0x00,
            JpegConstants.MarkerPrefix, JpegConstants.Sos, 0x00, 0x08, 0x01, 99, 0x00, 0x00, 0x3F, 0x00
        ];
        JpegDecoder.TryDecodeToRgb(data, out _, out _).ShouldBeNull();
    }

    [Fact]
    public void StreamEndsAfterSegment_ReturnsNull() =>
        // SOF0 parsed, then stream ends before any SOS → marker loop exits with null.
        JpegDecoder.TryDecodeToRgb(
                [JpegConstants.MarkerPrefix, JpegConstants.Soi, JpegConstants.MarkerPrefix, JpegConstants.Sof0, 0x00, 0x0B, 8, 0x00, 0x08, 0x00, 0x08, 0x01, 0x01, 0x11, 0x00],
                out _,
                out _
            )
            .ShouldBeNull();

    // ── Grayscale (1-component) + restart-interval paths ──────────────────────

    [Fact]
    public void GrayscaleJpeg_DecodesToReplicatedRgb()
    {
        var jpeg = GrayscaleJpegBuilder.Build(16, 16, gray: 200);

        var rgb = JpegDecoder.TryDecodeToRgb(jpeg, out var w, out var h);

        rgb.ShouldNotBeNull();
        w.ShouldBe(16);
        h.ShouldBe(16);
        rgb.Length.ShouldBe(w * h * 3);
        // Grayscale: each pixel's three channels are equal, and brighter than mid-grey.
        var (r, g, b) = Pixel(rgb, w, 8, 8);
        r.ShouldBe(g);
        g.ShouldBe(b);
        r.ShouldBeGreaterThan(128);
    }

    [Fact]
    public void GrayscaleJpeg_NonBlockAlignedSize_DecodesToExactDimensions()
    {
        var jpeg = GrayscaleJpegBuilder.Build(10, 6, gray: 100);

        var rgb = JpegDecoder.TryDecodeToRgb(jpeg, out var w, out var h);

        rgb.ShouldNotBeNull();
        w.ShouldBe(10);
        h.ShouldBe(6);
        rgb.Length.ShouldBe(10 * 6 * 3);
    }

    [Fact]
    public void GrayscaleJpeg_BrighterInput_ProducesBrighterOutput()
    {
        var dark = JpegDecoder.TryDecodeToRgb(GrayscaleJpegBuilder.Build(16, 16, gray: 60), out _, out _);
        var light = JpegDecoder.TryDecodeToRgb(GrayscaleJpegBuilder.Build(16, 16, gray: 220), out var w, out _);
        dark.ShouldNotBeNull();
        light.ShouldNotBeNull();

        Pixel(light, w, 8, 8).R.ShouldBeGreaterThan(Pixel(dark, w, 8, 8).R);
    }

    [Fact]
    public void GrayscaleJpeg_WithRestartInterval_DecodesAcrossRestarts()
    {
        // 32×32 = 16 MCUs; restart every 2 MCUs forces several RSTn markers mid-scan.
        var jpeg = GrayscaleJpegBuilder.Build(32, 32, gray: 150, restartInterval: 2);

        var rgb = JpegDecoder.TryDecodeToRgb(jpeg, out var w, out var h);

        rgb.ShouldNotBeNull();
        w.ShouldBe(32);
        h.ShouldBe(32);
        var (r, g, b) = Pixel(rgb, w, 20, 20);
        r.ShouldBe(g);
        g.ShouldBe(b);
    }
}
