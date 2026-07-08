namespace Unchained.Drawing.Text.Tests;

/// <summary>
///     Loads the DejaVu / Noto TrueType fonts embedded as resources in the
///     <c>Unchained.Drawing.Text</c> assembly, so the glyph tests have a real,
///     deterministic font to exercise FreeType2 without depending on system-installed fonts.
/// </summary>
internal static class BundledFonts
{
    /// <summary>Reads the bundled DejaVuSans-Regular.ttf bytes from the Drawing.Text assembly.</summary>
    internal static byte[] DejaVuSansRegular() =>
        Load("Unchained.Drawing.Text.Fonts.DejaVuSans-Regular.ttf");

    private static byte[] Load(string resourceName)
    {
        var asm = typeof(FontCache).Assembly;
        using var stream = asm.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException(
                               $"Bundled font resource '{resourceName}' not found in '{asm.GetName().Name}'."
                           );
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
