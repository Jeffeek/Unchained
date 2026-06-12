using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HarfBuzzSharp;
using SharpFont;
using Buffer = HarfBuzzSharp.Buffer;
using Face = HarfBuzzSharp.Face;
using FtFace = SharpFont.Face;
using FtLibrary = SharpFont.Library;

namespace Unchained.Drawing.Text;

/// <summary>
///     Owns the FreeType2 library handle, one HarfBuzz font per loaded typeface,
///     and the corresponding raw font bytes used to create both.
///     Standard 14 fonts are substituted with bundled DejaVu fonts (Bitstream Vera / SIL OFL).
///     Unrecognised fonts fall back to bundled NotoSans-Regular (SIL OFL).
/// </summary>
internal sealed class FontCache : IDisposable
{
    private static int _resolverRegistered;

    // Each entry: FreeType2 Face + HarfBuzz Font + the GCHandle that pins the font bytes.
    // SharpFont passes byte[] to FT_New_Memory_Face via P/Invoke, which pins only during the
    // call. FreeType keeps a raw pointer to that buffer for the face's lifetime, so we must
    // keep the array pinned until the face is disposed. GCHandle.Pinned prevents GC from
    // moving the array, which would leave FreeType with a dangling pointer.
    private readonly Dictionary<string, (FtFace FtFace, Font HbFont, GCHandle Pin)> _fonts =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly FtLibrary _ftLibrary;

    private bool _disposed;

    internal FontCache() => _ftLibrary = new FtLibrary();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var (ftFace, hbFont, pin) in _fonts.Values)
        {
            hbFont.Dispose();
            ftFace.Dispose();
            if (pin.IsAllocated) pin.Free();
        }

        _fonts.Clear();
        _ftLibrary.Dispose();
    }

    // ModuleInitializer runs when the Unchained.Drawing.Text assembly is first loaded —
    // before any P/Invoke in SharpFont can fire, regardless of which class triggers the load.
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void RegisterFreeTypeResolver()
#pragma warning restore CA2255
    {
        if (Interlocked.Exchange(ref _resolverRegistered, 1) == 1)
            return;

        NativeLibrary.SetDllImportResolver(typeof(FtLibrary).Assembly, ResolveFreetype);
    }

    private static nint ResolveFreetype(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != "freetype6") return nint.Zero;

        string rid, fileName;
        string[] systemFallbacks;

        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            rid = $"win-{arch}";
            fileName = "freetype6.dll";
            systemFallbacks = ["freetype6.dll"];
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            rid = $"linux-{arch}";
            fileName = "libfreetype.so.6";
            systemFallbacks = ["libfreetype.so.6", "libfreetype.so"];
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            rid = $"osx-{arch}";
            fileName = "libfreetype.6.dylib";
            systemFallbacks = ["libfreetype.6.dylib", "libfreetype.dylib"];
        }
        else
            return nint.Zero;

        // 1. runtimes/{rid}/native/ under the output root (NuGet package convention)
        var runtimesPath = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", fileName);
        if (NativeLibrary.TryLoad(runtimesPath, out var h1)) return h1;

        // 2. Output root directly (flattened copy from Unchained.Drawing.Runtimes)
        var rootPath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (NativeLibrary.TryLoad(rootPath, out var h2)) return h2;

        // 3. System-installed FreeType (e.g. apt/yum on Linux)
        foreach (var name in systemFallbacks)
        {
            if (NativeLibrary.TryLoad(name, assembly, searchPath, out var h3))
                return h3;
        }

        return nint.Zero;
    }

    /// <summary>
    ///     Returns the FreeType2 face and HarfBuzz font for the named typeface.
    ///     When <paramref name="embeddedBytes" /> are provided they are used directly;
    ///     otherwise a bundled substitute font is selected.
    /// </summary>
    internal (FtFace FtFace, Font HbFont) GetFonts(string fontName, byte[]? embeddedBytes = null)
    {
        // Use a cache key that includes a hash of the embedded bytes when present.
        // This prevents collisions when two different resource names share the same
        // /BaseFont name (common with CFF subsets — e.g. "ABCDEF+Helvetica").
        var cacheKey = embeddedBytes is { Length: > 0 }
            ? $"{fontName}:{embeddedBytes.Length}"
            : fontName;

        if (_fonts.TryGetValue(cacheKey, out var cached)) return (cached.FtFace, cached.HbFont);

        var bytes = embeddedBytes is { Length: > 0 }
            ? embeddedBytes
            : LoadSubstituteFont(fontName);

        // CreatePair may throw if the font bytes are malformed (truncated CFF, corrupt
        // TrueType, etc.). Fall back to the substitute font so glyph rendering continues.
        (FtFace FtFace, Font HbFont, GCHandle Pin) pair;
        try
        {
            pair = CreatePair(bytes);
        }
        catch
        {
            pair = CreatePair(LoadSubstituteFont(fontName));
        }

        _fonts[cacheKey] = pair;
        return (pair.FtFace, pair.HbFont);
    }

    internal FtFace GetFace(string fontName, byte[]? embeddedBytes = null) =>
        GetFonts(fontName, embeddedBytes).FtFace;

    private (FtFace FtFace, Font HbFont, GCHandle Pin) CreatePair(byte[] fontBytes)
    {
        // Pin fontBytes for the lifetime of the FtFace. SharpFont passes the array to
        // FT_New_Memory_Face via P/Invoke (pinned only during the call); FreeType then
        // keeps a raw pointer into that buffer. If GC moves the array afterwards, FreeType
        // reads stale memory and bitmap data becomes corrupt/garbage.
        var pin = GCHandle.Alloc(fontBytes, GCHandleType.Pinned);
        var ftFace = _ftLibrary.NewMemoryFace(fontBytes, 0);

        var gch = GCHandle.Alloc(fontBytes, GCHandleType.Pinned);
        Font hbFont;
        try
        {
            using var blob = new Blob(gch.AddrOfPinnedObject(), fontBytes.Length, MemoryMode.Duplicate);
            using var hbFace = new Face(blob, 0);
            hbFont = new Font(hbFace);
            hbFont.SetScale(ftFace.UnitsPerEM, ftFace.UnitsPerEM);
        }
        finally
        {
            gch.Free();
        }

        return (ftFace, hbFont, pin);
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
    internal string DiagnoseGlyphRender(
        string fontName,
        byte[]? embeddedBytes,
        char ch,
        int pixelSize
    )
    {
        try
        {
            var (ftFace, hbFont) = GetFonts(fontName, embeddedBytes);

            ftFace.SetPixelSizes(0, (uint)pixelSize);
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

            // Capture full stack trace to pinpoint the overflow
            string? loadError = null;
            try
            {
                ftFace.LoadGlyph(glyphId, LoadFlags.Render, LoadTarget.Normal);
            }
            catch (Exception ex)
            {
                loadError = $"FAIL: LoadGlyph({glyphId}) threw {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
            }

            if (loadError is not null)
                return loadError;

            var bm = ftFace.Glyph.Bitmap;
            // Do NOT use bm.BufferData — throws OverflowException for negative Pitch.
            // Read via Marshal.Copy from bm.Buffer instead.
            // Guard against garbage values from SharpFont struct offset mismatch on Windows x64.
            var nonZero = -1;
            const int maxGlyphDim = 4096;
            if (bm.Buffer != IntPtr.Zero && bm.Width > 0 && bm.Rows > 0
                && bm.Width <= maxGlyphDim && bm.Rows <= maxGlyphDim)
            {
                var absPitch = Math.Abs(bm.Pitch);
                if (absPitch > 0 && absPitch <= maxGlyphDim * 4)
                {
                    var rawBytes = new byte[absPitch * bm.Rows];
                    Marshal.Copy(bm.Buffer, rawBytes, 0, rawBytes.Length);
                    nonZero = rawBytes.Count(b => b > 0);
                }
            }

            return $"OK: glyphId={glyphId}, xAdv={xAdv}, " +
                   $"bitmap={bm.Width}x{bm.Rows}, mode={bm.PixelMode}, " +
                   $"nonZeroAlpha={nonZero}, " +
                   $"bearingL={ftFace.Glyph.BitmapLeft}, bearingT={ftFace.Glyph.BitmapTop}";
        }
        catch (Exception ex)
        {
            return $"EXCEPTION in DiagnoseGlyphRender: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
        }
    }
}
