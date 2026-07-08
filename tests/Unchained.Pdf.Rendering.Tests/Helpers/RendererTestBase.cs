using Unchained.Drawing.Text;
using Unchained.Pdf.Rendering.Proprietary.Engine;
using Unchained.Pdf.Tests.Shared;

namespace Unchained.Pdf.Rendering.Tests.Helpers;

/// <summary>
///     Base for integration tests that exercise <see cref="UnchainedPdfRenderer" />.
///     Initialises FreeType2 once per test class and makes the renderer available
///     as a protected property. If the FreeType2 native library cannot be loaded, the
///     constructor throws and the test fails — surfacing the real error rather than hiding it.
/// </summary>
public abstract class RendererTestBase : PdfTestBase, IDisposable
{
    protected readonly UnchainedPdfRenderer Renderer = new();

    public void Dispose() => Renderer.Dispose();

    /// <summary>Loads the embedded DejaVu Sans Regular TrueType font bytes from the Drawing.Text assembly.</summary>
    protected static byte[] LoadDejaVuSansRegular()
    {
        var asm = typeof(FontCache).Assembly;
        using var stream = asm.GetManifestResourceStream(
                               "Unchained.Drawing.Text.Fonts.DejaVuSans-Regular.ttf"
                           )
                           ?? throw new InvalidOperationException("DejaVuSans-Regular not found");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
