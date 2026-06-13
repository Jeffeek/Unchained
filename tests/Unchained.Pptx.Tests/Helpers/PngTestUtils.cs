using System.IO.Compression;
using System.Text;
using Unchained.Drawing.Constants;

namespace Unchained.Pptx.Tests.Helpers;

/// <summary>
///     PNG decoding helpers shared by the slide-rendering tests. Operates on filter-None
///     RGBA PNGs produced by <c>PngEncoder</c>: extracts the concatenated IDAT data,
///     inflates it, and walks the scanlines applying a per-pixel predicate.
///     Note: this scans the chunk stream for IDAT (PngEncoder may emit multiple chunks),
///     unlike the Pdf test side (<c>PdfTestConstants.DecodePdfEncoderPng</c>) which assumes
///     a single IDAT at a fixed offset — the two decode different encoders' output.
/// </summary>
internal static class PngTestUtils
{
    /// <summary>Extracts and concatenates the IDAT chunk payloads from a PNG byte array.</summary>
    internal static byte[] ExtractIdat(byte[] png)
    {
        using var output = new MemoryStream();
        var pos = 8; // skip signature
        while (pos + 8 <= png.Length)
        {
            var len = (png[pos] << 24) | (png[pos + 1] << 16) | (png[pos + 2] << 8) | png[pos + 3];
            var type = Encoding.ASCII.GetString(png, pos + 4, 4);
            var dataStart = pos + 8;
            if (type == PngConstants.IDAT)
                output.Write(png, dataStart, len);
            pos = dataStart + len + 4; // data + CRC
            if (type == PngConstants.IEND) break;
        }

        return output.ToArray();
    }

    /// <summary>
    ///     Inflates a filter-None RGBA PNG and counts the pixels for which
    ///     <paramref name="predicate" /> (given R, G, B) returns <see langword="true" />.
    /// </summary>
    internal static int CountPixels(byte[] png, int width, int height, Func<byte, byte, byte, bool> predicate)
    {
        var idat = ExtractIdat(png);
        using var input = new MemoryStream(idat);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        var bytes = raw.ToArray();

        var stride = 1 + (width * 4);
        var count = 0;
        for (var y = 0; y < height; y++)
        {
            var rowStart = (y * stride) + 1; // skip filter byte
            for (var x = 0; x < width; x++)
            {
                var p = rowStart + (x * 4);
                if (predicate(bytes[p], bytes[p + 1], bytes[p + 2]))
                    count++;
            }
        }

        return count;
    }
}
