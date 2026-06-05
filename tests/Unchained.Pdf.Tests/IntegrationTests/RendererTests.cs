using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="Unchained.Pdf.Rendering.Engine.PdfRenderer"/>.
/// Tests call <c>SkipIfNoFreeType()</c> so absent FreeType2 shows as Skipped, not Passed.
/// </summary>
public sealed class RendererTests : RendererTestBase
{
    // ── PNG magic bytes ───────────────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_SinglePage_StartsWithPngSignature()
    {
        SkipIfNoFreeType();

        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default, ct: TestContext.Current.CancellationToken);
        png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    [Fact]
    public async Task RenderPage_WithTextContent_StartsWithPngSignature()
    {
        SkipIfNoFreeType();

        await using var doc = await LoadAsync(PdfFixtures.WithTextContent(text: "Hello"), ct: TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default, ct: TestContext.Current.CancellationToken);
        png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    // ── Dimensions ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_150Dpi_WidthApproximatesExpected()
    {
        SkipIfNoFreeType();

        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        var page = doc.Pages[1];
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 150), ct: TestContext.Current.CancellationToken);
        // Expected pixel width ≈ pageWidthPt * 150 / 72
        var expected = (int)Math.Ceiling(page.Width * 150.0 / 72.0);
        var width = PdfTestConstants.PngWidth(png);
        width.ShouldBe(expected);
    }

    [Fact]
    public async Task RenderPage_300Dpi_LargerThan150Dpi()
    {
        SkipIfNoFreeType();

        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        var png150 = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 150), ct: TestContext.Current.CancellationToken);
        var png300 = await Renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 300), ct: TestContext.Current.CancellationToken);
        var w150 = PdfTestConstants.PngWidth(png150);
        var w300 = PdfTestConstants.PngWidth(png300);
        w300.ShouldBeGreaterThan(w150);
    }

    // ── Table-generated documents ─────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_TableDocument_ProducesPng()
    {
        SkipIfNoFreeType();

        var gen = new TableGenerator();
        var data = new TableData
        {
            Headers = ["Name", "Value"],
            Rows = [["Alice", "42"], ["Bob", "17"]]
        };
        await using var doc = await gen.GenerateAsync(data, TableStyle.Default, ct: TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default, ct: TestContext.Current.CancellationToken);
        png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    // ── Multi-page ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderDocumentAsync_MultiPage_ReturnsOnePerPage()
    {
        SkipIfNoFreeType();

        await using var doc = await LoadAsync(PdfFixtures.MultiPage(count: 3), ct: TestContext.Current.CancellationToken);
        var pages = await Renderer!.RenderDocumentAsync(doc, RenderOptions.Default, ct: TestContext.Current.CancellationToken);
        pages.Count.ShouldBe(3);
        foreach (var p in pages)
            p[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_Cancellation_ThrowsOperationCanceledException()
    {
        SkipIfNoFreeType();

        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default, cts.Token));
    }

    // ── Output size sanity ────────────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_ProducesNonEmptyPng()
    {
        SkipIfNoFreeType();

        await using var doc = await LoadAsync(PdfFixtures.WithTextContent(), ct: TestContext.Current.CancellationToken);
        var png = await Renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default, ct: TestContext.Current.CancellationToken);
        png.Length.ShouldBeGreaterThan(100);
    }
}
