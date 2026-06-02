using System.Buffers;
using System.Runtime.InteropServices;
using JpegLibrary;

namespace Unchained.Pdf.Parsing.Filters;

/// <summary>
/// Decodes JPEG-compressed data (DCTDecode filter, ISO 32000-1 §7.4.8)
/// using JpegLibrary — a pure-managed, zero-native-dependency C# JPEG decoder.
/// </summary>
internal static class JpegDecoder
{
    /// <summary>
    /// Decompresses JPEG bytes and returns a flat RGB byte array
    /// (<c>width × height × 3</c> bytes, row-major, no padding).
    /// Grayscale JPEGs are expanded to 3-channel RGB (R=G=B=Y).
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// JPEG uses an unsupported color space (e.g. CMYK / 4-component).
    /// </exception>
    public static ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> data)
    {
        var decoder = new global::JpegLibrary.JpegDecoder();
        decoder.SetInput(new ReadOnlySequence<byte>(data));
        decoder.Identify();

        var nc = decoder.NumberOfComponents;
        if (nc != 1 && nc != 3)
            throw new NotSupportedException(
                $"DCTDecode: unsupported JPEG color space ({nc} components).");

        var width = decoder.Width;
        var height = decoder.Height;

        // JpegLibrary upsamples chroma internally, so all components arrive at full resolution.
        // We collect them as three separate byte planes then convert to interleaved RGB.
        var planes = new byte[nc][];
        for (var i = 0; i < nc; i++)
            planes[i] = new byte[width * height];

        decoder.SetOutputWriter(new PlanarWriter(planes, width, height, nc));
        decoder.Decode();

        var rgb = new byte[width * height * 3];

        if (nc == 1)
        {
            var y = planes[0];
            for (var i = 0; i < width * height; i++)
            {
                rgb[i * 3] = y[i];
                rgb[i * 3 + 1] = y[i];
                rgb[i * 3 + 2] = y[i];
            }
        }
        else
        {
            var yPlane = planes[0];
            var cbPlane = planes[1];
            var crPlane = planes[2];

            for (var i = 0; i < width * height; i++)
            {
                var yy = yPlane[i];
                var cb = cbPlane[i] - 128;
                var cr = crPlane[i] - 128;

                rgb[i * 3] = Clamp(yy + (int)(1.402f * cr));
                rgb[i * 3 + 1] = Clamp(yy - (int)(0.344136f * cb) - (int)(0.714136f * cr));
                rgb[i * 3 + 2] = Clamp(yy + (int)(1.772f * cb));
            }
        }

        return rgb;
    }

    private static byte Clamp(int v) => v < 0 ? (byte)0 : v > 255 ? (byte)255 : (byte)v;

    private sealed class PlanarWriter(byte[][] planes, int width, int height, int componentCount)
        : JpegBlockOutputWriter
    {
        public override void WriteBlock(ref short blockRef, int componentIndex, int x, int y)
        {
            if (componentIndex >= componentCount) return;

            var block = MemoryMarshal.CreateSpan(ref blockRef, 64);
            var plane = planes[componentIndex];
            var writeW = Math.Min(width - x, 8);
            var writeH = Math.Min(height - y, 8);

            for (var row = 0; row < writeH; row++)
            {
                var destBase = (y + row) * width + x;
                var srcBase = row * 8;
                for (var col = 0; col < writeW; col++)
                    plane[destBase + col] = (byte)Math.Clamp(block[srcBase + col], (short)0, (short)255);
            }
        }
    }
}
