using Shouldly;
using Unchained.Drawing.Constants;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Rendering.Proprietary.Engine;
using Unchained.Pdf.Rendering.Tests.Helpers;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Rendering.Tests.IntegrationTests;

/// <summary>
///     Integration tests for <see cref="UnchainedPdfRenderer" />.
///     Requires FreeType2 at runtime; the renderer construction throws (failing the test) if it is absent.
/// </summary>
public sealed class RendererTests : RendererTestBase
{
    // ── PNG magic bytes ───────────────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_SinglePage_StartsWithPngSignature()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    [Fact]
    public async Task RenderPage_WithTextContent_StartsWithPngSignature()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithTextContent("Hello"), TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    // ── Dimensions ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_150Dpi_WidthApproximatesExpected()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var page = doc.Pages[1];
        var png = await Renderer.RenderPageAsync(doc.Pages[1], new RenderOptions(), TestContext.Current.CancellationToken);
        // Expected pixel width ≈ pageWidthPt * 150 / 72. The renderer truncates the
        // point×scale product to match common rasterizers (e.g. Pdfium); allow ±1 px so
        // the test honours the "approximates" contract regardless of the rounding mode.
        var expected = page.Width * 150.0 / 72.0;
        var width = PdfTestConstants.PngWidth(png);
        width.ShouldBeInRange((int)Math.Floor(expected) - 1, (int)Math.Ceiling(expected) + 1);
    }

    [Fact]
    public async Task RenderPage_300Dpi_LargerThan150Dpi()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var png150 = await Renderer.RenderPageAsync(doc.Pages[1], new RenderOptions(), TestContext.Current.CancellationToken);
        var png300 = await Renderer.RenderPageAsync(doc.Pages[1], new RenderOptions(300), TestContext.Current.CancellationToken);
        var w150 = PdfTestConstants.PngWidth(png150);
        var w300 = PdfTestConstants.PngWidth(png300);
        w300.ShouldBeGreaterThan(w150);
    }

    // ── Table-generated documents ─────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_TableDocument_ProducesPng()
    {
        var gen = new TableGenerator();
        var data = new TableData
        {
            Headers = ["Name", "Value"],
            Rows = [["Alice", "42"], ["Bob", "17"]]
        };
        await using var doc = await gen.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    // ── Multi-page ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderDocumentAsync_MultiPage_ReturnsOnePerPage()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);
        var pages = await Renderer.RenderDocumentAsync(doc, RenderOptions.Default, TestContext.Current.CancellationToken);
        pages.Count.ShouldBe(3);
        foreach (var p in pages)
            p[..8].ShouldBe(PdfTestConstants.PngSignature);
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_Cancellation_ThrowsOperationCanceledException()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Renderer.RenderPageAsync(doc.Pages[1], RenderOptions.Default, cts.Token));
    }

    // ── Output size sanity ────────────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_ProducesNonEmptyPng()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithTextContent(), TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png.Length.ShouldBeGreaterThan(100);
    }

    // ── Output formats ────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_JpegFormat_ProducesJpegBytes()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var jpeg = await Renderer.RenderPageAsync(
            doc.Pages[1],
            new RenderOptions(72, OutputFormat.Jpeg),
            TestContext.Current.CancellationToken
        );

        // JPEG starts with FF D8 FF
        jpeg.Length.ShouldBeGreaterThan(3);
        jpeg[0].ShouldBe(JpegConstants.MarkerPrefix);
        jpeg[1].ShouldBe(JpegConstants.Soi);
        jpeg[2].ShouldBe(JpegConstants.MarkerPrefix);
    }

    [Fact]
    public async Task RenderPage_BmpFormat_ProducesBmpBytes()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var bmp = await Renderer.RenderPageAsync(
            doc.Pages[1],
            new RenderOptions(72, OutputFormat.Bmp),
            TestContext.Current.CancellationToken
        );

        // BMP starts with 'BM'
        bmp.Length.ShouldBeGreaterThan(2);
        bmp[0].ShouldBe((byte)'B');
        bmp[1].ShouldBe((byte)'M');
    }

    [Fact]
    public async Task RenderPage_JpegQuality_LowerQualityProducesSmallerFile()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithTextContent(), TestContext.Current.CancellationToken);

        var highQ = await Renderer.RenderPageAsync(
            doc.Pages[1],
            new RenderOptions(72, OutputFormat.Jpeg, 95),
            TestContext.Current.CancellationToken
        );
        var lowQ = await Renderer.RenderPageAsync(
            doc.Pages[1],
            new RenderOptions(72, OutputFormat.Jpeg, 10),
            TestContext.Current.CancellationToken
        );

        lowQ.Length.ShouldBeLessThan(
            highQ.Length,
            "Lower JPEG quality should produce a smaller file"
        );
    }

    // ── Page rotation ─────────────────────────────────────────────────────────

    private static byte[] RotatedPage(int rotate)
    {
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Rotate {rotate} /Contents 4 0 R >>",
            "<< /Length 15 >>\nstream\n0 0 50 50 re f\nendstream"
        };
        return RawPdfBuilder.Build(bodies);
    }

    [
        Theory,
        InlineData(0),
        InlineData(90),
        InlineData(180),
        InlineData(270)
    ]
    public async Task RenderPage_Rotated_ProducesPngWithSwappedDimensions(int rotate)
    {
        await using var doc = await LoadAsync(RotatedPage(rotate), TestContext.Current.CancellationToken);
        var png = await Renderer.RenderPageAsync(doc.Pages[1], RenderOptions.Default, TestContext.Current.CancellationToken);
        png[..8].ShouldBe(PdfTestConstants.PngSignature);

        var w = PdfTestConstants.PngWidth(png);
        var h = PdfTestConstants.PngHeight(png);
        // MediaBox is 200×100; for 90/270 the canvas dimensions swap.
        if (rotate is 90 or 270)
            w.ShouldBeLessThan(h);
        else
            w.ShouldBeGreaterThan(h);
    }
}
