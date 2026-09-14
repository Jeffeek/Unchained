using Shouldly;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class DocumentMergerTests : PdfTestBase
{
    private static readonly DocumentMerger Merger = new();
    private static readonly AnnotationEditor Annotations = new();

    // Convenience wrapper that builds a MultiPage fixture and loads it.
    private static Task<IPdfDocument> LoadFixtureAsync(int pages) =>
        LoadAsync(PdfFixtures.MultiPage(pages));

    // Builds a document whose pages are individually identifiable: each page carries a single
    // annotation whose Contents is its 1-based page number (e.g. "P3"). This lets range-merge
    // tests assert exactly which pages were selected and in what order.
    private static async Task<IPdfDocument> LoadTaggedAsync(int pages)
    {
        var doc = await LoadFixtureAsync(pages);
        for (var page = 1; page <= pages; page++)
            await Annotations.AddAnnotationAsync(doc, page, new Annotation(AnnotationSubtype.Text, 10, 10, 20, 20, $"P{page}"));
        return doc;
    }

    private static IReadOnlyList<string?> PageTags(IPdfDocument doc) =>
        Enumerable.Range(1, doc.PageCount)
            .Select(p => doc.Pages[p].GetAnnotations().SingleOrDefault()?.Contents)
            .ToList();

    // ── IReadOnlyList<IPdfDocument> overload ──────────────────────────────────

    [Fact]
    public async Task MergeAsync_TwoDocuments_PageCountIsSumOfBoth()
    {
        await using var a = await LoadFixtureAsync(2);
        await using var b = await LoadFixtureAsync(3);
        await using var merged = await Merger.MergeAsync([a, b], MergeOptions.Default, TestContext.Current.CancellationToken);
        merged.PageCount.ShouldBe(5);
    }

    [Fact]
    public async Task MergeAsync_ThreeDocuments_AllPagesPreserved()
    {
        await using var a = await LoadFixtureAsync(1);
        await using var b = await LoadFixtureAsync(2);
        await using var c = await LoadFixtureAsync(4);
        await using var merged = await Merger.MergeAsync([a, b, c], MergeOptions.Default, TestContext.Current.CancellationToken);
        merged.PageCount.ShouldBe(7);
    }

    [Fact]
    public async Task MergeAsync_SingleDocument_PageCountMatches()
    {
        await using var a = await LoadFixtureAsync(3);
        await using var merged = await Merger.MergeAsync([a], MergeOptions.Default, TestContext.Current.CancellationToken);
        merged.PageCount.ShouldBe(3);
    }

    [Fact]
    public Task MergeAsync_EmptyList_Throws() =>
        Should.ThrowAsync<ArgumentException>(static () => Merger.MergeAsync(Array.Empty<IPdfDocument>(), MergeOptions.Default));

    [Fact]
    public async Task MergeAsync_ProducedDocument_IsParseableAfterSave()
    {
        await using var a = await LoadFixtureAsync(2);
        await using var b = await LoadFixtureAsync(2);
        await using var merged = await Merger.MergeAsync([a, b], MergeOptions.Default, TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(merged, ms, cancellationToken: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);

        reloaded.PageCount.ShouldBe(4);
    }

    [Fact]
    public async Task MergeAsync_MergedDocument_HasCorrectPagesCount()
    {
        await using var a = await LoadFixtureAsync(3);
        await using var b = await LoadFixtureAsync(5);
        await using var merged = await Merger.MergeAsync([a, b], MergeOptions.Default, TestContext.Current.CancellationToken);
        merged.Pages.Count.ShouldBe(8);
    }

    [Fact]
    public async Task MergeAsync_AllPagesAccessible()
    {
        await using var a = await LoadFixtureAsync(2);
        await using var b = await LoadFixtureAsync(2);
        await using var merged = await Merger.MergeAsync([a, b], MergeOptions.Default, TestContext.Current.CancellationToken);

        Should.NotThrow(() =>
            {
                for (var i = 1; i <= merged.PageCount; i++)
                    _ = merged.Pages[i];
            }
        );
    }

    [Fact]
    public async Task MergeAsync_Cancellation_ThrowsOperationCanceledException()
    {
        await using var a = await LoadFixtureAsync(1);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Merger.MergeAsync([a], MergeOptions.Default, cts.Token));
    }

    // ── IReadOnlyList<Stream> overload ────────────────────────────────────────

    [Fact]
    public async Task MergeAsync_Streams_TwoDocuments_PageCountIsSumOfBoth()
    {
        var a = new MemoryStream(PdfFixtures.MultiPage(2));
        var b = new MemoryStream(PdfFixtures.MultiPage(3));
        await using var merged = await Merger.MergeAsync([a, b], MergeOptions.Default, TestContext.Current.CancellationToken);
        merged.PageCount.ShouldBe(5);
    }

    [Fact]
    public async Task MergeAsync_Streams_EmptyList_Throws() =>
        await Should.ThrowAsync<ArgumentException>(static () => Merger.MergeAsync(new List<Stream>(), MergeOptions.Default));

    [Fact]
    public async Task MergeAsync_Streams_ProducedDocument_IsParseableAfterSave()
    {
        var a = new MemoryStream(PdfFixtures.MultiPage(2));
        var b = new MemoryStream(PdfFixtures.MultiPage(2));
        await using var merged = await Merger.MergeAsync([a, b], MergeOptions.Default, TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(merged, ms, cancellationToken: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);

        reloaded.PageCount.ShouldBe(4);
    }

    [Fact]
    public async Task MergeAsync_StreamsAndDocuments_ProduceSamePageCount()
    {
        const int pagesA = 2, pagesB = 3;
        await using var a = await LoadFixtureAsync(pagesA);
        await using var b = await LoadFixtureAsync(pagesB);
        await using var mergedDocs = await Merger.MergeAsync([a, b], MergeOptions.Default, TestContext.Current.CancellationToken);

        var sa = new MemoryStream(PdfFixtures.MultiPage(pagesA));
        var sb = new MemoryStream(PdfFixtures.MultiPage(pagesB));
        await using var mergedStreams = await Merger.MergeAsync([sa, sb], MergeOptions.Default, TestContext.Current.CancellationToken);

        mergedDocs.PageCount.ShouldBe(mergedStreams.PageCount);
    }

    [Fact]
    public async Task MergeAsync_Streams_Cancellation_ThrowsOperationCanceledException()
    {
        var s = new MemoryStream(PdfFixtures.MultiPage(1));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Merger.MergeAsync([s], MergeOptions.Default, cts.Token));
    }

    // ── MergeOptions ─────────────────────────────────────────────────────────

    [Fact]
    public async Task MergeAsync_FastOptions_ProducesValidDocument()
    {
        await using var a = await LoadFixtureAsync(2);
        await using var b = await LoadFixtureAsync(1);
        await using var merged = await Merger.MergeAsync([a, b], MergeOptions.Fast, TestContext.Current.CancellationToken);
        merged.PageCount.ShouldBe(3);
    }

    // ── With table content ────────────────────────────────────────────────────

    [Fact]
    public async Task MergeAsync_WithTableDocument_PageCountCorrect()
    {
        var tableGen = new TableGenerator();
        var data = new TableData
        {
            Headers = ["Name", "Value"],
            Rows = [["A", "1"], ["B", "2"]]
        };
        await using var tableDoc = await tableGen.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        await using var plainDoc = await LoadFixtureAsync(2);
        await using var merged = await Merger.MergeAsync([tableDoc, plainDoc], MergeOptions.Default, TestContext.Current.CancellationToken);

        merged.PageCount.ShouldBe(3);
    }

    // ── IReadOnlyList<MergeSource> overload (page-range merge) ─────────────────

    [Fact]
    public async Task MergeAsync_Sources_SingleRange_SelectsOnlyThosePages()
    {
        await using var a = await LoadTaggedAsync(5);
        await using var merged = await Merger.MergeAsync(
            [new MergeSource(a, [(3, 5)])],
            MergeOptions.Default,
            TestContext.Current.CancellationToken
        );

        merged.PageCount.ShouldBe(3);
        PageTags(merged).ShouldBe(["P3", "P4", "P5"]);
    }

    [Fact]
    public async Task MergeAsync_Sources_RangesReorderPages()
    {
        await using var a = await LoadTaggedAsync(4);
        await using var merged = await Merger.MergeAsync(
            [new MergeSource(a, [(4, 4), (1, 2)])],
            MergeOptions.Default,
            TestContext.Current.CancellationToken
        );

        PageTags(merged).ShouldBe(["P4", "P1", "P2"]);
    }

    [Fact]
    public async Task MergeAsync_Sources_RepeatedPage_AppearsTwice()
    {
        await using var a = await LoadTaggedAsync(3);
        await using var merged = await Merger.MergeAsync(
            [new MergeSource(a, [(2, 2), (2, 2)])],
            MergeOptions.Default,
            TestContext.Current.CancellationToken
        );

        PageTags(merged).ShouldBe(["P2", "P2"]);
    }

    [Fact]
    public async Task MergeAsync_Sources_MultipleDocuments_InterleaveRanges()
    {
        await using var a = await LoadTaggedAsync(3);
        await using var b = await LoadTaggedAsync(3);
        await using var merged = await Merger.MergeAsync(
            [new MergeSource(a, [(1, 1)]), new MergeSource(b, [(2, 3)])],
            MergeOptions.Default,
            TestContext.Current.CancellationToken
        );

        PageTags(merged).ShouldBe(["P1", "P2", "P3"]);
    }

    [Fact]
    public async Task MergeAsync_Sources_NullRange_TakesAllPages()
    {
        await using var a = await LoadTaggedAsync(2);
        await using var b = await LoadTaggedAsync(2);
        await using var merged = await Merger.MergeAsync(
            [new MergeSource(a), new MergeSource(b, [(1, 1)])],
            MergeOptions.Default,
            TestContext.Current.CancellationToken
        );

        merged.PageCount.ShouldBe(3);
        PageTags(merged).ShouldBe(["P1", "P2", "P1"]);
    }

    [Fact]
    public async Task MergeAsync_Sources_RangeMerge_ParseableAfterSave()
    {
        await using var a = await LoadTaggedAsync(4);
        await using var merged = await Merger.MergeAsync(
            [new MergeSource(a, [(2, 3)])],
            MergeOptions.Default,
            TestContext.Current.CancellationToken
        );

        using var ms = new MemoryStream();
        await Processor.SaveAsync(merged, ms, cancellationToken: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);

        reloaded.PageCount.ShouldBe(2);
        PageTags(reloaded).ShouldBe(["P2", "P3"]);
    }

    [Fact]
    public async Task MergeAsync_Sources_RangeOutOfBounds_Throws()
    {
        await using var a = await LoadTaggedAsync(3);
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => Merger.MergeAsync([new MergeSource(a, [(2, 9)])], MergeOptions.Default, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MergeAsync_Sources_EmptyList_Throws() =>
        await Should.ThrowAsync<ArgumentException>(static () => Merger.MergeAsync(Array.Empty<MergeSource>(), MergeOptions.Default));
}
