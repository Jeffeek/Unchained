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
    /// Blits a FreeType2 grayscale glyph bitmap onto <paramref name="buffer"/>
    /// using the supplied foreground colour.
    /// </summary>
    internal static void BlitGlyphBitmap(
        this RasterBuffer buffer,
        int destX, int destY,
        FTBitmap bitmap,
        byte r, byte g, byte b)
    {
        if (bitmap.PixelMode != PixelMode.Gray)
            return;

        buffer.BlitGrayBitmap(
            destX, destY,
            bitmap.Width, bitmap.Rows,
            Math.Abs(bitmap.Pitch), bitmap.BufferData,
            invertRows: bitmap.Pitch < 0,
            r, g, b);
    }
}
