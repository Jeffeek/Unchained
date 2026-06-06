using System.Diagnostics.CodeAnalysis;

namespace Unchained.Drawing;

/// <summary>
/// ARGB (4 bytes per pixel, row-major) pixel buffer used as the render target.
/// The byte layout per pixel is: [R, G, B, A] at indices [4*i, 4*i+1, 4*i+2, 4*i+3].
/// </summary>
internal sealed class RasterBuffer(int width, int height)
{
    private readonly byte[] _data = new byte[width * height * 4];

    internal int Width { get; } = width;
    internal int Height { get; } = height;

    internal void Clear(byte r = 255, byte g = 255, byte b = 255)
    {
        for (var i = 0; i < _data.Length; i += 4)
        {
            _data[i] = r;
            _data[i + 1] = g;
            _data[i + 2] = b;
            _data[i + 3] = 255;
        }
    }

    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    internal void SetPixel(int x, int y, byte r, byte g, byte b, byte a)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;

        var i = ((y * Width) + x) * 4;
        if (a == 255)
        {
            _data[i] = r;
            _data[i + 1] = g;
            _data[i + 2] = b;
        }
        else
        {
            // Alpha-blend over existing pixel
            var inv = 255 - a;
            _data[i] = (byte)(((_data[i] * inv) + (r * a)) / 255);
            _data[i + 1] = (byte)(((_data[i + 1] * inv) + (g * a)) / 255);
            _data[i + 2] = (byte)(((_data[i + 2] * inv) + (b * a)) / 255);
        }

        _data[i + 3] = 255;
    }

    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    internal void FillRect(int x, int y, int w, int h, byte r, byte g, byte b, byte a = 255)
    {
        var x1 = Math.Max(0, x);
        var y1 = Math.Max(0, y);
        var x2 = Math.Min(Width, x + w);
        var y2 = Math.Min(Height, y + h);
        for (var py = y1; py < y2; py++)
        for (var px = x1; px < x2; px++)
            SetPixel(px, py, r, g, b, a);
    }

    // Bresenham line with configurable thickness.
    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    internal void DrawLine(
        int x0, int y0, int x1, int y1,
        byte r, byte g, byte b,
        int thicknessPx = 1)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;
        var half = thicknessPx / 2;

        while (true)
        {
            for (var ty = -half; ty <= half; ty++)
            for (var tx = -half; tx <= half; tx++)
                SetPixel(x0 + tx, y0 + ty, r, g, b, 255);

            if (x0 == x1 && y0 == y1)
                break;

            var e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 >= dx)
                continue;

            err += dx;
            y0 += sy;
        }
    }

    /// <summary>
    /// Blits a grayscale glyph bitmap (one byte per pixel = alpha) onto the buffer.
    /// Called by <c>RasterBufferGlyphExtensions.BlitGlyphBitmap</c> in Drawing.Text.
    /// </summary>
    /// <param name="destX">Left edge of the glyph in buffer coordinates.</param>
    /// <param name="destY">Top edge of the glyph in buffer coordinates.</param>
    /// <param name="glyphWidth">Width of the glyph bitmap in pixels.</param>
    /// <param name="glyphHeight">Height of the glyph bitmap in rows.</param>
    /// <param name="pitch">Absolute row stride of the glyph buffer in bytes.</param>
    /// <param name="glyphBuffer">Raw grayscale alpha bytes from FreeType2.</param>
    /// <param name="invertRows">
    /// <see langword="true"/> when <c>FTBitmap.Pitch</c> is negative (top-down).
    /// </param>
    /// <param name="r">Red channel of the glyph colour.</param>
    /// <param name="g">Green channel of the glyph colour.</param>
    /// <param name="b">Blue channel of the glyph colour.</param>
    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    internal void BlitGrayBitmap(
        int destX, int destY,
        int glyphWidth, int glyphHeight,
        int pitch, byte[] glyphBuffer,
        bool invertRows,
        byte r, byte g, byte b)
    {
        for (var row = 0; row < glyphHeight; row++)
        {
            var srcRow = invertRows ? glyphHeight - 1 - row : row;
            var rowOffset = srcRow * pitch;
            for (var col = 0; col < glyphWidth; col++)
            {
                var alpha = glyphBuffer[rowOffset + col];
                if (alpha == 0)
                    continue;

                SetPixel(destX + col, destY + row, r, g, b, alpha);
            }
        }
    }

    // Writes an opaque RGB pixel directly (used for image blitting, no alpha).
    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    internal void BlitImagePixel(int x, int y, byte r, byte g, byte b)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;

        var i = ((y * Width) + x) * 4;
        _data[i] = r;
        _data[i + 1] = g;
        _data[i + 2] = b;
        _data[i + 3] = 255;
    }

    internal byte[] ToArgbBytes() => _data;
}
