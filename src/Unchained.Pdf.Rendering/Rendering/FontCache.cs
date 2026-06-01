using System.Reflection;
using System.Runtime.InteropServices;
using SharpFont;

namespace Unchained.Pdf.Rendering.Rendering;

/// <summary>
/// Owns the FreeType2 library handle and caches one <see cref="Face"/> per font name.
/// Standard 14 fonts are substituted with bundled DejaVu fonts (Bitstream Vera / SIL OFL).
/// </summary>
internal sealed class FontCache : IDisposable
{
    private readonly Library _library;
    private readonly Dictionary<string, Face> _faces = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    // SharpFont uses DllImport("freetype6") on all platforms, but the system library is
    // named differently on Linux (libfreetype.so.6) and macOS (libfreetype.6.dylib).
    // This resolver bridges that gap for .NET 5+, where Mono's dll map is not used.
    static FontCache() => NativeLibrary.SetDllImportResolver(typeof(Library).Assembly, ResolveFreetype);

    private static nint ResolveFreetype(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != "freetype6") return nint.Zero;

        // Platform-specific candidate names (system or bundled in output directory).
        string[] candidates;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            candidates = ["libfreetype.so.6", "libfreetype.so"];
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            candidates = ["libfreetype.6.dylib", "libfreetype.dylib"];
        else
            return nint.Zero; // Windows: freetype6.dll found by default DllImport resolution

        foreach (var name in candidates)
        {
            if (NativeLibrary.TryLoad(name, assembly, searchPath, out var handle))
                return handle;
        }

        return nint.Zero;
    }

    internal FontCache() => _library = new Library();

    /// <summary>
    /// Returns a FreeType2 <see cref="Face"/> for the named font.
    /// If <paramref name="embeddedBytes"/> is provided (embedded font from PDF), it is used.
    /// Otherwise, falls back to the bundled DejaVu substitute.
    /// </summary>
    internal Face GetFace(string fontName, byte[]? embeddedBytes = null)
    {
        if (_faces.TryGetValue(fontName, out var cached)) return cached;

        Face face;
        if (embeddedBytes is { Length: > 0 })
            face = _library.NewMemoryFace(embeddedBytes, 0);
        else
        {
            var resourceBytes = LoadSubstituteFont(fontName);
            face = _library.NewMemoryFace(resourceBytes, 0);
        }

        _faces[fontName] = face;

        return face;
    }

    private static byte[] LoadSubstituteFont(string fontName) =>
        LoadEmbeddedFont(SelectResourceName(fontName));

    // Maps Standard 14 font names to bundled DejaVu resource names.
    private static string SelectResourceName(string fontName) => fontName switch
    {
        "Helvetica-Bold" or "Helvetica-BoldOblique" => "DejaVuSans-Bold.ttf",
        "Helvetica-Oblique" => "DejaVuSans-Oblique.ttf",
        "Helvetica" or "Helvetica-Regular" => "DejaVuSans-Regular.ttf",
        "Times-Bold" or "Times-BoldItalic" => "DejaVuSerif-Bold.ttf",
        "Times-Roman" or "Times-Italic" => "DejaVuSerif-Regular.ttf",
        "Courier" or "Courier-Bold"
            or "Courier-Oblique" or "Courier-BoldOblique"
            => "DejaVuSansMono-Regular.ttf",
        _ => "DejaVuSans-Regular.ttf"
    };

    private static byte[] LoadEmbeddedFont(string resourceFileName)
    {
        var asm = typeof(FontCache).Assembly;
        // Embedded resource name: Unchained.Pdf.Rendering.Rendering.Fonts.<filename>
        // (default namespace = assembly name = Unchained.Pdf.Rendering, folder path = Rendering/Fonts/)
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
        if (_disposed)
            return;

        _disposed = true;
        foreach (var face in _faces.Values)
            face.Dispose();

        _faces.Clear();
        _library.Dispose();
    }
}
