using Shouldly;
using Unchained.Pdf.Engine;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class DocumentOptimizerTests
{
    private static readonly DocumentProcessor Processor = new();
    private static readonly DocumentOptimizer Optimizer = new();

    private static Task<Abstractions.IPdfDocument> LoadAsync(byte[] b) =>
        Processor.LoadAsync(new MemoryStream(b));

    // ── OptimizeAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task OptimizeAsync_DocumentStillParseable()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.WithTextContent(text: "Optimize me"));
        await Optimizer.OptimizeAsync(doc);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task OptimizeAsync_ContentOperatorsPreserved()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.WithTextContent(text: "Hello"));
        await Optimizer.OptimizeAsync(doc);
        doc.Pages[1].GetContentOperators().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task OptimizeAsync_RoundTrip_ParseableAfterSave()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.MultiPage(count: 3));
        await Optimizer.OptimizeAsync(doc);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);
        ms.Position = 0;
        await using var reloaded = await Processor.LoadAsync(ms);
        reloaded.PageCount.ShouldBe(3);
    }

    [Fact]
    public async Task OptimizeAsync_Cancellation_ThrowsOperationCanceledException()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.SinglePage());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Optimizer.OptimizeAsync(doc, cts.Token));
    }

    // ── OptimizeResourcesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task OptimizeResourcesAsync_DocumentStillParseable()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.MultiPage(count: 2));
        await Optimizer.OptimizeResourcesAsync(doc);
        doc.PageCount.ShouldBe(2);
    }

    [Fact]
    public async Task OptimizeResourcesAsync_RoundTrip_ParseableAfterSave()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.MultiPage(count: 2));
        await Optimizer.OptimizeResourcesAsync(doc);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);
        ms.Position = 0;
        await using var reloaded = await Processor.LoadAsync(ms);
        reloaded.PageCount.ShouldBe(2);
    }
}
