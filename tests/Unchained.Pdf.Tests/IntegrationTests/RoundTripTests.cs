using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     End-to-end tests that exercise the full parse → inspect → save → reparse pipeline.
///     These tests validate that the parser, document model, and writer are coherent.
/// </summary>
public sealed class RoundTripTests : IDisposable
{
    private readonly DocumentProcessor _processor = new();

    public void Dispose() => _processor.Dispose();

    // ── Load and inspect ──────────────────────────────────────────────────────

    [Fact]
    public async Task Load_SinglePage_PageCountIsOne()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()), TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [
        Theory,
        InlineData(1),
        InlineData(3),
        InlineData(10),
        InlineData(50)
    ]
    public async Task Load_MultiPage_PageCountMatches(int count)
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.MultiPage(count)), TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(count);
    }

    [Fact]
    public async Task Load_Pages_DimensionsMatchMediaBox()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()), TestContext.Current.CancellationToken);
        var page = doc.Pages[1];
        page.Width.ShouldBe(595.0, 0.1);
        page.Height.ShouldBe(842.0, 0.1);
    }

    [Fact]
    public async Task Load_Pages_IsPortraitOrientation()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()), TestContext.Current.CancellationToken);
        doc.Pages[1].IsLandscape.ShouldBeFalse();
    }

    [Fact]
    public async Task Load_PagesCollection_CountMatchesPageCount()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.MultiPage(3)), TestContext.Current.CancellationToken);
        doc.Pages.Count.ShouldBe(doc.PageCount);
    }

    [Fact]
    public async Task Load_EnumeratePages_AllPagesAccessible()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.MultiPage(3)), TestContext.Current.CancellationToken);
        var numbers = doc.Pages.Select(static p => p.PageNumber).ToList();
        numbers.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Load_PageIndexer_OutOfRange_Throws()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()), TestContext.Current.CancellationToken);
        Should.Throw<Exception>(() => _ = doc.Pages[0]);
        Should.Throw<Exception>(() => _ = doc.Pages[2]);
    }

    // ── Metadata ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Load_NoInfoDictionary_MetadataIsEmpty()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()), TestContext.Current.CancellationToken);
        doc.Metadata.ShouldBe(DocumentMetadata.Empty);
    }

    [Fact]
    public async Task Load_WithInfoDictionary_MetadataPopulated()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.WithInfo("My Title", "Jane Doe")),
            TestContext.Current.CancellationToken);
        doc.Metadata.Title.ShouldBe("My Title");
        doc.Metadata.Author.ShouldBe("Jane Doe");
    }

    // ── Save and reload ───────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndReload_SinglePage_PreservesPageCount()
    {
        await using var original = await _processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()), TestContext.Current.CancellationToken);

        var saved = new MemoryStream();
        await _processor.SaveAsync(original, saved, ct: TestContext.Current.CancellationToken);

        await using var reloaded = await _processor.LoadAsync(new MemoryStream(saved.ToArray()), TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task SaveAndReload_MultiPage_PreservesPageCount()
    {
        await using var original = await _processor.LoadAsync(new MemoryStream(PdfFixtures.MultiPage(4)), TestContext.Current.CancellationToken);

        var saved = new MemoryStream();
        await _processor.SaveAsync(original, saved, ct: TestContext.Current.CancellationToken);

        await using var reloaded = await _processor.LoadAsync(new MemoryStream(saved.ToArray()), TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(4);
    }

    // ── Load from file vs stream ──────────────────────────────────────────────

    [Fact]
    public async Task LoadFromFile_MatchesLoadFromStream()
    {
        var path = Path.GetTempFileName();
        try
        {
            var bytes = PdfFixtures.MultiPage(2);
            await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);

            await using var fromFile = await _processor.LoadAsync(path, TestContext.Current.CancellationToken);
            await using var fromStream = await _processor.LoadAsync(new MemoryStream(bytes), TestContext.Current.CancellationToken);

            fromFile.PageCount.ShouldBe(fromStream.PageCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Dispose behaviour ────────────────────────────────────────────────────

    [Fact]
    public async Task Dispose_SetsIsDisposed()
    {
        var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()), TestContext.Current.CancellationToken);
        doc.IsDisposed.ShouldBeFalse();
        await doc.DisposeAsync();
        doc.IsDisposed.ShouldBeTrue();
    }
}
