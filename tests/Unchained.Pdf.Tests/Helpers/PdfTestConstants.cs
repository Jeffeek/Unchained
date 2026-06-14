using System.IO.Compression;

namespace Unchained.Pdf.Tests.Helpers;

/// <summary>Constants shared across all test classes.</summary>
internal static class PdfTestConstants
{
    /// <summary>The 8-byte PNG file signature (magic bytes).</summary>
    internal static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>Reads a big-endian <see cref="uint" /> from <paramref name="data" /> at <paramref name="offset" />.</summary>
    internal static uint ReadUInt32BigEndian(IReadOnlyList<byte> data, int offset) =>
        ((uint)data[offset] << 24) |
        ((uint)data[offset + 1] << 16) |
        ((uint)data[offset + 2] << 8) |
        data[offset + 3];

    /// <summary>Extracts the pixel width from a PNG byte array (IHDR bytes 16–19).</summary>
    internal static int PngWidth(byte[] png) =>
        (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];

    /// <summary>Extracts the pixel height from a PNG byte array (IHDR bytes 20–23).</summary>
    internal static int PngHeight(byte[] png) =>
        (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];

    /// <summary>
    ///     Decodes a PNG produced by PdfPngEncoder (RGBA color type 6, filter=None, single
    ///     ZLib IDAT chunk starting at offset 33) into raw scanline bytes, also returning the
    ///     pixel dimensions and the per-row stride (1 filter byte + width × 4 RGBA bytes).
    /// </summary>
    internal static byte[] DecodePdfEncoderPng(
        byte[] png,
        out int width,
        out int height,
        out int stride
    )
    {
        width = (int)ReadUInt32BigEndian(png, 16);
        height = (int)ReadUInt32BigEndian(png, 20);
        var idatLength = (int)ReadUInt32BigEndian(png, 33);
        var idat = png.AsSpan(33 + 8, idatLength).ToArray();

        using var compressed = new MemoryStream(idat);
        using var decompressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress))
            zlib.CopyTo(decompressed);

        stride = 1 + (width * 4);
        return decompressed.ToArray();
    }

    /// <summary>
    ///     Decodes a PdfPngEncoder PNG and counts pixels that are not pure white
    ///     (any of R, G, B below 255).
    /// </summary>
    internal static int CountNonWhitePixels(byte[] png)
    {
        var raw = DecodePdfEncoderPng(png, out var width, out var height, out var stride);
        var nonWhite = 0;
        for (var y = 0; y < height; y++)
        {
            var rowStart = (y * stride) + 1; // +1 to skip the filter byte
            for (var x = 0; x < width; x++)
            {
                var p = rowStart + (x * 4);
                if (raw[p] < 255 || raw[p + 1] < 255 || raw[p + 2] < 255)
                    nonWhite++;
            }
        }

        return nonWhite;
    }

    /// <summary>
    ///     Decodes a PdfPngEncoder PNG into a grayscale <c>[height, width]</c> array,
    ///     averaging each pixel's R, G and B channels.
    /// </summary>
    internal static int[,] DecodeGrayscale(byte[] png)
    {
        var raw = DecodePdfEncoderPng(png, out var width, out var height, out var stride);
        var gray = new int[height, width];
        for (var y = 0; y < height; y++)
        {
            var rowStart = (y * stride) + 1;
            for (var x = 0; x < width; x++)
            {
                var p = rowStart + (x * 4);
                gray[y, x] = (raw[p] + raw[p + 1] + raw[p + 2]) / 3;
            }
        }

        return gray;
    }
}
