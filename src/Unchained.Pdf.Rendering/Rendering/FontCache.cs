using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using HarfBuzzSharp;
using FtFace = SharpFont.Face;
using FtLibrary = SharpFont.Library;

namespace Unchained.Pdf.Rendering.Rendering;

/// <summary>
/// Owns the FreeType2 library handle, one HarfBuzz font per loaded typeface,
/// and the corresponding raw font bytes used to create both.
/// Standard 14 fonts are substituted with bundled DejaVu fonts (Bitstream Vera / SIL OFL).
/// Unrecognised fonts fall back to bundled NotoSans-Regular (SIL OFL).
/// </summary>
internal sealed class FontCache : IDisposable
{
    private readonly FtLibrary _ftLibrary;

    // Each entry: FreeType2 Face (for rasterisation) + HarfBuzz Font (for shaping).
    private readonly Dictionary<string, (FtFace FtFace, Font HbFont)> _fonts =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    // SharpFont uses DllImport("freetype6") on all platforms, but the system library is
    // named differently on Linux (libfreetype.so.6) and macOS (libfreetype.6.dylib).
    // This resolver bridges that gap for .NET 5+, where Mono's dllmap is not used.
    static FontCache() => NativeLibrary.SetDllImportResolver(typeof(FtLibrary).Assembly, ResolveFreetype);

    private static nint ResolveFreetype(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != "freetype6") return nint.Zero;

        string[] candidates;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            candidates = ["libfreetype.so.6", "libfreetype.so"];
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            candidates = ["libfreetype.6.dylib", "libfreetype.dylib"];
        else
            return nint.Zero; // Windows: freetype6.dll resolved by default

        // Unchained.Pdf.Runtimes copies the native file to the output root (AppContext.BaseDirectory)
        // via Link="<name>" + CopyToOutputDirectory. Probe the absolute path first so we don't depend
        // on DllImportSearchPath resolving into that directory.
        var baseDir = AppContext.BaseDirectory;
        foreach (var fullPath in candidates.Select(name => Path.Combine(baseDir, name)))
        {
            if (File.Exists(fullPath) && NativeLibrary.TryLoad(fullPath, out var h))
                return h;
        }

        // Fall back to system-installed FreeType (e.g. Homebrew on macOS, apt on Linux).
        foreach (var name in candidates)
        {
            if (NativeLibrary.TryLoad(name, assembly, searchPath, out var h))
                return h;
        }

        return nint.Zero;
    }

    internal FontCache() => _ftLibrary = new FtLibrary();

    /// <summary>
    /// Returns the FreeType2 <see cref="HarfBuzzSharp.Face"/> and HarfBuzz <see cref="Font"/> for the
    /// named font. If <paramref name="embeddedBytes"/> are provided (an embedded font program
    /// from the PDF), they are used; otherwise the bundled substitute is selected.
    /// </summary>
    internal (FtFace FtFace, Font HbFont) GetFonts(string fontName, byte[]? embeddedBytes = null)
    {
        if (_fonts.TryGetValue(fontName, out var cached)) return cached;

        var bytes = embeddedBytes is { Length: > 0 }
            ? embeddedBytes
            : LoadSubstituteFont(fontName);

        var pair = CreatePair(bytes);
        _fonts[fontName] = pair;

        return pair;
    }

    // Convenience overload — returns only the FreeType2 face (used by path-rendering paths).
    internal FtFace GetFace(string fontName, byte[]? embeddedBytes = null) =>
        GetFonts(fontName, embeddedBytes).FtFace;

    private (FtFace FtFace, Font HbFont) CreatePair(byte[] fontBytes)
    {
        var ftFace = _ftLibrary.NewMemoryFace(fontBytes, 0);

        // HarfBuzz Blob requires an IntPtr to the data.
        // GCHandle.Alloc pins the managed array; MemoryMode.Duplicate copies it internally
        // so we can safely release the pin after the Blob is constructed.
        var gch = GCHandle.Alloc(fontBytes, GCHandleType.Pinned);
        Font hbFont;
        try
        {
            using var blob = new Blob(gch.AddrOfPinnedObject(), fontBytes.Length, MemoryMode.Duplicate);
            using var hbFace = new Face(blob, 0);
            hbFont = new Font(hbFace);
            // Scale HarfBuzz advances to FreeType2 glyph-space (units-per-em).
            // PageRenderer rescales to pixel units via SetScale before each shaping call.
            hbFont.SetScale(ftFace.UnitsPerEM, ftFace.UnitsPerEM);
        }
        finally
        {
            gch.Free();
        }

        return (ftFace, hbFont);
    }

    // ── Font selection ────────────────────────────────────────────────────────

    private static byte[] LoadSubstituteFont(string fontName) =>
        LoadEmbeddedFont(SelectResourceName(fontName));

    // Priority: Standard 14 → DejaVu variant → NotoSans (broadest Unicode coverage).
    private static string SelectResourceName(string fontName) => fontName switch
    {
        "Helvetica-Bold" or "Helvetica-BoldOblique" => "DejaVuSans-Bold.ttf",
        "Helvetica-Oblique" => "DejaVuSans-Oblique.ttf",
        "Helvetica" or "Helvetica-Regular" => "DejaVuSans-Regular.ttf",
        "Times-Bold" or "Times-BoldItalic" => "DejaVuSerif-Bold.ttf",
        "Times-Roman" or "Times-Italic" => "DejaVuSerif-Regular.ttf",
        "Courier" or "Courier-Bold" or "Courier-Oblique" or "Courier-BoldOblique"
            => "DejaVuSansMono-Regular.ttf",
        // All other fonts (embedded with unrecognised name, or non-Standard-14 system fonts)
        // fall back to NotoSans-Regular — better Unicode coverage than DejaVu.
        _ => "NotoSans-Regular.ttf"
    };

    private static byte[] LoadEmbeddedFont(string resourceFileName)
    {
        var asm = typeof(FontCache).Assembly;
        var resourceName = $"Unchained.Pdf.Rendering.Rendering.Fonts.{resourceFileName}";
        using var stream = asm.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException(
                               $"Bundled font resource '{resourceName}' not found. " +
                               "Ensure the font files are included as EmbeddedResource in the project.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);

        return ms.ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        foreach (var (ftFace, hbFont) in _fonts.Values)
        {
            hbFont.Dispose();
            ftFace.Dispose();
        }

        _fonts.Clear();
        _ftLibrary.Dispose();
    }
}
