using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class DocumentMergerTests : PdfTestBase
{
    private static readonly DocumentMerger Merger = new();

    // Convenience wrapper that builds a MultiPage fixture and loads it.
    private static Task<Abstractions.IPdfDocument> LoadFixtureAsync(int pages) =>
        LoadAsync(PdfFixtures.MultiPage(pages));

    // ── IReadOnlyList<IPdfDocument> overload ──────────────────────────────────

    [Fact]
    public async Task MergeAsync_TwoDocuments_PageCountIsSumOfBoth()
    {
        await using var a = await LoadFixtureAsync(2);
        await using var b = await LoadFixtureAsync(3);
        await using var merged = await Merger.MergeAsync([a, b], MergeOptions.Default, ct: TestContext.Current.CancellationToken);
        merged.PageCount.ShouldBe(5);
    }

    [Fact]
    public async Task MergeAsync_ThreeDocuments_AllPagesPreserved()
    {
        await using var a = await LoadFixtureAsync(1);
        await using var b = await LoadFixtureAsync(2);
        await using var c = await LoadFixtureAsync(4);
        await using var merged = await Merger.MergeAsync([a, b, c], MergeOptions.Default, ct: TestContext.Current.CancellationToken);
        merged.PageCount.ShouldBe(7);
    }

    [Fact]
    public async Task MergeAsync_SingleDocument_PageCountMatches()
    {
        await using var a = await LoadFixtureAsync(3);
        await using var merged = await Merger.MergeAsync([a], MergeOptions.Default, ct: TestContext.Current.CancellationToken);
        merged.PageCount.ShouldBe(3);
    }

    [Fact]
    public Task MergeAsync_EmptyList_Throws() =>
        Should.ThrowAsync<ArgumentException>(static () => Merger.MergeAsync(Array.Empty<Abstractions.IPdfDocument>(), MergeOptions.Default));

    [Fact]
    public async Task MergeAsync_ProducedDocument_IsParseableAfterSave()
    {
        await using var a = await LoadFixtureAsync(2);
        await using var b = await LoadFixtureAsync(2);
        await using var merged = await Merger.MergeAsync([a, b], MergeOptions.Default, ct: TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(merged, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, ct: TestContext.Current.CancellationToken);

        reloaded.PageCount.ShouldBe(4);
    }

    [Fact]
    public async Task MergeAsync_MergedDocument_HasCorrectPagesCount()
    {
        await using var a = await LoadFixtureAsync(3);
        await using var b = await LoadFixtureAsync(5);
        await using var merged = await Merger.MergeAsync([a, b], MergeOptions.Default, ct: TestContext.Current.CancellationToken);
        merged.Pages.Count.ShouldBe(8);
    }

    [Fact]
    public async Task MergeAsync_AllPagesAccessible()
    {
        await using var a = await LoadFixtureAsync(2);
        await using var b = await LoadFixtureAsync(2);
        await using var merged = await Merger.MergeAsync([a, b], MergeOptions.Default, ct: TestContext.Current.CancellationToken);

        Should.NotThrow(() =>
        {
            for (var i = 1; i <= merged.PageCount; i++)
                _ = merged.Pages[i];
        });
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
        await using var merged = await Merger.MergeAsync([a, b], MergeOptions.Default, ct: TestContext.Current.CancellationToken);
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
        await using var merged = await Merger.MergeAsync([a, b], MergeOptions.Default, ct: TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(merged, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, ct: TestContext.Current.CancellationToken);

        reloaded.PageCount.ShouldBe(4);
    }

    [Fact]
    public async Task MergeAsync_StreamsAndDocuments_ProduceSamePageCount()
    {
        const int pagesA = 2, pagesB = 3;
        await using var a = await LoadFixtureAsync(pagesA);
        await using var b = await LoadFixtureAsync(pagesB);
        await using var mergedDocs = await Merger.MergeAsync([a, b], MergeOptions.Default, ct: TestContext.Current.CancellationToken);

        var sa = new MemoryStream(PdfFixtures.MultiPage(pagesA));
        var sb = new MemoryStream(PdfFixtures.MultiPage(pagesB));
        await using var mergedStreams = await Merger.MergeAsync([sa, sb], MergeOptions.Default, ct: TestContext.Current.CancellationToken);

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
        await using var merged = await Merger.MergeAsync([a, b], MergeOptions.Fast, ct: TestContext.Current.CancellationToken);
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
        await using var tableDoc = await tableGen.GenerateAsync(data, TableStyle.Default, ct: TestContext.Current.CancellationToken);
        await using var plainDoc = await LoadFixtureAsync(2);
        await using var merged = await Merger.MergeAsync([tableDoc, plainDoc], MergeOptions.Default, ct: TestContext.Current.CancellationToken);

        merged.PageCount.ShouldBe(3);
    }
}
