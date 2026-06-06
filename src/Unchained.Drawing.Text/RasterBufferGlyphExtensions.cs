using System.Runtime.InteropServices;
using Unchained.Drawing;
using SharpFont;

namespace Unchained.Drawing.Text;

/// <summary>
/// Extends <see cref="RasterBuffer"/> with glyph blitting from FreeType2 bitmaps.
/// Defined here (Drawing.Text) rather than in Drawing because it requires SharpFont.
/// </summary>
internal static class RasterBufferGlyphExtensions
{
    /// <summary>
    /// Blits a FreeType2 glyph bitmap onto <paramref name="buffer"/>
    /// using the supplied foreground colour. Handles Gray and Mono pixel modes.
    /// </summary>
    internal static void BlitGlyphBitmap(
        this RasterBuffer buffer,
        int destX, int destY,
        FTBitmap bitmap,
        byte r, byte g, byte b)
    {
        var w = bitmap.Width;
        var h = bitmap.Rows;
        if (w <= 0 || h <= 0 || bitmap.Buffer == IntPtr.Zero) return;

        var pitch = bitmap.Pitch;
        var absPitch = Math.Abs(pitch);
        if (absPitch == 0) return;

        switch (bitmap.PixelMode)
        {
            case PixelMode.Gray:
            {
                // Read each row via Marshal.Copy from the raw IntPtr.
                // Do NOT use bitmap.BufferData: it multiplies Rows * Pitch with signed
                // arithmetic and throws OverflowException when Pitch is negative.
                var rowBuf = new byte[absPitch];
                for (var row = 0; row < h; row++)
                {
                    var srcRow = pitch >= 0 ? row : h - 1 - row;
                    Marshal.Copy(IntPtr.Add(bitmap.Buffer, srcRow * absPitch), rowBuf, 0, absPitch);
                    buffer.BlitGrayBitmap(destX, destY + row, w, 1, absPitch, rowBuf,
                        invertRows: false, r, g, b);
                }
                break;
            }

            case PixelMode.Mono:
            {
                // 1 bit per pixel, packed MSB-first; unpack to a Gray byte row before blitting.
                var monoRow = new byte[absPitch];
                var grayRow = new byte[w];
                for (var row = 0; row < h; row++)
                {
                    var srcRow = pitch >= 0 ? row : h - 1 - row;
                    Marshal.Copy(IntPtr.Add(bitmap.Buffer, srcRow * absPitch), monoRow, 0, absPitch);
                    for (var col = 0; col < w; col++)
                        grayRow[col] = (monoRow[col >> 3] & (0x80 >> (col & 7))) != 0 ? (byte)255 : (byte)0;
                    buffer.BlitGrayBitmap(destX, destY + row, w, 1, w, grayRow,
                        invertRows: false, r, g, b);
                }
                break;
            }
        }
    }
}
