using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Xunit;
using Unchained.Pdf.Tests.Helpers;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class StampApplierTests : PdfTestBase
{
    private static readonly StampApplier Applier = new();

    private static readonly TextStamp DefaultStamp = new("DRAFT", X: 100, Y: 400);


    // ── StampAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task StampAsync_SinglePage_PageCountUnchanged()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, DefaultStamp, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task StampAsync_SinglePage_ContentStreamContainsBtTj()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, DefaultStamp, ct: TestContext.Current.CancellationToken);
        var ops = doc.Pages[1].GetContentOperators();
        ops.ShouldContain(static op => op.Name == "BT");
        ops.ShouldContain(static op => op.Name == "Tj");
    }

    [Fact]
    public async Task StampAsync_SinglePage_ContentStreamContainsQWrapper()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, DefaultStamp, ct: TestContext.Current.CancellationToken);
        var ops = doc.Pages[1].GetContentOperators();
        ops.ShouldContain(static op => op.Name == "q");
        ops.ShouldContain(static op => op.Name == "Q");
    }

    [Fact]
    public async Task StampAsync_MultiPage_AllPagesStamped()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(count: 3), ct: TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, DefaultStamp, ct: TestContext.Current.CancellationToken);
        for (var i = 1; i <= doc.PageCount; i++)
            doc.Pages[i].GetContentOperators().ShouldContain(static op => op.Name == "Tj");
    }

    [Fact]
    public async Task StampAsync_RoundTrip_DocumentParseableAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, DefaultStamp, ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, ct: TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task StampAsync_StampTextAppearsInExtractedText()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, new TextStamp("CONFIDENTIAL", X: 100, Y: 400), ct: TestContext.Current.CancellationToken);
        var text = doc.Pages[1].ExtractText();
        text.ShouldContain("CONFIDENTIAL");
    }

    [Fact]
    public async Task StampAsync_IsBackground_PrependedToContents()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithTextContent(text: "original"), ct: TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, DefaultStamp with { IsBackground = true }, ct: TestContext.Current.CancellationToken);
        // Both operators should be present (original content + stamp)
        var ops = doc.Pages[1].GetContentOperators();
        ops.ShouldContain(static op => op.Name == "q");
        ops.ShouldContain(static op => op.Name == "Tj");
    }

    [Fact]
    public async Task StampAsync_Cancellation_ThrowsOperationCanceledException()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Applier.StampAsync(doc, DefaultStamp, cts.Token));
    }

    // ── StampPageAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task StampPageAsync_SingleTargetPage_OnlyThatPageStamped()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(count: 3), ct: TestContext.Current.CancellationToken);
        await Applier.StampPageAsync(doc, pageNumber: 2, DefaultStamp, ct: TestContext.Current.CancellationToken);
        doc.Pages[1].GetContentOperators().ShouldNotContain(static op => op.Name == "Tj");
        doc.Pages[2].GetContentOperators().ShouldContain(static op => op.Name == "Tj");
        doc.Pages[3].GetContentOperators().ShouldNotContain(static op => op.Name == "Tj");
    }

    [Fact]
    public async Task StampPageAsync_RoundTrip_DocumentParseableAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(count: 2), ct: TestContext.Current.CancellationToken);
        await Applier.StampPageAsync(doc, pageNumber: 1, DefaultStamp, ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, ct: TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(2);
    }
}
