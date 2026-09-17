using Shouldly;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Rendering.Engine;
using Xunit;

namespace Unchained.Pdf.Rendering.Tests.UnitTests.Engine;

public sealed class PdfiumPdfRendererTests
{
    private static readonly IDocumentProcessor Processor = new DocumentProcessor();

    private static async Task<IPdfDocument> LoadTestDocumentAsync()
    {
        // ReSharper disable BadListLineBreaks
        var testPdfPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Unchained.Pdf.Tests", "TestFiles", "simple.pdf");
        // ReSharper restore BadListLineBreaks
        if (!File.Exists(testPdfPath))
            testPdfPath = Path.Combine(AppContext.BaseDirectory, "TestFiles", "simple.pdf");

        var bytes = await File.ReadAllBytesAsync(testPdfPath);
        return await Processor.LoadAsync(new MemoryStream(bytes), CancellationToken.None);
    }

    [Fact]
    public async Task RenderPageAsync_WithValidPdf_RendersPage()
    {
        await using var doc = await LoadTestDocumentAsync();
        using var renderer = new PdfiumPdfRenderer();
        var page = doc.Pages[1];

        var result = await renderer.RenderPageAsync(page, new RenderOptions { Dpi = 96 }, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task RenderPageAsync_WithHighDpi_ProducesLargerImage()
    {
        await using var doc = await LoadTestDocumentAsync();
        using var renderer = new PdfiumPdfRenderer();
        var page = doc.Pages[1];

        var result72 = await renderer.RenderPageAsync(page, new RenderOptions { Dpi = 72 }, TestContext.Current.CancellationToken);
        var result144 = await renderer.RenderPageAsync(page, new RenderOptions { Dpi = 144 }, TestContext.Current.CancellationToken);

        result144.Length.ShouldBeGreaterThan(result72.Length);
    }

    [Fact]
    public async Task RenderPageAsync_ProducesPngOutput()
    {
        await using var doc = await LoadTestDocumentAsync();
        using var renderer = new PdfiumPdfRenderer();
        var page = doc.Pages[1];

        var result = await renderer.RenderPageAsync(page, new RenderOptions { Dpi = 96 }, TestContext.Current.CancellationToken);

        result.Length.ShouldBeGreaterThan(8);
        result[0].ShouldBe((byte)0x89);
        result[1].ShouldBe((byte)'P');
        result[2].ShouldBe((byte)'N');
        result[3].ShouldBe((byte)'G');
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var renderer = new PdfiumPdfRenderer();
        renderer.Dispose();
        renderer.Dispose();
    }

    [Fact]
    public async Task RenderPageAsync_WithCancellationToken_CanBeCancelled()
    {
        await using var doc = await LoadTestDocumentAsync();
        using var renderer = new PdfiumPdfRenderer();
        using var cts = new CancellationTokenSource();
        var page = doc.Pages[1];

        await cts.CancelAsync();

        await Should.ThrowAsync<TaskCanceledException>(async () => await renderer.RenderPageAsync(page, new RenderOptions { Dpi = 96 }, cts.Token));
    }

    [Fact]
    public async Task RenderPageAsync_MultiplePages_RendersCorrectly()
    {
        await using var doc = await LoadTestDocumentAsync();
        using var renderer = new PdfiumPdfRenderer();

        for (var i = 1; i <= doc.PageCount; i++)
        {
            var page = doc.Pages[i];
            var result = await renderer.RenderPageAsync(page, new RenderOptions { Dpi = 96 }, TestContext.Current.CancellationToken);
            result.ShouldNotBeNull();
            result.ShouldNotBeEmpty();
        }
    }

    [Fact]
    public async Task RenderPageAsync_WithLowDpi_ProducesValidImage()
    {
        await using var doc = await LoadTestDocumentAsync();
        using var renderer = new PdfiumPdfRenderer();
        var page = doc.Pages[1];

        var result = await renderer.RenderPageAsync(page, new RenderOptions { Dpi = 36 }, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task RenderPageAsync_WithHighDpi_ProducesValidImage()
    {
        await using var doc = await LoadTestDocumentAsync();
        using var renderer = new PdfiumPdfRenderer();
        var page = doc.Pages[1];

        var result = await renderer.RenderPageAsync(page, new RenderOptions { Dpi = 300 }, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task RenderDocumentAsync_ThrowsNotImplementedException()
    {
        await using var doc = await LoadTestDocumentAsync();
        using var renderer = new PdfiumPdfRenderer();

        await Should.ThrowAsync<NotImplementedException>(async () => await renderer.RenderDocumentAsync(doc, new RenderOptions { Dpi = 96 }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RenderPageAsync_WithVeryLowDpi_ProducesValidImage()
    {
        await using var doc = await LoadTestDocumentAsync();
        using var renderer = new PdfiumPdfRenderer();
        var page = doc.Pages[1];

        var result = await renderer.RenderPageAsync(page, new RenderOptions { Dpi = 1 }, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result[0].ShouldBe((byte)0x89);
        result[1].ShouldBe((byte)'P');
    }

    [Fact]
    public async Task RenderPageAsync_WithVeryHighDpi_ProducesValidImage()
    {
        await using var doc = await LoadTestDocumentAsync();
        using var renderer = new PdfiumPdfRenderer();
        var page = doc.Pages[1];

        var result = await renderer.RenderPageAsync(page, new RenderOptions { Dpi = 600 }, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result[0].ShouldBe((byte)0x89);
    }

    [Fact]
    public async Task RenderPageAsync_MultipleConcurrentRenders_WorksCorrectly()
    {
        await using var doc = await LoadTestDocumentAsync();
        using var renderer = new PdfiumPdfRenderer();
        var page = doc.Pages[1];

        var tasks = Enumerable.Range(0, 5)
            .Select(_ => renderer.RenderPageAsync(page, new RenderOptions { Dpi = 96 }, TestContext.Current.CancellationToken))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.ShouldAllBe(static r => r.Length > 0);
    }

    [Fact]
    public async Task RenderPageAsync_SamePageMultipleTimes_ProducesSameOutput()
    {
        await using var doc = await LoadTestDocumentAsync();
        using var renderer = new PdfiumPdfRenderer();
        var page = doc.Pages[1];

        var result1 = await renderer.RenderPageAsync(page, new RenderOptions { Dpi = 96 }, TestContext.Current.CancellationToken);
        var result2 = await renderer.RenderPageAsync(page, new RenderOptions { Dpi = 96 }, TestContext.Current.CancellationToken);

        result1.Length.ShouldBe(result2.Length);
    }

    [Fact]
    public async Task RenderPageAsync_DifferentDpiValues_ProduceDifferentSizes()
    {
        await using var doc = await LoadTestDocumentAsync();
        using var renderer = new PdfiumPdfRenderer();
        var page = doc.Pages[1];

        var result96 = await renderer.RenderPageAsync(page, new RenderOptions { Dpi = 96 }, TestContext.Current.CancellationToken);
        var result192 = await renderer.RenderPageAsync(page, new RenderOptions { Dpi = 192 }, TestContext.Current.CancellationToken);
        var result288 = await renderer.RenderPageAsync(page, new RenderOptions { Dpi = 288 }, TestContext.Current.CancellationToken);

        result192.Length.ShouldBeGreaterThan(result96.Length);
        result288.Length.ShouldBeGreaterThan(result192.Length);
    }

    [Fact]
    public async Task RenderPageAsync_ProducesValidPngStructure()
    {
        await using var doc = await LoadTestDocumentAsync();
        using var renderer = new PdfiumPdfRenderer();
        var page = doc.Pages[1];

        var result = await renderer.RenderPageAsync(page, new RenderOptions { Dpi = 96 }, TestContext.Current.CancellationToken);

        result.Length.ShouldBeGreaterThan(8);
        result[0].ShouldBe((byte)0x89);
        result[1].ShouldBe((byte)'P');
        result[2].ShouldBe((byte)'N');
        result[3].ShouldBe((byte)'G');
        result[4].ShouldBe((byte)0x0D);
        result[5].ShouldBe((byte)0x0A);
        result[6].ShouldBe((byte)0x1A);
        result[7].ShouldBe((byte)0x0A);
    }

    [Fact]
    public void Dispose_MultipleInstancesCanBeDisposed()
    {
        var renderer1 = new PdfiumPdfRenderer();
        var renderer2 = new PdfiumPdfRenderer();

        renderer1.Dispose();
        renderer2.Dispose();
    }

    [Fact]
    public async Task RenderPageAsync_AfterDispose_StillWorks()
    {
        await using var doc = await LoadTestDocumentAsync();
        var renderer = new PdfiumPdfRenderer();
        renderer.Dispose();

        var page = doc.Pages[1];
        var result = await renderer.RenderPageAsync(page, new RenderOptions { Dpi = 96 }, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
    }
}
