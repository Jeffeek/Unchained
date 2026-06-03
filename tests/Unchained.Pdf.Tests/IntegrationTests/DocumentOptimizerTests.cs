using Shouldly;
using Unchained.Pdf.Engine;
using Xunit;
using Unchained.Pdf.Tests.Helpers;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class DocumentOptimizerTests : PdfTestBase
{
    private static readonly DocumentOptimizer Optimizer = new();


    // ── OptimizeAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task OptimizeAsync_DocumentStillParseable()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithTextContent(text: "Optimize me"));
        await Optimizer.OptimizeAsync(doc, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task OptimizeAsync_ContentOperatorsPreserved()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithTextContent(text: "Hello"));
        await Optimizer.OptimizeAsync(doc, ct: TestContext.Current.CancellationToken);
        doc.Pages[1].GetContentOperators().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task OptimizeAsync_RoundTrip_ParseableAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(count: 3));
        await Optimizer.OptimizeAsync(doc, ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms);
        reloaded.PageCount.ShouldBe(3);
    }

    [Fact]
    public async Task OptimizeAsync_Cancellation_ThrowsOperationCanceledException()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Optimizer.OptimizeAsync(doc, cts.Token));
    }

    // ── OptimizeResourcesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task OptimizeResourcesAsync_DocumentStillParseable()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(count: 2));
        await Optimizer.OptimizeResourcesAsync(doc, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(2);
    }

    [Fact]
    public async Task OptimizeResourcesAsync_RoundTrip_ParseableAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(count: 2));
        await Optimizer.OptimizeResourcesAsync(doc, ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms);
        reloaded.PageCount.ShouldBe(2);
    }
}
