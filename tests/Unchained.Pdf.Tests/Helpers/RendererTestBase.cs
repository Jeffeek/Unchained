using Unchained.Pdf.Rendering.Engine;
using Xunit;

namespace Unchained.Pdf.Tests.Helpers;

/// <summary>
///     Base for integration tests that exercise <see cref="PdfRenderer" />.
///     Initialises FreeType2 once per test class and makes the renderer available
///     as a protected property. Tests that require FreeType2 should guard with
///     <c>if (!FreeTypeAvailable) return;</c>.
/// </summary>
public abstract class RendererTestBase : PdfTestBase, IDisposable
{
    protected readonly bool FreeTypeAvailable;
    protected readonly PdfRenderer? Renderer;

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

    /// <summary>
    ///     Skips the current test (marks it as <em>Skipped</em>, not <em>Passed</em>) when
    ///     FreeType2 is not available at runtime. Use instead of <c>if (!FreeTypeAvailable) return;</c>
    ///     so the test runner correctly distinguishes "not run" from "passed".
    /// </summary>
    protected void SkipIfNoFreeType() =>
        Assert.SkipUnless(FreeTypeAvailable, "FreeType2 native library not available at runtime.");
}
