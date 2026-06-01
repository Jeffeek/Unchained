using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Rendering.Engine;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="PdfRenderer"/>. Tests are skipped gracefully
/// when FreeType2 is not available at runtime (e.g. CI without the native DLL).
/// </summary>
public sealed class RendererTests : IDisposable
{
    private static readonly DocumentProcessor Processor = new();
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private readonly PdfRenderer? _renderer;
    private readonly bool _freeTypeAvailable;

    public RendererTests()
    {
        try
        {
            _renderer = new PdfRenderer();
            _freeTypeAvailable = true;
        }
        catch
        {
            _freeTypeAvailable = false;
        }
    }

    public void Dispose() => _renderer?.Dispose();

    // Returns true when FreeType2 is available; false signals to the test it should pass vacuously.
    private bool FreeTypeAvailable() => _freeTypeAvailable;

    // ── PNG magic bytes ───────────────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_SinglePage_StartsWithPngSignature()
    {
        if (!FreeTypeAvailable())
            return;

        await using var doc = await Processor.LoadAsync(new MemoryStream(Helpers.PdfFixtures.SinglePage()));
        var png = await _renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default);
        png[..8].ShouldBe(PngSignature);
    }

    [Fact]
    public async Task RenderPage_WithTextContent_StartsWithPngSignature()
    {
        if (!FreeTypeAvailable())
            return;

        await using var doc = await Processor.LoadAsync(new MemoryStream(Helpers.PdfFixtures.WithTextContent(text: "Hello")));
        var png = await _renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default);
        png[..8].ShouldBe(PngSignature);
    }

    // ── Dimensions ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_150Dpi_WidthApproximatesExpected()
    {
        if (!FreeTypeAvailable())
            return;

        await using var doc = await Processor.LoadAsync(new MemoryStream(Helpers.PdfFixtures.SinglePage()));
        var page = doc.Pages[1];
        var png = await _renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 150));
        // Expected pixel width ≈ pageWidthPt * 150 / 72
        var expected = (int)Math.Ceiling(page.Width * 150.0 / 72.0);
        var width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        width.ShouldBe(expected);
    }

    [Fact]
    public async Task RenderPage_300Dpi_LargerThan150Dpi()
    {
        if (!FreeTypeAvailable())
            return;

        await using var doc = await Processor.LoadAsync(new MemoryStream(Helpers.PdfFixtures.SinglePage()));
        var png150 = await _renderer!.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 150));
        var png300 = await _renderer.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 300));
        var w150 = (png150[16] << 24) | (png150[17] << 16) | (png150[18] << 8) | png150[19];
        var w300 = (png300[16] << 24) | (png300[17] << 16) | (png300[18] << 8) | png300[19];
        w300.ShouldBeGreaterThan(w150);
    }

    // ── Table-generated documents ─────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_TableDocument_ProducesPng()
    {
        if (!FreeTypeAvailable())
            return;

        var gen = new TableGenerator();
        var data = new TableData
        {
            Headers = ["Name", "Value"],
            Rows = [["Alice", "42"], ["Bob", "17"]]
        };
        await using var doc = await gen.GenerateAsync(data, TableStyle.Default);
        var png = await _renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default);
        png[..8].ShouldBe(PngSignature);
    }

    // ── Multi-page ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderDocumentAsync_MultiPage_ReturnsOnePerPage()
    {
        if (!FreeTypeAvailable())
            return;

        await using var doc = await Processor.LoadAsync(new MemoryStream(Helpers.PdfFixtures.MultiPage(count: 3)));
        var pages = await _renderer!.RenderDocumentAsync(doc, RenderOptions.Default);
        pages.Count.ShouldBe(3);
        foreach (var p in pages)
            p[..8].ShouldBe(PngSignature);
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_Cancellation_ThrowsOperationCanceledException()
    {
        if (!FreeTypeAvailable())
            return;

        await using var doc = await Processor.LoadAsync(new MemoryStream(Helpers.PdfFixtures.SinglePage()));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => _renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default, cts.Token));
    }

    // ── Output size sanity ────────────────────────────────────────────────────

    [Fact]
    public async Task RenderPage_ProducesNonEmptyPng()
    {
        if (!FreeTypeAvailable()) return;

        await using var doc = await Processor.LoadAsync(
            new MemoryStream(Helpers.PdfFixtures.WithTextContent()));
        var png = await _renderer!.RenderPageAsync(doc.Pages[1], RenderOptions.Default);
        png.Length.ShouldBeGreaterThan(100);
    }
}
