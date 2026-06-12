using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class BookmarkEditorTests : PdfTestBase
{
    private static readonly BookmarkEditor Editor = new();


    // ── GetBookmarks (reading existing) ──────────────────────────────────────

    [Fact]
    public async Task GetBookmarks_WithOutlines_ReturnsItems()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithOutlines(("Chapter 1", 1), ("Chapter 2", 2)), TestContext.Current.CancellationToken);
        doc.GetBookmarks().Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetBookmarks_Titles_Match()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithOutlines(("Introduction", 1)), TestContext.Current.CancellationToken);
        doc.GetBookmarks()[0].Title.ShouldBe("Introduction");
    }

    [Fact]
    public async Task GetBookmarks_PageNumbers_Match()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithOutlines(("Ch1", 1), ("Ch2", 2)), TestContext.Current.CancellationToken);
        doc.GetBookmarks()[0].PageNumber.ShouldBe(1);
        doc.GetBookmarks()[1].PageNumber.ShouldBe(2);
    }

    [Fact]
    public async Task GetBookmarks_NoOutlines_ReturnsEmpty()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        doc.GetBookmarks().ShouldBeEmpty();
    }

    // ── SetBookmarksAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SetBookmarksAsync_FlatList_BookmarksPresent()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);
        var bms = new List<Bookmark>
        {
            new("Part 1", 1),
            new("Part 2", 2)
        };
        await Editor.SetBookmarksAsync(doc, bms, TestContext.Current.CancellationToken);
        doc.GetBookmarks().Count.ShouldBe(2);
    }

    [Fact]
    public async Task SetBookmarksAsync_Titles_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetBookmarksAsync(doc, [new("MyChapter", 1)], TestContext.Current.CancellationToken);
        doc.GetBookmarks()[0].Title.ShouldBe("MyChapter");
    }

    [Fact]
    public async Task SetBookmarksAsync_PageNumbers_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);
        await Editor.SetBookmarksAsync(doc,
            [
                new("A", 1),
                new("B", 2)
            ],
            TestContext.Current.CancellationToken);
        doc.GetBookmarks()[0].PageNumber.ShouldBe(1);
        doc.GetBookmarks()[1].PageNumber.ShouldBe(2);
    }

    [Fact]
    public async Task SetBookmarksAsync_EmptyList_RemovesBookmarks()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithOutlines(("Ch1", 1)), TestContext.Current.CancellationToken);
        await Editor.SetBookmarksAsync(doc, [], TestContext.Current.CancellationToken);
        doc.GetBookmarks().ShouldBeEmpty();
    }

    [Fact]
    public async Task SetBookmarksAsync_PageCountUnchanged()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);
        await Editor.SetBookmarksAsync(doc, [new("A", 1)], TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(3);
    }

    [Fact]
    public async Task SetBookmarksAsync_RoundTrip_ParseableAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);
        await Editor.SetBookmarksAsync(doc, [new("X", 1), new("Y", 2)], TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);
        reloaded.GetBookmarks().Count.ShouldBe(2);
    }

    [Fact]
    public async Task SetBookmarksAsync_NestedBookmarks_CountCorrect()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);
        var bms = new List<Bookmark>
        {
            new("Parent", 1, [new("Child", 2)])
        };
        await Editor.SetBookmarksAsync(doc, bms, TestContext.Current.CancellationToken);
        var top = doc.GetBookmarks();
        top.Count.ShouldBe(1);
        top[0].Children.ShouldNotBeNull();
        top[0].Children!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SetBookmarksAsync_Cancellation_ThrowsOperationCanceledException()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Editor.SetBookmarksAsync(doc, [new("A", 1)], cts.Token));
    }
}
