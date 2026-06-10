using System.Reflection;
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
    // SharpFont uses IntPtr (8 bytes) for FT_Long fields on any 64-bit process, but on
    // Windows x64 FT_Long is C `long` = 4 bytes (MSVC convention). This mismatch shifts
    // every struct field that follows a NativeLong field, placing face->glyph at offset 152
    // instead of the correct 120, and glyph slot fields proportionally off.
    //
    // On Linux / macOS x64, C `long` = 8 bytes → SharpFont's offsets are correct there.
    private static readonly bool NeedsDirectOffsets =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && IntPtr.Size == 8;

    // SharpFont.NativeObject.Reference is protected, so we access it once via reflection
    // and cache the PropertyInfo. GetValue is fast (no type/name string lookup per call).
    private static readonly PropertyInfo? FaceRefProp =
        typeof(Face).GetProperty("Reference",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? typeof(Face).BaseType?.GetProperty("Reference",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static IntPtr GetFaceRef(Face face) =>
        FaceRefProp?.GetValue(face) is IntPtr r ? r : IntPtr.Zero;

    // Byte offsets for Windows x64 (FT_Long = 4 bytes).
    // FT_FaceRec_::glyph comes after 5×long(4) + padding + 2×ptr + int + ptr + int + ptr
    //   + 2×ptr(generic) + 4×long(bbox) + 8×short → 120.
    private const int FaceGlyphOffset      = 120;

    // FT_GlyphSlotRec_::bitmap is at:
    //   3×ptr(8) + uint(4) + pad(4) + 2×ptr(generic,16) + 8×long(metrics,32)
    //   + 2×long(linearAdv,8) + 2×long(advance,8) + ulong(format,4) + pad(4) = 104.
    private const int SlotBitmapBase       = 104;
    private const int SlotBitmapRows       = SlotBitmapBase + 0;   // unsigned int
    private const int SlotBitmapWidth      = SlotBitmapBase + 4;   // unsigned int
    private const int SlotBitmapPitch      = SlotBitmapBase + 8;   // int
    // +12 is padding (to 8-byte-align the pointer at +16)
    private const int SlotBitmapBuffer     = SlotBitmapBase + 16;  // unsigned char* (ptr)
    // +24 num_grays (ushort=2), +26 pixel_mode (uchar=1)
    private const int SlotBitmapPixelMode  = SlotBitmapBase + 26;  // pixel_mode byte
    // FT_Bitmap ends at base+40 (with 4-byte padding before palette ptr at +32)
    private const int SlotBitmapLeft       = SlotBitmapBase + 40;  // bitmap_left  FT_Int
    private const int SlotBitmapTop        = SlotBitmapBase + 44;  // bitmap_top   FT_Int

    // FreeType PixelMode constants (matches SharpFont.PixelMode enum values).
    private const int PixelModeGray = 2;
    private const int PixelModeMono = 1;

    /// <summary>
    /// Reads the rendered glyph from <paramref name="ftFace"/> using correct native struct
    /// offsets and blits it onto <paramref name="buffer"/> at the given pen position.
    /// This avoids SharpFont's wrong <c>face-&gt;glyph</c> offset on Windows x64.
    /// </summary>
    internal static void BlitGlyphFromFace(
        this RasterBuffer buffer,
        int penX, int penY,
        Face ftFace,
        byte r, byte g, byte b,
        string blendMode = "Normal")
    {
        int w, h, pitch, pixelMode, bitmapLeft, bitmapTop;
        IntPtr bufPtr;

        if (NeedsDirectOffsets)
        {
            var faceRef = GetFaceRef(ftFace);
            if (faceRef == IntPtr.Zero) return;
            var glyphSlotPtr = Marshal.ReadIntPtr(faceRef, FaceGlyphOffset);
            if (glyphSlotPtr == IntPtr.Zero) return;

            h           = Marshal.ReadInt32(glyphSlotPtr, SlotBitmapRows);
            w           = Marshal.ReadInt32(glyphSlotPtr, SlotBitmapWidth);
            pitch       = Marshal.ReadInt32(glyphSlotPtr, SlotBitmapPitch);
            bufPtr      = Marshal.ReadIntPtr(glyphSlotPtr, SlotBitmapBuffer);
            pixelMode   = Marshal.ReadByte(glyphSlotPtr, SlotBitmapPixelMode);
            bitmapLeft  = Marshal.ReadInt32(glyphSlotPtr, SlotBitmapLeft);
            bitmapTop   = Marshal.ReadInt32(glyphSlotPtr, SlotBitmapTop);
        }
        else
        {
            var glyph   = ftFace.Glyph;
            var bm      = glyph.Bitmap;
            w           = bm.Width;
            h           = bm.Rows;
            pitch       = bm.Pitch;
            bufPtr      = bm.Buffer;
            pixelMode   = (int)bm.PixelMode;
            bitmapLeft  = glyph.BitmapLeft;
            bitmapTop   = glyph.BitmapTop;
        }

        var destX = penX + bitmapLeft;
        var destY = penY - bitmapTop;

        BlitFromNativeBuffer(buffer, destX, destY, w, h, pitch, bufPtr, pixelMode, r, g, b, blendMode);
    }

    // Low-level blitter: given raw glyph bitmap fields, reads pixel data and blits.
    private static void BlitFromNativeBuffer(
        RasterBuffer buffer,
        int destX, int destY,
        int w, int h, int pitch,
        IntPtr bufPtr,
        int pixelMode,
        byte r, byte g, byte b,
        string blendMode = "Normal")
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
                    buffer.BlitGrayBitmap(destX, destY + row, w, 1, absPitch, rowBuf,
                        invertRows: false, r, g, b, blendMode);
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
                    buffer.BlitGrayBitmap(destX, destY + row, w, 1, w, grayRow,
                        invertRows: false, r, g, b, blendMode);
                }
                break;
            }
        }
    }

    /// <summary>
    /// Blits a FreeType2 glyph bitmap onto <paramref name="buffer"/> using an
    /// <see cref="FTBitmap"/> value obtained from SharpFont. Prefer
    /// <see cref="BlitGlyphFromFace"/> when the <see cref="Face"/> is available,
    /// because SharpFont's glyph-slot pointer is incorrect on Windows x64.
    /// </summary>
    internal static void BlitGlyphBitmap(
        this RasterBuffer buffer,
        int destX, int destY,
        FTBitmap bitmap,
        byte r, byte g, byte b)
    {
        BlitFromNativeBuffer(
            buffer, destX, destY,
            bitmap.Width, bitmap.Rows, bitmap.Pitch, bitmap.Buffer,
            (int)bitmap.PixelMode, r, g, b);
    }
}
