using System.Reflection;
using System.Runtime.InteropServices;
using FreeTypeSharp;
using HarfBuzzSharp;
using Buffer = HarfBuzzSharp.Buffer;
using Face = HarfBuzzSharp.Face;

namespace Unchained.Drawing.Text;

/// <summary>
///     Owns the FreeType2 library handle, one HarfBuzz font per loaded typeface,
///     and the corresponding raw font bytes used to create both.
///     Standard 14 fonts are substituted with bundled DejaVu fonts (Bitstream Vera / SIL OFL).
///     Unrecognised fonts fall back to bundled NotoSans-Regular (SIL OFL).
/// </summary>
internal sealed class FontCache : IDisposable
{
    // Each entry: FreeType2 face + HarfBuzz font + the GCHandle that pins the font bytes.
    // FreeType's FT_New_Memory_Face keeps a raw pointer to the byte buffer for the face's
    // lifetime, so the array must stay pinned until the face is disposed — otherwise GC could
    // move it and leave FreeType with a dangling pointer.
    private readonly Dictionary<string, (GlyphFace Face, Font HbFont, GCHandle Pin)> _fonts =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly FreeTypeLibrary _ftLibrary;

    private bool _disposed;

    public FontCache() => _ftLibrary = new FreeTypeLibrary();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var (face, hbFont, pin) in _fonts.Values)
        {
            hbFont.Dispose();
            face.Dispose();
            if (pin.IsAllocated)
                pin.Free();
        }

        _fonts.Clear();
        _ftLibrary.Dispose();
    }

    /// <summary>
    ///     Returns the FreeType2 face and HarfBuzz font for the named typeface.
    ///     When <paramref name="embeddedBytes" /> are provided they are used directly;
    ///     otherwise a bundled substitute font is selected.
    /// </summary>
    public (GlyphFace Face, Font HbFont) GetFonts(string fontName, byte[]? embeddedBytes = null)
    {
        // Use a cache key that includes the embedded byte length when present.
        // This prevents collisions when two different resource names share the same
        // /BaseFont name (common with CFF subsets — e.g. "ABCDEF+Helvetica").
        var cacheKey = embeddedBytes is { Length: > 0 }
            ? $"{fontName}:{embeddedBytes.Length}"
            : fontName;

        if (_fonts.TryGetValue(cacheKey, out var cached)) return (cached.Face, cached.HbFont);

        var bytes = embeddedBytes is { Length: > 0 }
            ? embeddedBytes
            : LoadSubstituteFont(fontName);

        // CreatePair may throw if the font bytes are malformed (truncated CFF, corrupt
        // TrueType, etc.). Fall back to the substitute font so glyph rendering continues.
        (GlyphFace Face, Font HbFont, GCHandle Pin) pair;
        try
        {
            pair = CreatePair(bytes);
        }
        catch
        {
            pair = CreatePair(LoadSubstituteFont(fontName));
        }

        _fonts[cacheKey] = pair;
        return (pair.Face, pair.HbFont);
    }

    public GlyphFace GetFace(string fontName, byte[]? embeddedBytes = null) =>
        GetFonts(fontName, embeddedBytes).Face;

    private (GlyphFace Face, Font HbFont, GCHandle Pin) CreatePair(byte[] fontBytes)
    {
        // Pin fontBytes for the lifetime of the GlyphFace. FreeType's FT_New_Memory_Face keeps
        // a raw pointer into this buffer; if GC moves the array, FreeType reads stale memory and
        // bitmap data becomes corrupt. Freed in Dispose.
        var pin = GCHandle.Alloc(fontBytes, GCHandleType.Pinned);
        var face = new GlyphFace(_ftLibrary, pin.AddrOfPinnedObject(), fontBytes.Length);

        // HarfBuzz needs its own copy (MemoryMode.Duplicate); the temporary pin is released
        // immediately after the blob is built.
        var gch = GCHandle.Alloc(fontBytes, GCHandleType.Pinned);
        Font hbFont;
        try
        {
            using var blob = new Blob(gch.AddrOfPinnedObject(), fontBytes.Length, MemoryMode.Duplicate);
            using var hbFace = new Face(blob, 0);
            hbFont = new Font(hbFace);
            hbFont.SetScale(face.UnitsPerEm, face.UnitsPerEm);
        }
        finally
        {
            gch.Free();
        }

        return (face, hbFont, pin);
    }

    // ── Font selection ────────────────────────────────────────────────────────

    private static byte[] LoadSubstituteFont(string fontName) =>
        LoadEmbeddedFont(SelectResourceName(fontName));

    private static string SelectResourceName(string fontName) => fontName switch
    {
        "Helvetica-Bold" or "Helvetica-BoldOblique"
            or "Arial-Bold" or "Arial-BoldItalic" => "DejaVuSans-Bold.ttf",
        "Helvetica-Oblique" or "Arial-Italic" => "DejaVuSans-Oblique.ttf",
        "Helvetica" or "Helvetica-Regular" or "Arial" or "Arial-Regular"
            or "Calibri" or "Calibri-Regular" => "DejaVuSans-Regular.ttf",
        "Times-Bold" or "Times-BoldItalic" => "DejaVuSerif-Bold.ttf",
        "Times-Roman" or "Times-Italic" => "DejaVuSerif-Regular.ttf",
        "Courier" or "Courier-Bold" or "Courier-Oblique" or "Courier-BoldOblique"
            => "DejaVuSansMono-Regular.ttf",
        _ => "NotoSans-Regular.ttf"
    };

    private static byte[] LoadEmbeddedFont(string resourceFileName)
    {
        var asm = typeof(FontCache).Assembly;
        var resourceName = $"Unchained.Drawing.Text.Fonts.{resourceFileName}";
        using var stream = asm.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException(
                               $"Bundled font resource '{resourceName}' not found in " +
                               $"'{asm.GetName().Name}'. Ensure the font files are included " +
                               "as EmbeddedResource in the project.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    ///     Diagnostic helper used by tests. Shapes the character through HarfBuzz,
    ///     loads the glyph through FreeType2, and returns a human-readable summary.
    ///     Returns an error description instead of throwing.
    /// </summary>
    public string DiagnoseGlyphRender(
        string fontName,
        byte[]? embeddedBytes,
        char ch,
        int pixelSize
    )
    {
        try
        {
            var (face, hbFont) = GetFonts(fontName, embeddedBytes);

            face.SetPixelSize((uint)pixelSize);
            var hbScale = pixelSize * 64;
            hbFont.SetScale(hbScale, hbScale);

            using var buf = new Buffer();
            buf.AddUtf8(ch.ToString());
            buf.GuessSegmentProperties();
            hbFont.Shape(buf);

            var infos = buf.GlyphInfos;
            var positions = buf.GlyphPositions;

            if (infos.Length == 0)
                return $"FAIL: HarfBuzz produced 0 glyphs for '{ch}' in font '{fontName}'";

            var glyphId = infos[0].Codepoint;
            var xAdv = positions[0].XAdvance;

            if (!face.TryLoadGlyph(glyphId))
                return $"FAIL: LoadGlyph({glyphId}) failed for '{ch}' in font '{fontName}'";

            var bm = face.GetGlyphBitmap();

            var nonZero = -1;
            const int maxGlyphDim = 4096;
            if (bm.Buffer != IntPtr.Zero && bm is { Width: > 0 and <= maxGlyphDim, Rows: > 0 and <= maxGlyphDim })
            {
                var absPitch = Math.Abs(bm.Pitch);
                if (absPitch is > 0 and <= maxGlyphDim * 4)
                {
                    var rawBytes = new byte[absPitch * bm.Rows];
                    Marshal.Copy(bm.Buffer, rawBytes, 0, rawBytes.Length);
                    nonZero = rawBytes.Count(static b => b > 0);
                }
            }

            return $"OK: glyphId={glyphId}, xAdv={xAdv}, " +
                   $"bitmap={bm.Width}x{bm.Rows}, mode={bm.PixelMode}, " +
                   $"nonZeroAlpha={nonZero}, " +
                   $"bearingL={bm.Left}, bearingT={bm.Top}";
        }
        catch (Exception ex)
        {
            return $"EXCEPTION in DiagnoseGlyphRender: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
        }
    }
}
