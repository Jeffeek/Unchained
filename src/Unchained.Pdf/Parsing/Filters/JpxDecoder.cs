using CoreJ2K;
using CoreJ2K.Configuration;

namespace Unchained.Pdf.Parsing.Filters;

/// <summary>
/// Decodes JPEG 2000-compressed data (JPXDecode filter, ISO 32000-1 §7.4.9)
/// using CoreJ2K — a pure-managed, BSD-3-Clause C# JPEG 2000 decoder.
/// <para>
/// Returns a flat <c>width × height × 3</c> RGB byte array for 1- and 3-component images.
/// Grayscale (1-component) images are expanded to R=G=B=Y.
/// </para>
/// </summary>
internal static class JpxDecoder
{
    private static readonly J2KDecoderConfiguration DefaultConfig = new();

    /// <summary>
    /// Decodes JPEG 2000 bytes and returns a flat RGB byte array
    /// (<c>width × height × 3</c> bytes, row-major).
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Image has an unsupported number of components (not 1 or 3).
    /// </exception>
    /// <exception cref="InvalidOperationException">JPEG 2000 data is malformed.</exception>
    internal static ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> data)
    {
        try
        {
            using var image = J2kImage.FromBytes(data.ToArray(), DefaultConfig);

            var width = image.Width;
            var height = image.Height;
            var nc = image.NumberOfComponents;

            if (nc != 1 && nc != 3)
                throw new NotSupportedException($"JPXDecode: unsupported {nc}-component image (expected 1 or 3).");

            var rgb = new byte[width * height * 3];

            if (nc == 1)
            {
                var gray = image.GetComponentBytes(0);
                for (var i = 0; i < width * height; i++)
                    rgb[i * 3] = rgb[(i * 3) + 1] = rgb[(i * 3) + 2] = gray[i];
            }
            else
            {
                var r = image.GetComponentBytes(0);
                var g = image.GetComponentBytes(1);
                var b = image.GetComponentBytes(2);
                for (var i = 0; i < width * height; i++)
                {
                    rgb[i * 3] = r[i];
                    rgb[(i * 3) + 1] = g[i];
                    rgb[(i * 3) + 2] = b[i];
                }
            }

            return rgb;
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"JPXDecode failed: {ex.Message}", ex);
        }
    }
}
