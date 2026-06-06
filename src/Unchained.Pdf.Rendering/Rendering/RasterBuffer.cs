using System.Diagnostics.CodeAnalysis;
using SharpFont;

namespace Unchained.Pdf.Rendering.Rendering;

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
        GlyphPixelsWritten = GlyphCallsWithZeroDims = GlyphCallsWithNullBuf =
            GlyphCallsWithZeroPitch = GlyphCallsUnknownMode = GlyphCallsAllAlphaZero = 0;
        for (var i = 0; i < _data.Length; i += 4)
        {
            _data[i] = r;
            _data[i + 1] = g;
            _data[i + 2] = b;
            _data[i + 3] = 255;
        }
    }

    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    private void SetPixel(int x, int y, byte r, byte g, byte b, byte a)
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
            // ReSharper disable once BadListLineBreaks
            SetPixel(px, py, r, g, b, a);
    }

    // Bresenham line with configurable thickness.
    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    internal void DrawLine(
        int x0, int y0, int x1, int y1,
        byte r, byte g, byte b,
        int thicknessPx = 1
    )
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

    // Diagnostic counters reset by Clear() each render.
    internal int GlyphPixelsWritten  { get; private set; }
    internal int GlyphCallsWithZeroDims  { get; private set; }  // w=0 or h=0
    internal int GlyphCallsWithNullBuf   { get; private set; }  // Buffer=Zero
    internal int GlyphCallsWithZeroPitch { get; private set; }  // absPitch=0
    internal int GlyphCallsUnknownMode   { get; private set; }  // PixelMode not Gray/Mono
    internal int GlyphCallsAllAlphaZero  { get; private set; }  // ran loop, no non-zero alpha
    // Last glyph diagnostic (overwritten each call)
    internal (int W, int H, int Pitch, int PixelMode, int NonZeroAlpha) LastGlyphDiag { get; private set; }

    // Blit a FreeType2 glyph bitmap at (destX, destY) using the given colour.
    // Handles Gray (anti-aliased) and Mono (1-bit) pixel modes.
    //
    // IMPORTANT: We do NOT use bitmap.BufferData (the SharpFont property).
    // That property throws OverflowException for bottom-up bitmaps (Pitch < 0)
    // because it multiplies Pitch * Rows with signed arithmetic. Instead we
    // read each row directly via Marshal.Copy from bitmap.Buffer, which is always valid.
    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    internal void BlitGlyphBitmap(int destX, int destY, FTBitmap bitmap, byte r, byte g, byte b)
    {
        var w    = bitmap.Width;
        var h    = bitmap.Rows;

        if (w <= 0 || h <= 0) { GlyphCallsWithZeroDims++; return; }
        if (bitmap.Buffer == IntPtr.Zero) { GlyphCallsWithNullBuf++; return; }

        var pitch    = bitmap.Pitch;
        var absPitch = Math.Abs(pitch);
        if (absPitch == 0) { GlyphCallsWithZeroPitch++; return; }

        LastGlyphDiag = (w, h, pitch, (int)bitmap.PixelMode, 0);

        switch (bitmap.PixelMode)
        {
            case PixelMode.Gray:
            {
                // 8 bits per pixel: alpha coverage (0 = transparent, 255 = opaque)
                var rowBuf   = new byte[absPitch];
                var nonZeroA = 0;
                for (var row = 0; row < h; row++)
                {
                    var srcRow = pitch >= 0 ? row : h - 1 - row;
                    System.Runtime.InteropServices.Marshal.Copy(
                        IntPtr.Add(bitmap.Buffer, srcRow * absPitch), rowBuf, 0, absPitch);

                    for (var col = 0; col < w; col++)
                    {
                        var alpha = rowBuf[col];
                        if (alpha == 0) continue;
                        nonZeroA++;
                        SetPixel(destX + col, destY + row, r, g, b, alpha);
                        GlyphPixelsWritten++;
                    }
                }
                if (nonZeroA == 0) GlyphCallsAllAlphaZero++;
                LastGlyphDiag = (w, h, pitch, (int)bitmap.PixelMode, nonZeroA);
                break;
            }

            case PixelMode.Mono:
            {
                // 1 bit per pixel, packed MSB-first; 1 = opaque, 0 = transparent
                var rowBuf   = new byte[absPitch];
                var nonZeroA = 0;
                for (var row = 0; row < h; row++)
                {
                    var srcRow = pitch >= 0 ? row : h - 1 - row;
                    System.Runtime.InteropServices.Marshal.Copy(
                        IntPtr.Add(bitmap.Buffer, srcRow * absPitch), rowBuf, 0, absPitch);

                    for (var col = 0; col < w; col++)
                    {
                        var byteIdx = col >> 3;
                        var bitMask = 0x80 >> (col & 7);
                        if ((rowBuf[byteIdx] & bitMask) == 0) continue;
                        nonZeroA++;
                        SetPixel(destX + col, destY + row, r, g, b, 255);
                        GlyphPixelsWritten++;
                    }
                }
                if (nonZeroA == 0) GlyphCallsAllAlphaZero++;
                LastGlyphDiag = (w, h, pitch, (int)bitmap.PixelMode, nonZeroA);
                break;
            }

            default:
                GlyphCallsUnknownMode++;
                LastGlyphDiag = (w, h, pitch, (int)bitmap.PixelMode, -1);
                break;
        }
    }

    // Writes an opaque RGB pixel directly (used for image XObject blitting, no alpha).
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
