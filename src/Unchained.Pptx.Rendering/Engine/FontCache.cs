using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HarfBuzzSharp;
using FtFace = SharpFont.Face;
using FtLibrary = SharpFont.Library;

namespace Unchained.Pptx.Rendering.Engine;

/// <summary>
/// Owns the FreeType2 library handle, one HarfBuzz font per loaded typeface,
/// and the corresponding raw font bytes used to create both.
/// Unrecognised fonts fall back to bundled NotoSans-Regular (SIL OFL).
/// </summary>
internal sealed class FontCache : IDisposable
{
    private readonly FtLibrary _ftLibrary;

    // Each entry: FreeType2 Face (for rasterisation) + HarfBuzz Font (for shaping).
    private readonly Dictionary<string, (FtFace FtFace, Font HbFont)> _fonts =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    private static int _resolverRegistered;

    // ModuleInitializer runs when the Unchained.Pptx.Rendering assembly is first loaded —
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

        // 1. runtimes/{rid}/native/ under the output root (NuGet package convention).
        var runtimesPath = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", fileName);
        if (NativeLibrary.TryLoad(runtimesPath, out var h1)) return h1;

        // 2. Output root directly (flattened copy from Unchained.Pdf.Runtimes).
        var rootPath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (NativeLibrary.TryLoad(rootPath, out var h2)) return h2;

        // 3. System-installed FreeType (e.g. apt/yum on Linux).
        foreach (var name in systemFallbacks)
        {
            if (NativeLibrary.TryLoad(name, assembly, searchPath, out var h3))
                return h3;
        }

        return nint.Zero;
    }

    internal FontCache() => _ftLibrary = new FtLibrary();

    /// <summary>
    /// Returns the FreeType2 face and HarfBuzz font for the given font name.
    /// When <paramref name="embeddedBytes"/> are provided they are used directly;
    /// otherwise the closest bundled substitute font is selected.
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

    // Convenience overload — returns only the FreeType2 face.
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
            // SlideRasterizer rescales to pixel units via SetScale before each shaping call.
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

    // Priority: known OOXML theme font names → DejaVu variant → NotoSans (broadest Unicode coverage).
    private static string SelectResourceName(string fontName) => fontName switch
    {
        "Calibri Bold" or "Arial Bold" or "Helvetica-Bold" or "Helvetica-BoldOblique"
            => "DejaVuSans-Bold.ttf",
        "Calibri Italic" or "Arial Italic" or "Helvetica-Oblique"
            => "DejaVuSans-Oblique.ttf",
        "Calibri" or "Arial" or "Helvetica" or "Helvetica-Regular"
            => "DejaVuSans-Regular.ttf",
        "Times New Roman Bold" or "Times-Bold" or "Times-BoldItalic"
            => "DejaVuSerif-Bold.ttf",
        "Times New Roman" or "Times-Roman" or "Times-Italic"
            => "DejaVuSerif-Regular.ttf",
        "Courier New" or "Courier" or "Courier-Bold" or "Courier-Oblique" or "Courier-BoldOblique"
            => "DejaVuSansMono-Regular.ttf",
        // All other fonts fall back to NotoSans-Regular — better Unicode coverage than DejaVu.
        _ => "NotoSans-Regular.ttf"
    };

    private static byte[] LoadEmbeddedFont(string resourceFileName)
    {
        var asm = typeof(FontCache).Assembly;
        var resourceName = $"Unchained.Pptx.Rendering.Rendering.Fonts.{resourceFileName}";
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
