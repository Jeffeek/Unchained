using System.Buffers.Binary;
using System.IO.Compression;
using Unchained.Drawing.Constants;

namespace Unchained.Drawing.Decoders;

/// <summary>
/// Minimal PNG decoder using only BCL APIs (<see cref="ZLibStream"/> for inflate).
/// Decodes 8-bit-per-channel grayscale, grayscale+alpha, truecolor, truecolor+alpha,
/// and indexed-colour PNGs to packed 24-bit RGB. Returns <see langword="null"/> for
/// formats it cannot handle (16-bit, interlaced) so the caller can skip the image.
/// </summary>
internal static class PngDecoder
{
    internal static byte[]? TryDecodeToRgb(ReadOnlySpan<byte> png, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (png.Length < 8 || !png[..8].SequenceEqual(PngConstants.Signature))
            return null;

        int w = 0, h = 0, bitDepth = 0, colorType = 0, interlace = 0;
        byte[]? palette = null;
        using var idat = new MemoryStream();

        var pos = 8;
        while (pos + 8 <= png.Length)
        {
            var len = BinaryPrimitives.ReadInt32BigEndian(png.Slice(pos, 4));
            var type = System.Text.Encoding.ASCII.GetString(png.Slice(pos + 4, 4));
            var dataStart = pos + 8;
            if (len < 0 || dataStart + len + 4 > png.Length)
                break;

            switch (type)
            {
                case PngConstants.IHDR:
                {
                    w = BinaryPrimitives.ReadInt32BigEndian(png.Slice(dataStart, 4));
                    h = BinaryPrimitives.ReadInt32BigEndian(png.Slice(dataStart + 4, 4));
                    bitDepth = png[dataStart + 8];
                    colorType = png[dataStart + 9];
                    interlace = png[dataStart + 12];
                    break;
                }
                case PngConstants.PLTE:
                {
                    palette = png.Slice(dataStart, len).ToArray();
                    break;
                }
                case PngConstants.TRNS:
                {
                    png.Slice(dataStart, len).ToArray();
                    break;
                }
                case PngConstants.IDAT:
                {
                    idat.Write(png.Slice(dataStart, len));
                    break;
                }
                case PngConstants.IEND:
                {
                    pos = png.Length; // stop
                    break;
                }
            }

            if (pos >= png.Length)
                break;

            pos = dataStart + len + 4; // skip data + CRC
        }

        // Unsupported: out-of-range dims, 16-bit channels, interlaced.
        if (w <= 0 || h <= 0 || bitDepth != 8 || interlace != 0)
            return null;

        var channels = colorType switch
        {
            0 => 1, // grayscale
            2 => 3, // truecolor
            3 => 1, // indexed
            4 => 2, // grayscale + alpha
            6 => 4, // truecolor + alpha
            _ => 0
        };
        if (channels == 0 || (colorType == 3 && palette is null))
            return null;

        byte[] raw;
        try
        {
            idat.Position = 0;
            using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            zlib.CopyTo(outMs);
            raw = outMs.ToArray();
        }
        catch
        {
            return null;
        }

        var stride = w * channels;
        if (raw.Length < (stride + 1) * h)
            return null;

        var unfiltered = Unfilter(raw, w, h, channels, stride);
        // ReSharper disable once BadListLineBreaks
        var rgb = ToRgb(unfiltered, w, h, channels, colorType, palette);
        if (rgb is null)
            return null;

        width = w;
        height = h;

        return rgb;
    }

    // Reverses PNG scanline filters (None/Sub/Up/Average/Paeth) into raw channel bytes.
    private static byte[] Unfilter(
        byte[] raw,
        int width,
        int height,
        int channels,
        int stride
    )
    {
        var output = new byte[width * height * channels];
        var prevRow = new byte[stride];
        var curRow = new byte[stride];
        var inPos = 0;

        for (var y = 0; y < height; y++)
        {
            var filter = raw[inPos++];
            Array.Copy(raw, inPos, curRow, 0, stride);
            inPos += stride;

            for (var x = 0; x < stride; x++)
            {
                var a = x >= channels ? curRow[x - channels] : (byte)0;  // left
                var b = prevRow[x];                                      // up
                var c = x >= channels ? prevRow[x - channels] : (byte)0; // up-left
                var value = curRow[x];

                curRow[x] = filter switch
                {
                    0 => value,
                    1 => (byte)(value + a),
                    2 => (byte)(value + b),
                    3 => (byte)(value + (a + b) / 2),
                    4 => (byte)(value + Paeth(a, b, c)),
                    _ => value
                };
            }

            Array.Copy(curRow, 0, output, y * stride, stride);
            (prevRow, curRow) = (curRow, prevRow);
        }

        return output;
    }

    private static byte[]? ToRgb(
        IReadOnlyList<byte> data,
        int width,
        int height,
        int channels,
        int colorType,
        byte[]? palette
    )
    {
        var rgb = new byte[width * height * 3];
        var pixelCount = width * height;

        for (var i = 0; i < pixelCount; i++)
        {
            byte r, g, b;
            var src = i * channels;
            switch (colorType)
            {
                case 0: // grayscale
                case 4: // grayscale + alpha (channel 0 = gray)
                {
                    r = g = b = data[src];
                    break;
                }
                case 2: // truecolor
                case 6: // truecolor + alpha
                {
                    r = data[src];
                    g = data[src + 1];
                    b = data[src + 2];
                    break;
                }
                case 3: // indexed
                {
                    var idx = data[src] * 3;
                    if (palette is null || idx + 2 >= palette.Length)
                        return null;

                    r = palette[idx];
                    g = palette[idx + 1];
                    b = palette[idx + 2];

                    break;
                }
                default:
                    return null;
            }

            var dest = i * 3;
            rgb[dest] = r;
            rgb[dest + 1] = g;
            rgb[dest + 2] = b;
        }

        return rgb;
    }

    private static int Paeth(byte a, byte b, byte c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc)
            return a;

        return pb <= pc ? b : c;
    }
}
