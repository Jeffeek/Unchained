using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Shouldly;
using Unchained.Drawing.Constants;
using Unchained.Drawing.Decoders;
using Unchained.Drawing.Encoders;
using Xunit;

namespace Unchained.Drawing.Tests.Decoders;

public sealed class PngDecoderTests
{
    [Fact]
    public void DecodesEncoderRoundTrip_RecoversPixels()
    {
        // Build a known 4×3 buffer with distinct colours, encode to PNG, decode back.
        var buffer = new RasterBuffer(4, 3);
        buffer.SetPixel(
            0,
            0,
            255,
            0,
            0,
            255
        ); // red
        buffer.SetPixel(
            1,
            0,
            0,
            255,
            0,
            255
        ); // green
        buffer.SetPixel(
            2,
            0,
            0,
            0,
            255,
            255
        ); // blue
        buffer.SetPixel(
            3,
            0,
            255,
            255,
            0,
            255
        ); // yellow
        buffer.SetPixel(
            0,
            2,
            10,
            20,
            30,
            255
        );

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
        PngDecoder.TryDecodeToRgb([JpegConstants.MarkerPrefix, JpegConstants.Soi, JpegConstants.MarkerPrefix, JpegConstants.App0Jfif], out _, out _)
            .ShouldBeNull();

    [Fact]
    public void EmptyInput_ReturnsNull() =>
        PngDecoder.TryDecodeToRgb(ReadOnlySpan<byte>.Empty, out _, out _).ShouldBeNull();

    [Fact]
    public void Grayscale_DecodesToEqualChannels()
    {
        // 2×2 grayscale, filter None on every row.
        byte[] rows = [10, 200, 30, 220];
        var png = BuildPng(
            2,
            2,
            colorType: 0,
            channels: 1,
            scanlines: rows
        );

        var rgb = PngDecoder.TryDecodeToRgb(png, out var w, out var h);
        rgb.ShouldNotBeNull();
        w.ShouldBe(2);
        h.ShouldBe(2);
        PixelAt(rgb, w, 0, 0).ShouldBe((10, 10, 10));
        PixelAt(rgb, w, 1, 0).ShouldBe((200, 200, 200));
        PixelAt(rgb, w, 0, 1).ShouldBe((30, 30, 30));
    }

    [Fact]
    public void Truecolor_DecodesRgb()
    {
        // 2×1 truecolor.
        byte[] rows = [255, 0, 0, 0, 255, 0];
        var png = BuildPng(
            2,
            1,
            colorType: 2,
            channels: 3,
            scanlines: rows
        );

        var rgb = PngDecoder.TryDecodeToRgb(png, out var w, out _);
        rgb.ShouldNotBeNull();
        PixelAt(rgb, w, 0, 0).ShouldBe((255, 0, 0));
        PixelAt(rgb, w, 1, 0).ShouldBe((0, 255, 0));
    }

    [Fact]
    public void GrayscaleAlpha_UsesGrayChannelOnly()
    {
        // 2×1 grayscale+alpha: (gray, alpha) per pixel.
        byte[] rows = [40, 0, 90, 255];
        var png = BuildPng(
            2,
            1,
            colorType: 4,
            channels: 2,
            scanlines: rows
        );

        var rgb = PngDecoder.TryDecodeToRgb(png, out var w, out _);
        rgb.ShouldNotBeNull();
        PixelAt(rgb, w, 0, 0).ShouldBe((40, 40, 40));
        PixelAt(rgb, w, 1, 0).ShouldBe((90, 90, 90));
    }

    [Fact]
    public void TruecolorAlpha_DropsAlpha()
    {
        // 1×1 truecolor+alpha.
        byte[] rows = [12, 34, 56, 128];
        var png = BuildPng(
            1,
            1,
            colorType: 6,
            channels: 4,
            scanlines: rows
        );

        var rgb = PngDecoder.TryDecodeToRgb(png, out var w, out _);
        rgb.ShouldNotBeNull();
        PixelAt(rgb, w, 0, 0).ShouldBe((12, 34, 56));
    }

    [Fact]
    public void Indexed_ResolvesPaletteColors()
    {
        // 2×1 indexed, palette[0]=red palette[1]=blue.
        byte[] rows = [0, 1];
        byte[] palette = [255, 0, 0, 0, 0, 255];
        var png = BuildPng(
            2,
            1,
            colorType: 3,
            channels: 1,
            scanlines: rows,
            palette: palette
        );

        var rgb = PngDecoder.TryDecodeToRgb(png, out var w, out _);
        rgb.ShouldNotBeNull();
        PixelAt(rgb, w, 0, 0).ShouldBe((255, 0, 0));
        PixelAt(rgb, w, 1, 0).ShouldBe((0, 0, 255));
    }

    [Fact]
    public void Indexed_WithoutPalette_ReturnsNull()
    {
        byte[] rows = [0, 1];
        var png = BuildPng(
            2,
            1,
            colorType: 3,
            channels: 1,
            scanlines: rows
        );
        PngDecoder.TryDecodeToRgb(png, out _, out _).ShouldBeNull();
    }

    [Fact]
    public void Indexed_OutOfRangeIndex_ReturnsNull()
    {
        // Index 5 with a 1-entry palette → out of range.
        byte[] rows = [5];
        byte[] palette = [10, 20, 30];
        var png = BuildPng(
            1,
            1,
            colorType: 3,
            channels: 1,
            scanlines: rows,
            palette: palette
        );
        PngDecoder.TryDecodeToRgb(png, out _, out _).ShouldBeNull();
    }

    [Fact]
    public void SubFilter_IsReversed()
    {
        // 3×1 truecolor with Sub filter: stored deltas reconstruct to absolute pixels.
        // Pixel0 = (10,20,30); pixel1 = pixel0 + (5,5,5); pixel2 = pixel1 + (1,2,3).
        byte[] raw = [10, 20, 30, 5, 5, 5, 1, 2, 3];
        var png = BuildPngFiltered(
            3,
            1,
            colorType: 2,
            channels: 3,
            filterType: 1,
            filteredRows: raw
        );

        var rgb = PngDecoder.TryDecodeToRgb(png, out var w, out _);
        rgb.ShouldNotBeNull();
        PixelAt(rgb, w, 0, 0).ShouldBe((10, 20, 30));
        PixelAt(rgb, w, 1, 0).ShouldBe((15, 25, 35));
        PixelAt(rgb, w, 2, 0).ShouldBe((16, 27, 38));
    }

    [Fact]
    public void UpFilter_IsReversed()
    {
        // 1×2 grayscale, Up filter on row 1: row0=100, row1 stored delta 5 → 105.
        var row0 = "\0d"u8.ToArray();   // filter None, value 100
        byte[] row1 = [2, 5];     // filter Up, delta 5
        var png = BuildPngRawScanLines(
            1,
            2,
            colorType: 0,
            scanLines: (byte[][])[row0, row1]
        );

        var rgb = PngDecoder.TryDecodeToRgb(png, out var w, out _);
        rgb.ShouldNotBeNull();
        PixelAt(rgb, w, 0, 0).ShouldBe((100, 100, 100));
        PixelAt(rgb, w, 0, 1).ShouldBe((105, 105, 105));
    }

    [Fact]
    public void AverageFilter_IsReversed()
    {
        // 2×1 grayscale, Average filter. value[0] = 50 (a=0,b=0 → +0).
        // value[1] = 10 + (a=50 + b=0)/2 = 10 + 25 = 35.
        byte[] row = [3, 50, 10];
        var png = BuildPngRawScanLines(
            2,
            1,
            colorType: 0,
            scanLines: (byte[][])[row]
        );

        var rgb = PngDecoder.TryDecodeToRgb(png, out var w, out _);
        rgb.ShouldNotBeNull();
        PixelAt(rgb, w, 0, 0).ShouldBe((50, 50, 50));
        PixelAt(rgb, w, 1, 0).ShouldBe((35, 35, 35));
    }

    [Fact]
    public void PaethFilter_IsReversed()
    {
        // 2×1 grayscale, Paeth filter. Pixel0: a=b=c=0 → predictor 0, value 60.
        // Pixel1: a=60,b=0,c=0 → Paeth predicts 60; stored 5 → 65.
        byte[] row = [4, 60, 5];
        var png = BuildPngRawScanLines(
            2,
            1,
            colorType: 0,
            scanLines: (byte[][])[row]
        );

        var rgb = PngDecoder.TryDecodeToRgb(png, out var w, out _);
        rgb.ShouldNotBeNull();
        PixelAt(rgb, w, 0, 0).ShouldBe((60, 60, 60));
        PixelAt(rgb, w, 1, 0).ShouldBe((65, 65, 65));
    }

    [Fact]
    public void SixteenBitDepth_ReturnsNull()
    {
        var png = BuildPng(
            1,
            1,
            colorType: 0,
            channels: 1,
            scanlines: "\0\0"u8.ToArray(),
            bitDepth: 16
        );
        PngDecoder.TryDecodeToRgb(png, out _, out _).ShouldBeNull();
    }

    [Fact]
    public void Interlaced_ReturnsNull()
    {
        var png = BuildPng(
            1,
            1,
            colorType: 0,
            channels: 1,
            scanlines: [0],
            interlace: 1
        );
        PngDecoder.TryDecodeToRgb(png, out _, out _).ShouldBeNull();
    }

    [Fact]
    public void UnknownColorType_ReturnsNull()
    {
        // Color type 5 is not defined → channels==0 → null.
        var png = BuildPng(
            1,
            1,
            colorType: 5,
            channels: 1,
            scanlines: [0]
        );
        PngDecoder.TryDecodeToRgb(png, out _, out _).ShouldBeNull();
    }

    [Fact]
    public void CorruptIdat_ReturnsNull()
    {
        // Valid header but the IDAT payload is not a valid zlib stream.
        using var ms = new MemoryStream();
        ms.Write(PngConstants.Signature);
        WriteIhdr(
            ms,
            1,
            1,
            bitDepth: 8,
            colorType: 0,
            interlace: 0
        );
        WriteChunk(ms, PngConstants.IDAT, [0x00, 0x01, 0x02, 0x03]);
        WriteChunk(ms, PngConstants.IEND, []);

        PngDecoder.TryDecodeToRgb(ms.ToArray(), out _, out _).ShouldBeNull();
    }

    [Fact]
    public void TruncatedIdat_TooFewScanlineBytes_ReturnsNull()
    {
        // Declares 4×4 truecolor but supplies only one short scanline → raw too small.
        byte[] tooShort = [0, 1, 2, 3];
        var png = BuildPngRawConcat(
            4,
            4,
            colorType: 2,
            tooShort
        );
        PngDecoder.TryDecodeToRgb(png, out _, out _).ShouldBeNull();
    }

    [Fact]
    public void TrnsChunk_IsIgnored_AndImageDecodes()
    {
        // 1×1 grayscale with a tRNS chunk present; decoder reads it but ignores it.
        var idat = Deflate(BuildScanLines(1, 1, 1, [77]));
        using var ms = new MemoryStream();
        ms.Write(PngConstants.Signature);
        WriteIhdr(
            ms,
            1,
            1,
            bitDepth: 8,
            colorType: 0,
            interlace: 0
        );
        WriteChunk(ms, PngConstants.TRNS, [0x00, 0x10]);
        WriteChunk(ms, PngConstants.IDAT, idat);
        WriteChunk(ms, PngConstants.IEND, []);

        var rgb = PngDecoder.TryDecodeToRgb(ms.ToArray(), out var w, out _);
        rgb.ShouldNotBeNull();
        PixelAt(rgb, w, 0, 0).ShouldBe((77, 77, 77));
    }

    private static (int, int, int) PixelAt(
        IReadOnlyList<byte> rgb,
        int width,
        int x,
        int y
    )
    {
        var i = ((y * width) + x) * 3;
        return (rgb[i], rgb[i + 1], rgb[i + 2]);
    }

    // ── PNG construction helpers ───────────────────────────────────────────────

    // Builds a PNG where every scanline uses filter None; `scanlines` holds the raw
    // unfiltered channel bytes for all rows concatenated (length = width*height*channels).
    private static byte[] BuildPng(
        int width,
        int height,
        int colorType,
        int channels,
        byte[] scanlines,
        byte[]? palette = null,
        int bitDepth = 8,
        int interlace = 0
    )
    {
        var idat = Deflate(BuildScanLines(width, height, channels, scanlines));
        return Assemble(
            width,
            height,
            bitDepth,
            colorType,
            interlace,
            idat,
            palette
        );
    }

    // Builds a PNG where every row uses the same single filter type and `filteredRows`
    // holds the post-filter channel bytes for all rows concatenated.
    private static byte[] BuildPngFiltered(
        int width,
        int height,
        int colorType,
        int channels,
        byte filterType,
        byte[] filteredRows
    )
    {
        var stride = width * channels;
        var raw = new byte[height * (stride + 1)];
        for (var y = 0; y < height; y++)
        {
            raw[y * (stride + 1)] = filterType;
            Array.Copy(
                filteredRows,
                y * stride,
                raw,
                (y * (stride + 1)) + 1,
                stride
            );
        }

        return Assemble(
            width,
            height,
            8,
            colorType,
            0,
            Deflate(raw),
            palette: null
        );
    }

    // Builds a PNG from explicit per-row scan lines, each already prefixed with its filter byte.
    private static byte[] BuildPngRawScanLines(
        int width,
        int height,
        int colorType,
        IEnumerable<byte[]> scanLines
    )
    {
        using var raw = new MemoryStream();
        foreach (var row in scanLines)
            raw.Write(row);
        return Assemble(
            width,
            height,
            8,
            colorType,
            0,
            Deflate(raw.ToArray()),
            palette: null
        );
    }

    // Builds a PNG from an arbitrary (possibly too-short) raw byte block, for error cases.
    private static byte[] BuildPngRawConcat(
        int width,
        int height,
        int colorType,
        byte[] raw
    ) =>
        Assemble(
            width,
            height,
            8,
            colorType,
            0,
            Deflate(raw),
            palette: null
        );

    private static byte[] BuildScanLines(
        int width,
        int height,
        int channels,
        byte[] data
    )
    {
        var stride = width * channels;
        var raw = new byte[height * (stride + 1)];
        for (var y = 0; y < height; y++)
        {
            raw[y * (stride + 1)] = 0; // filter None
            Array.Copy(
                data,
                y * stride,
                raw,
                (y * (stride + 1)) + 1,
                stride
            );
        }

        return raw;
    }

    private static byte[] Assemble(
        int width,
        int height,
        int bitDepth,
        int colorType,
        int interlace,
        byte[] idat,
        byte[]? palette
    )
    {
        using var ms = new MemoryStream();
        ms.Write(PngConstants.Signature);
        WriteIhdr(
            ms,
            width,
            height,
            bitDepth,
            colorType,
            interlace
        );
        if (palette is not null)
            WriteChunk(ms, PngConstants.PLTE, palette);
        WriteChunk(ms, PngConstants.IDAT, idat);
        WriteChunk(ms, PngConstants.IEND, []);
        return ms.ToArray();
    }

    private static void WriteIhdr(
        Stream s,
        int width,
        int height,
        int bitDepth,
        int colorType,
        int interlace
    )
    {
        var data = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(data, width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4), height);
        data[8] = (byte)bitDepth;
        data[9] = (byte)colorType;
        data[10] = 0;
        data[11] = 0;
        data[12] = (byte)interlace;
        WriteChunk(s, PngConstants.IHDR, data);
    }

    // Writes a chunk with a zero CRC; PngDecoder does not validate CRCs.
    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len);
        s.Write(Encoding.ASCII.GetBytes(type));
        if (data.Length > 0) s.Write(data);
        s.Write(stackalloc byte[4]); // CRC placeholder (ignored by decoder)
    }

    private static byte[] Deflate(byte[] raw)
    {
        using var ms = new MemoryStream();
        using (var zlib = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(raw);
        return ms.ToArray();
    }
}
