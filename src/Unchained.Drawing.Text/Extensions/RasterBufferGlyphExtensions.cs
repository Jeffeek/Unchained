using System.Runtime.InteropServices;

namespace Unchained.Drawing.Text.Extensions;

/// <summary>
///     Extends <see cref="RasterBuffer" /> with glyph blitting from FreeType2 bitmaps.
///     Defined here (Drawing.Text) rather than in Drawing because it requires a
///     <see cref="GlyphFace" />.
/// </summary>
internal static class RasterBufferGlyphExtensions
{
    // FreeType PixelMode constants.
    private const int PixelModeGray = 2;
    private const int PixelModeMono = 1;

    /// <summary>
    ///     Reads the rendered glyph from <paramref name="face" /> and blits it onto
    ///     <paramref name="buffer" /> at the given pen position.
    /// </summary>
    internal static void BlitGlyphFromFace(
        this RasterBuffer buffer,
        int penX,
        int penY,
        GlyphFace face,
        byte r,
        byte g,
        byte b,
        string blendMode = "Normal"
    )
    {
        var bitmap = face.GetGlyphBitmap();
        var destX = penX + bitmap.Left;
        var destY = penY - bitmap.Top;

        BlitFromNativeBuffer(buffer,
            destX,
            destY,
            bitmap.Width,
            bitmap.Rows,
            bitmap.Pitch,
            bitmap.Buffer,
            bitmap.PixelMode,
            r,
            g,
            b,
            blendMode);
    }

    // Low-level blitter: given raw glyph bitmap fields, reads pixel data and blits.
    private static void BlitFromNativeBuffer(
        RasterBuffer buffer,
        int destX,
        int destY,
        int w,
        int h,
        int pitch,
        IntPtr bufPtr,
        int pixelMode,
        byte r,
        byte g,
        byte b,
        string blendMode = "Normal"
    )
    {
        if (w <= 0 || h <= 0 || bufPtr == IntPtr.Zero) return;

        var absPitch = Math.Abs(pitch);
        if (absPitch == 0) return;

        const int maxGlyphDim = 4096;
        if (w > maxGlyphDim || h > maxGlyphDim || absPitch > maxGlyphDim * 4) return;

        switch (pixelMode)
        {
            case PixelModeGray:
            {
                var rowBuf = new byte[absPitch];
                for (var row = 0; row < h; row++)
                {
                    var srcRow = pitch >= 0 ? row : h - 1 - row;
                    Marshal.Copy(IntPtr.Add(bufPtr, srcRow * absPitch), rowBuf, 0, absPitch);
                    buffer.BlitGrayBitmap(destX,
                        destY + row,
                        w,
                        1,
                        absPitch,
                        rowBuf,
                        false,
                        r,
                        g,
                        b,
                        blendMode);
                }

                break;
            }

            case PixelModeMono:
            {
                var monoRow = new byte[absPitch];
                var grayRow = new byte[w];
                for (var row = 0; row < h; row++)
                {
                    var srcRow = pitch >= 0 ? row : h - 1 - row;
                    Marshal.Copy(IntPtr.Add(bufPtr, srcRow * absPitch), monoRow, 0, absPitch);
                    for (var col = 0; col < w; col++)
                        grayRow[col] = (monoRow[col >> 3] & (0x80 >> (col & 7))) != 0 ? (byte)255 : (byte)0;
                    buffer.BlitGrayBitmap(destX,
                        destY + row,
                        w,
                        1,
                        w,
                        grayRow,
                        false,
                        r,
                        g,
                        b,
                        blendMode);
                }

                break;
            }
        }
    }
}
