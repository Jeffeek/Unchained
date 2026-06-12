using FreeTypeSharp;
using static FreeTypeSharp.FT;
using static FreeTypeSharp.FT_LOAD;
using static FreeTypeSharp.FT_Render_Mode_;

namespace Unchained.Drawing.Text;

/// <summary>
///     Owns a FreeType2 face (created from in-memory font bytes) and exposes the narrow set
///     of operations the renderers need: sizing, glyph loading + rasterization, char-index
///     lookup, advance measurement, and access to the rendered bitmap and glyph outline.
///     This is the only type that touches the FreeType backend directly — consumers work
///     against <see cref="GlyphFace" /> so the backend can be swapped in one place.
/// </summary>
internal sealed unsafe class GlyphFace : IDisposable
{
    private FT_FaceRec_* _face;
    private bool _disposed;

    internal GlyphFace(FreeTypeLibrary library, nint fontData, int dataLength)
    {
        FT_FaceRec_* face;
        var error = FT_New_Memory_Face(library.Native, (byte*)fontData, dataLength, 0, &face);
        if (error != FT_Error.FT_Err_Ok)
            throw new FreeTypeException(error);

        _face = face;
    }

    /// <summary>Font units per em — used to scale the HarfBuzz font to match.</summary>
    public ushort UnitsPerEm => _face->units_per_EM;

    /// <summary>Sets the rasterization size in whole pixels.</summary>
    public void SetPixelSize(uint pixelSize) => FT_Set_Pixel_Sizes(_face, 0, pixelSize);

    /// <summary>Maps a Unicode character code to a glyph index via the font's charmap.</summary>
    public uint GetCharIndex(uint charCode) => FT_Get_Char_Index(_face, (UIntPtr)charCode);

    /// <summary>
    ///     Loads and rasterizes the glyph at <paramref name="glyphIndex" />.
    ///     Returns <see langword="false" /> when FreeType rejects the glyph (caller skips it).
    /// </summary>
    public bool TryLoadGlyph(uint glyphIndex, bool hinting = false)
    {
        var flags = FT_LOAD_RENDER | (hinting ? FT_LOAD_DEFAULT : FT_LOAD_NO_HINTING);
        return FT_Load_Glyph(_face, glyphIndex, flags) == FT_Error.FT_Err_Ok;
    }

    /// <summary>
    ///     Loads the glyph outline without rasterizing (for stroke/clip text render modes).
    ///     Returns <see langword="false" /> when FreeType rejects the glyph.
    /// </summary>
    public bool TryLoadGlyphOutline(uint glyphIndex)
    {
        var flags = FT_LOAD_NO_BITMAP | FT_LOAD_NO_HINTING;
        return FT_Load_Glyph(_face, glyphIndex, flags) == FT_Error.FT_Err_Ok;
    }

    /// <summary>
    ///     Horizontal advance of <paramref name="glyphIndex" /> in 16.16 fixed-point pixels at
    ///     the current size. Returns 0 when FreeType cannot measure it.
    /// </summary>
    public long GetAdvance(uint glyphIndex)
    {
        nint advance;
        return FT_Get_Advance(_face, glyphIndex, FT_LOAD_DEFAULT, &advance) == FT_Error.FT_Err_Ok
            ? advance
            : 0;
    }

    /// <summary>
    ///     Reads the rasterized glyph bitmap of the last loaded glyph. Fields come straight
    ///     from the correctly-marshaled FreeType structs — no hand-coded offsets.
    /// </summary>
    public GlyphBitmap GetGlyphBitmap()
    {
        var slot = _face->glyph;
        var bitmap = slot->bitmap;
        return new GlyphBitmap(
            (int)bitmap.width,
            (int)bitmap.rows,
            bitmap.pitch,
            (nint)bitmap.buffer,
            (int)bitmap.pixel_mode,
            slot->bitmap_left,
            slot->bitmap_top);
    }

    /// <summary>
    ///     Returns the outline of the last loaded glyph as one polyline per contour, in
    ///     26.6 fixed-point font pixels (X right, Y up). Off-curve control points are kept
    ///     as-is; callers approximate them as line segments. Empty when the glyph has no
    ///     outline (e.g. a bitmap-only or empty glyph).
    /// </summary>
    public IReadOnlyList<(double X, double Y)[]> GetGlyphContours()
    {
        var outline = _face->glyph->outline;
        var pointCount = outline.n_points;
        var contourCount = outline.n_contours;
        if (pointCount <= 0 || contourCount <= 0 || outline.points == null || outline.contours == null)
            return [];

        var result = new List<(double X, double Y)[]>(contourCount);
        var start = 0;
        for (var c = 0; c < contourCount; c++)
        {
            int end = outline.contours[c];
            var count = end - start + 1;
            if (count > 0)
            {
                var poly = new (double X, double Y)[count];
                for (var j = 0; j < count; j++)
                {
                    var point = outline.points[start + j];
                    poly[j] = ((long)point.x / 64.0, (long)point.y / 64.0);
                }

                result.Add(poly);
            }

            start = end + 1;
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_face != null)
        {
            FT_Done_Face(_face);
            _face = null;
        }
    }
}

/// <summary>
///     Snapshot of a rendered FreeType glyph bitmap. <see cref="Buffer" /> points into native
///     memory owned by the face and is only valid until the next glyph is loaded.
/// </summary>
/// <param name="Width">Bitmap width in pixels.</param>
/// <param name="Rows">Bitmap height in pixels.</param>
/// <param name="Pitch">Bytes per row; negative when rows run bottom-up.</param>
/// <param name="Buffer">Pointer to the first byte of pixel data.</param>
/// <param name="PixelMode">FreeType pixel mode (1 = mono, 2 = gray).</param>
/// <param name="Left">Horizontal bearing — pixels from the pen X to the bitmap's left edge.</param>
/// <param name="Top">Vertical bearing — pixels from the baseline up to the bitmap's top edge.</param>
internal readonly record struct GlyphBitmap(
    int Width,
    int Rows,
    int Pitch,
    nint Buffer,
    int PixelMode,
    int Left,
    int Top
);
