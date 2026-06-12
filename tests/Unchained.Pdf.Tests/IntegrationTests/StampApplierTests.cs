using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class StampApplierTests : PdfTestBase
{
    private static readonly StampApplier Applier = new();

    private static readonly TextStamp DefaultStamp = new("DRAFT", 100, 400);


    // ── StampAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task StampAsync_SinglePage_PageCountUnchanged()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, DefaultStamp, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task StampAsync_SinglePage_ContentStreamContainsBtTj()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, DefaultStamp, TestContext.Current.CancellationToken);
        var ops = doc.Pages[1].GetContentOperators();
        ops.ShouldContain(static op => op.Name == "BT");
        ops.ShouldContain(static op => op.Name == "Tj");
    }

    [Fact]
    public async Task StampAsync_SinglePage_ContentStreamContainsQWrapper()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, DefaultStamp, TestContext.Current.CancellationToken);
        var ops = doc.Pages[1].GetContentOperators();
        ops.ShouldContain(static op => op.Name == "q");
        ops.ShouldContain(static op => op.Name == "Q");
    }

    [Fact]
    public async Task StampAsync_MultiPage_AllPagesStamped()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, DefaultStamp, TestContext.Current.CancellationToken);
        for (var i = 1; i <= doc.PageCount; i++)
            doc.Pages[i].GetContentOperators().ShouldContain(static op => op.Name == "Tj");
    }

    [Fact]
    public async Task StampAsync_RoundTrip_DocumentParseableAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, DefaultStamp, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task StampAsync_StampTextAppearsInExtractedText()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, new TextStamp("CONFIDENTIAL", 100, 400), TestContext.Current.CancellationToken);
        var text = doc.Pages[1].ExtractText();
        text.ShouldContain("CONFIDENTIAL");
    }

    [Fact]
    public async Task StampAsync_IsBackground_PrependedToContents()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithTextContent("original"), TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, DefaultStamp with { IsBackground = true }, TestContext.Current.CancellationToken);
        // Both operators should be present (original content + stamp)
        var ops = doc.Pages[1].GetContentOperators();
        ops.ShouldContain(static op => op.Name == "q");
        ops.ShouldContain(static op => op.Name == "Tj");
    }

    [Fact]
    public async Task StampAsync_Cancellation_ThrowsOperationCanceledException()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Applier.StampAsync(doc, DefaultStamp, cts.Token));
    }

    // ── StampPageAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task StampPageAsync_SingleTargetPage_OnlyThatPageStamped()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);
        await Applier.StampPageAsync(doc, 2, DefaultStamp, TestContext.Current.CancellationToken);
        doc.Pages[1].GetContentOperators().ShouldNotContain(static op => op.Name == "Tj");
        doc.Pages[2].GetContentOperators().ShouldContain(static op => op.Name == "Tj");
        doc.Pages[3].GetContentOperators().ShouldNotContain(static op => op.Name == "Tj");
    }

    [Fact]
    public async Task StampPageAsync_RoundTrip_DocumentParseableAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);
        await Applier.StampPageAsync(doc, 1, DefaultStamp, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(2);
    }
}
