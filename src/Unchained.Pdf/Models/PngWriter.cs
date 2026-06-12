using System.IO.Compression;
using Unchained.Drawing.Constants;
using Unchained.Drawing.Extensions;

namespace Unchained.Pdf.Models;

/// <summary>
/// Minimal, dependency-free PNG encoder for 8-bit RGB / RGBA raster data. Lives in the core
/// package (which has no reference to Unchained.Drawing) so extracted images can be exported
/// without pulling in the rendering stack. Uses BCL <see cref="ZLibStream"/> for IDAT.
/// </summary>
internal static class PngWriter
{
    // Encodes width×height pixels. rgb is W*H*3 (R,G,B). When alpha (W*H) is supplied the
    // output is RGBA (colour type 6); otherwise RGB (colour type 2).
    internal static byte[] Encode(
        int width,
        int height,
        byte[] rgb,
        byte[]? alpha
    )
    {
        var hasAlpha = alpha is not null;
        var channels = hasAlpha ? 4 : 3;
        var colorType = (byte)(hasAlpha ? 6 : 2);

        // Build raw scan lines: each row prefixed with filter byte 0 (None).
        var stride = width * channels;
        var raw = new byte[height * (1 + stride)];
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * (1 + stride);
            raw[rowStart] = 0; // filter: None
            var dst = rowStart + 1;
            for (var x = 0; x < width; x++)
            {
                var si = (y * width + x) * 3;
                raw[dst++] = rgb[si];
                raw[dst++] = rgb[si + 1];
                raw[dst++] = rgb[si + 2];
                if (hasAlpha) raw[dst++] = alpha![y * width + x];
            }
        }

        using var comp = new MemoryStream();
        using (var z = new ZLibStream(comp, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(raw, 0, raw.Length);

        var idat = comp.ToArray();

        using var ms = new MemoryStream();
        ms.Write(PngConstants.Signature, 0, PngConstants.Signature.Length);
        WriteChunk(ms, PngConstants.IHDR.ToUtf8Span(), Ihdr(width, height, colorType));
        WriteChunk(ms, PngConstants.IDAT.ToUtf8Span(), idat);
        WriteChunk(ms, PngConstants.IEND.ToUtf8Span(), []);

        return ms.ToArray();
    }

    private static byte[] Ihdr(int w, int h, byte colorType)
    {
        var b = new byte[13];
        WriteU32(b, 0, (uint)w);
        WriteU32(b, 4, (uint)h);
        b[8] = 8;          // bit depth
        b[9] = colorType;  // 2 = RGB, 6 = RGBA
        // b[10..12] = 0: deflate, adaptive filtering, no interlace

        return b;
    }

    private static void WriteChunk(Stream s, ReadOnlySpan<byte> type, byte[] data)
    {
        var len = new byte[4];
        WriteU32(len, 0, (uint)data.Length);
        s.Write(len, 0, 4);
        s.Write(type);
        s.Write(data, 0, data.Length);
        var crc = new byte[4];
        WriteU32(crc, 0, Crc32(type, data));
        s.Write(crc, 0, 4);
    }

    private static void WriteU32(IList<byte> b, int o, uint v)
    {
        b[o] = (byte)(v >> 24);
        b[o + 1] = (byte)(v >> 16);
        b[o + 2] = (byte)(v >> 8);
        b[o + 3] = (byte)v;
    }

    private static uint Crc32(ReadOnlySpan<byte> type, IEnumerable<byte> data)
    {
        var c = PngConstants.Crc32Init;
        foreach (var x in type)
            c = (c >> 8) ^ PngConstants.CtcTable[(c ^ x) & 0xFF];

        c = data.Aggregate(c, static (current, x) => (current >> 8) ^ PngConstants.CtcTable[(current ^ x) & 0xFF]);

        return c ^ PngConstants.Crc32Init;
    }
}
