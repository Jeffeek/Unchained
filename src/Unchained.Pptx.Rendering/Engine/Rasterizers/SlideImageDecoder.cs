using Unchained.Drawing;
using Unchained.Drawing.Decoders;
using Unchained.Ooxml.Media;

namespace Unchained.Pptx.Rendering.Engine.Rasterizers;

/// <summary>
///     Decodes embedded slide images (PNG / JPEG / GIF / SVG) to packed RGB and blits scaled RGB
///     into a raster buffer with nearest-neighbour sampling. Pure and state-free; extracted
///     from <see cref="SlideRasterizer" />.
/// </summary>
internal static class SlideImageDecoder
{
    // Nearest-neighbour blit of a packed RGB source into the destination rect.
    internal static void BlitScaledRgb(
        RasterBuffer buffer,
        IReadOnlyList<byte> rawPixels,
        int imgWidth,
        int imgHeight,
        int x,
        int y,
        int width,
        int height
    )
    {
        for (var py = 0; py < height; py++)
        {
            var srcY = py * imgHeight / height;
            for (var px = 0; px < width; px++)
            {
                var srcX = px * imgWidth / width;
                var srcOffset = ((srcY * imgWidth) + srcX) * 3;
                buffer.BlitImagePixel(
                    x + px,
                    y + py,
                    rawPixels[srcOffset],
                    rawPixels[srcOffset + 1],
                    rawPixels[srcOffset + 2]
                );
            }
        }
    }

    internal static byte[]? TryDecodeImageToRgb(EmbeddedImage image, out int width, out int height)
    {
        width = 0;
        height = 0;

        var bytes = image.Data.Span;
        if (bytes.IsEmpty)
            return null;

        if (IsPng(bytes))
            return PngDecoder.TryDecodeToRgb(bytes, out width, out height);
        if (IsJpeg(bytes))
            return JpegDecoder.TryDecodeToRgb(bytes, out width, out height);
        if (GifDecoder.IsGif(bytes))
            return GifDecoder.TryDecodeToRgb(bytes, out width, out height);

        if (!IsSvg(bytes)) return null;

        // Render SVG at a reasonable fixed resolution; caller scales to dest rect.
        const int svgRenderSize = 256;
        var pixels = SvgDecoder.TryDecodeToRgb(
            bytes,
            svgRenderSize,
            svgRenderSize,
            out width,
            out height
        );

        return pixels;
    }

    private static bool IsPng(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8 &&
        bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
        bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;

    private static bool IsJpeg(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    private static bool IsSvg(ReadOnlySpan<byte> bytes)
    {
        // SVG starts with XML declaration or <svg tag (possibly with BOM).
        if (bytes.Length < 4) return false;
        // Skip UTF-8 BOM if present.
        var start = (bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) ? 3 : 0;
        // Look for <svg or <?xml within the first 512 bytes.
        var searchLen = Math.Min(bytes.Length - start, 512);
        var trimmed = bytes.Slice(start, searchLen);
        while (trimmed.Length > 0 && trimmed[0] <= 32) trimmed = trimmed[1..];
        switch (trimmed.Length)
        {
            case >= 4 when trimmed[0] == '<' && trimmed[1] == 's' && trimmed[2] == 'v' && trimmed[3] == 'g':
                return true;
            case >= 5 when trimmed[0] == '<' && trimmed[1] == '?' && trimmed[2] == 'x' && trimmed[3] == 'm' && trimmed[4] == 'l':
            {
                // Skip <?xml...?> to reach the root element.
                var close = trimmed.IndexOf("?>"u8);
                if (close >= 0)
                {
                    var afterDecl = trimmed[(close + 2)..];
                    while (afterDecl.Length > 0 && afterDecl[0] <= 32) afterDecl = afterDecl[1..];
                    if (afterDecl.Length >= 4 && afterDecl[0] == '<' && afterDecl[1] == 's' && afterDecl[2] == 'v' && afterDecl[3] == 'g') return true;
                }

                break;
            }
        }

        return false;
    }
}
