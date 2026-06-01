using Unchained.Pdf.Rendering.Engine;

namespace Unchained.Pdf.Tests.Helpers;

/// <summary>
/// Base for integration tests that exercise <see cref="PdfRenderer"/>.
/// Initialises FreeType2 once per test class and makes the renderer available
/// as a protected property. Tests that require FreeType2 should guard with
/// <c>if (!FreeTypeAvailable) return;</c>.
/// </summary>
public abstract class RendererTestBase : PdfTestBase, IDisposable
{
    protected readonly PdfRenderer? Renderer;
    protected readonly bool FreeTypeAvailable;

    protected RendererTestBase()
    {
        try
        {
            Renderer = new PdfRenderer();
            FreeTypeAvailable = true;
        }
        catch
        {
            FreeTypeAvailable = false;
        }
    }

    public void Dispose() => Renderer?.Dispose();
}
