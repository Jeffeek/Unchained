using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests.RealPdf;

/// <summary>
/// Rendering tests against real-world PDFs.
/// Requires FreeType2 at runtime; tests call <c>SkipIfNoFreeType()</c> so absent FreeType2 shows as Skipped, not Passed.
/// Smoke tests loop over every *.pdf in the folder and pass vacuously when empty.
/// </summary>
public sealed class RealPdfRenderingTests : RendererTestBase
{
    // ── Smoke — render first page of every PDF ────────────────────────────────

    [Fact]
    public async Task Render_AllRealPdfs_FirstPage_ProducesValidPng()
    {
        SkipIfNoFreeType();

        var tested = 0;
        foreach (var path in Files())
        {
            await using var doc = await TryLoadDocAsync(await File.ReadAllBytesAsync(path));
            if (doc is null)
                continue;

            var png = await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default);
            png[..8].ShouldBe(PdfTestConstants.PngSignature, path);
            tested++;
        }

        if (tested == 0)
            Assert.Skip("No parseable PDF files found in TestFiles/.");
    }

    [Fact]
    public async Task Render_AllRealPdfs_FirstPage_DimensionsMatchPageSize()
    {
        SkipIfNoFreeType();

        var tested = 0;
        foreach (var path in Files())
        {
            await using var doc = await TryLoadDocAsync(await File.ReadAllBytesAsync(path));
            if (doc is null)
                continue;

            var page = doc.Pages[1];
            if (page.Width <= 0 || page.Height <= 0)
                continue;

            var png = await Renderer!.RenderPageAsync(page, new RenderOptions(Dpi: 72));
            PdfTestConstants.PngWidth(png).ShouldBeInRange((int)page.Width - 2, (int)Math.Ceiling(page.Width) + 2, path);
            PdfTestConstants.PngHeight(png).ShouldBeInRange((int)page.Height - 2, (int)Math.Ceiling(page.Height) + 2, path);
            tested++;
        }

        if (tested == 0)
            Assert.Skip("No parseable PDF files found in TestFiles/.");
    }

    // ── specific files ────────────────────────────────────────────────────────

    [Fact]
    public async Task Render_Simple_ProducesNonWhitePng()
    {
        SkipIfNoFreeType();

        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Simple);

        await using var doc = await LoadAsync(bytes);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default);
        // A page with text should contain at least one non-white pixel.
        png.Skip(8).Any(static b => b != 0xFF)
            .ShouldBeTrue("Expected at least one non-white pixel.");
    }

    [Fact]
    public async Task Render_Multipage_AllPagesProduceValidPng()
    {
        SkipIfNoFreeType();

        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Multipage);

        await using var doc = await LoadAsync(bytes);
        var pages = await Renderer!.RenderDocumentAsync(doc, RenderOptions.Default);
        pages.Count.ShouldBe(doc.PageCount);
        foreach (var png in pages)
            png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    [Fact]
    public async Task Render_WithImages_OutputIsSubstantialSize()
    {
        SkipIfNoFreeType();

        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.WithImages);

        await using var doc = await LoadAsync(bytes);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 150));
        png.Length.ShouldBeGreaterThan(5_000,
            "Expected a substantial PNG for a page containing images.");
    }

    [Fact]
    public async Task Render_WithEmbeddedFonts_ProducesValidPng()
    {
        SkipIfNoFreeType();

        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.WithEmbeddedFonts);

        await using var doc = await LoadAsync(bytes);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default);
        png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    [Fact]
    public async Task Render_Large_FirstPage_ProducesValidPng()
    {
        SkipIfNoFreeType();

        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Large);

        await using var doc = await LoadAsync(bytes);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 72));
        png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    [Fact]
    public async Task Render_Large_RenderDocumentAsync_RespectsCancellation()
    {
        SkipIfNoFreeType();

        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Large);

        // Cancel after 50 ms — short enough to fire during rendering on any machine,
        // long enough for the render task to have started.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await using var doc = await LoadAsync(bytes);
        await Should.ThrowAsync<OperationCanceledException>(() => Renderer!.RenderDocumentAsync(doc, new RenderOptions(Dpi: 72), cts.Token));
    }
}
