using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Rendering.Tests.Helpers;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Rendering.Tests.IntegrationTests.RealPdf;

/// <summary>
///     Rendering tests against real-world PDFs.
///     Requires FreeType2 at runtime; the renderer construction throws (failing the test) if it is absent.
///     Smoke tests loop over every *.pdf in the folder and pass vacuously when empty.
/// </summary>
public sealed class RealPdfRenderingTests : RendererTestBase
{
    // ── Smoke — render first page of every PDF ────────────────────────────────

    [Fact]
    public async Task Render_AllRealPdfs_FirstPage_ProducesValidPng()
    {
        var tested = 0;
        foreach (var path in Files())
        {
            await using var doc = await TryLoadDocAsync(
                await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken
            );
            if (doc is null)
                continue;

            var png = await Renderer.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
            png[..8].ShouldBe(PdfTestConstants.PngSignature, path);
            tested++;
        }

        tested.ShouldBeGreaterThan(0, "No parseable PDF files found in TestFiles/.");
    }

    [Fact]
    public async Task Render_AllRealPdfs_FirstPage_DimensionsMatchPageSize()
    {
        var tested = 0;
        foreach (var path in Files())
        {
            await using var doc = await TryLoadDocAsync(
                await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken
            );
            if (doc is null)
                continue;

            var page = doc.Pages[1];
            if (page.Width <= 0 || page.Height <= 0)
                continue;

            var png = await Renderer.RenderPageAsync(page, new RenderOptions(72), TestContext.Current.CancellationToken);
            PdfTestConstants.PngWidth(png).ShouldBeInRange((int)page.Width - 2, (int)Math.Ceiling(page.Width) + 2, path);
            PdfTestConstants.PngHeight(png).ShouldBeInRange((int)page.Height - 2, (int)Math.Ceiling(page.Height) + 2, path);
            tested++;
        }

        tested.ShouldBeGreaterThan(0, "No parseable PDF files found in TestFiles/.");
    }

    // ── specific files ────────────────────────────────────────────────────────

    [Fact]
    public async Task Render_Simple_ProducesNonWhitePng()
    {
        var bytes = RealPdfFixtures.Load(RealPdfFixtures.Files.Simple);

        await using var doc = await LoadAsync(bytes, TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        // A page with text should contain at least one non-white pixel.
        png.Skip(8)
            .Any(static b => b != 0xFF)
            .ShouldBeTrue("Expected at least one non-white pixel.");
    }

    [Fact]
    public async Task Render_Multipage_AllPagesProduceValidPng()
    {
        var bytes = RealPdfFixtures.Load(RealPdfFixtures.Files.Multipage);

        await using var doc = await LoadAsync(bytes, TestContext.Current.CancellationToken);
        var pages = await Renderer.RenderDocumentAsync(doc, RenderOptions.Default, TestContext.Current.CancellationToken);
        pages.Count.ShouldBe(doc.PageCount);
        foreach (var png in pages)
            png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    [Fact]
    public async Task Render_WithImages_OutputIsSubstantialSize()
    {
        var bytes = RealPdfFixtures.Load(RealPdfFixtures.Files.WithImages);

        await using var doc = await LoadAsync(bytes, TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], new RenderOptions(), TestContext.Current.CancellationToken);
        png.Length.ShouldBeGreaterThan(
            5_000,
            "Expected a substantial PNG for a page containing images."
        );
    }

    [Fact]
    public async Task Render_WithEmbeddedFonts_ProducesValidPng()
    {
        var bytes = RealPdfFixtures.Load(RealPdfFixtures.Files.WithEmbeddedFonts);

        await using var doc = await LoadAsync(bytes, TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    [Fact]
    public async Task Render_Large_FirstPage_ProducesValidPng()
    {
        var bytes = RealPdfFixtures.Load(RealPdfFixtures.Files.Large);

        await using var doc = await LoadAsync(bytes, TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], new RenderOptions(72), TestContext.Current.CancellationToken);
        png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    [Fact]
    public async Task Render_Large_RenderDocumentAsync_RespectsCancellation()
    {
        var bytes = RealPdfFixtures.Load(RealPdfFixtures.Files.Large);

        // Cancel after 50 ms — short enough to fire during rendering on any machine,
        // long enough for the render task to have started.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await using var doc = await LoadAsync(bytes, TestContext.Current.CancellationToken);
        await Should.ThrowAsync<OperationCanceledException>(() => Renderer.RenderDocumentAsync(doc, new RenderOptions(72), cts.Token));
    }
}
