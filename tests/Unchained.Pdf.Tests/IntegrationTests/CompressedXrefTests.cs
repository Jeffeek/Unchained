using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
/// Tests that verify parsing of PDFs with compressed cross-reference streams
/// (ISO 32000-1 §7.5.8) and objects stored inside object streams (§7.5.7).
/// These cover the two <c>NotImplementedException</c> stubs that were replaced
/// in Milestone 1.
/// </summary>
public sealed class CompressedXrefTests : IDisposable
{
    private readonly DocumentProcessor _processor = new();

    public void Dispose() => _processor.Dispose();

    // ── Compressed xref stream (§7.5.8) ──────────────────────────────────────

    [Fact]
    public async Task Load_CompressedXref_SinglePage_ParsesCorrectly()
    {
        var bytes = PdfFixtures.WithCompressedXref(pageCount: 1);
        await using var doc = await _processor.LoadAsync(new MemoryStream(bytes));
        doc.PageCount.ShouldBe(1);
    }

    [
        Theory,
        InlineData(1),
        InlineData(3),
        InlineData(10)
    ]
    public async Task Load_CompressedXref_MultiPage_PageCountMatches(int count)
    {
        var bytes = PdfFixtures.WithCompressedXref(pageCount: count);
        await using var doc = await _processor.LoadAsync(new MemoryStream(bytes));
        doc.PageCount.ShouldBe(count);
    }

    [Fact]
    public async Task Load_CompressedXref_Pages_DimensionsCorrect()
    {
        var bytes = PdfFixtures.WithCompressedXref(pageCount: 1);
        await using var doc = await _processor.LoadAsync(new MemoryStream(bytes));
        doc.Pages[1].Width.ShouldBe(595.0, tolerance: 0.1);
        doc.Pages[1].Height.ShouldBe(842.0, tolerance: 0.1);
    }

    [Fact]
    public async Task Load_CompressedXref_IterateAllPages_Works()
    {
        var bytes = PdfFixtures.WithCompressedXref(pageCount: 5);
        await using var doc = await _processor.LoadAsync(new MemoryStream(bytes));
        var numbers = doc.Pages.Select(static p => p.PageNumber).ToList();
        numbers.ShouldBe([1, 2, 3, 4, 5]);
    }

    [Fact]
    public async Task Load_CompressedXref_SaveAndReload_PreservesPageCount()
    {
        var bytes = PdfFixtures.WithCompressedXref(pageCount: 2);
        await using var original = await _processor.LoadAsync(new MemoryStream(bytes));

        var saved = new MemoryStream();
        await _processor.SaveAsync(original, saved);

        await using var reloaded = await _processor.LoadAsync(new MemoryStream(saved.ToArray()));
        reloaded.PageCount.ShouldBe(2);
    }

    // ── Mixed: traditional xref still works after compressed xref is supported ──

    [Fact]
    public async Task Load_TraditionalXref_StillParsesCorrectly()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()));
        doc.PageCount.ShouldBe(1);
    }
}
