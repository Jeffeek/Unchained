using SharpFont;

namespace Unchained.Pdf.Rendering.Rendering;

/// <summary>
/// ARGB (4 bytes per pixel, row-major) pixel buffer used as the render target.
/// The byte layout per pixel is: [R, G, B, A] at indices [4*i, 4*i+1, 4*i+2, 4*i+3].
/// </summary>
internal sealed class RasterBuffer(int width, int height)
{
    private readonly byte[] _data = new byte[width * height * 4];

    internal int Width  { get; } = width;
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

    private void SetPixel(int x, int y, byte r, byte g, byte b, byte a)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
        var i = (y * Width + x) * 4;
        if (a == 255)
        {
            _data[i] = r; _data[i + 1] = g; _data[i + 2] = b; _data[i + 3] = 255;
        }
        else
        {
            // Alpha-blend over existing pixel
            var inv = 255 - a;
            _data[i]     = (byte)((_data[i]     * inv + r * a) / 255);
            _data[i + 1] = (byte)((_data[i + 1] * inv + g * a) / 255);
            _data[i + 2] = (byte)((_data[i + 2] * inv + b * a) / 255);
            _data[i + 3] = 255;
        }
    }

    internal void FillRect(int x, int y, int w, int h, byte r, byte g, byte b, byte a = 255)
    {
        var x1 = Math.Max(0, x);
        var y1 = Math.Max(0, y);
        var x2 = Math.Min(Width,  x + w);
        var y2 = Math.Min(Height, y + h);
        for (var py = y1; py < y2; py++)
            for (var px = x1; px < x2; px++)
                SetPixel(px, py, r, g, b, a);
    }

    // Bresenham line with configurable thickness.
    internal void DrawLine(
        int x0, int y0, int x1, int y1,
        byte r, byte g, byte b,
        int thicknessPx = 1
    )
    {
        var dx = Math.Abs(x1 - x0); var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;  var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;
        var half = thicknessPx / 2;

        while (true)
        {
            for (var ty = -half; ty <= half; ty++)
                for (var tx = -half; tx <= half; tx++)
                    SetPixel(x0 + tx, y0 + ty, r, g, b, 255);

            if (x0 == x1 && y0 == y1) break;
            var e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx)  { err += dx; y0 += sy; }
        }
    }

    // Blit a FreeType2 grayscale glyph bitmap at (destX, destY) using the given colour.
    internal void BlitGlyphBitmap(int destX, int destY, FTBitmap bitmap, byte r, byte g, byte b)
    {
        if (bitmap.PixelMode != PixelMode.Gray) return;
        var w = bitmap.Width;
        var h = bitmap.Rows;
        var pitch = Math.Abs(bitmap.Pitch);
        var buffer = bitmap.BufferData;

        for (var row = 0; row < h; row++)
        {
            var srcRow = bitmap.Pitch < 0 ? h - 1 - row : row;
            var rowOffset = srcRow * pitch;
            for (var col = 0; col < w; col++)
            {
                var alpha = buffer[rowOffset + col];
                if (alpha == 0) continue;
                SetPixel(destX + col, destY + row, r, g, b, alpha);
            }
        }
    }

    internal byte[] ToArgbBytes() => _data;
}
