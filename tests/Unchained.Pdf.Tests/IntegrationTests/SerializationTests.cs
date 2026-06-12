using System.Text;
using Shouldly;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Tests that verify the full-rewrite serialization path:
///     load a PDF → save it → reload the saved bytes → assert structural equivalence.
///     These tests specifically exercise the new <c>PdfDocumentAdapter.Serialize</c>
///     implementation (not the old source-bytes pass-through).
/// </summary>
public sealed class SerializationTests : IDisposable
{
    private readonly DocumentProcessor _processor = new();

    public void Dispose() => _processor.Dispose();

    // ── Round-trip fidelity ───────────────────────────────────────────────────

    [
        Theory,
        InlineData(1),
        InlineData(3),
        InlineData(10)
    ]
    public async Task SaveAndReload_TraditionalXref_PageCountPreserved(int pageCount)
    {
        await using var original =
            await _processor.LoadAsync(new MemoryStream(PdfFixtures.MultiPage(pageCount)), TestContext.Current.CancellationToken);

        var saved = await SaveToBytes(original, TestContext.Current.CancellationToken);

        await using var reloaded = await _processor.LoadAsync(new MemoryStream(saved), TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(pageCount);
    }

    [
        Theory,
        InlineData(1),
        InlineData(5)
    ]
    public async Task SaveAndReload_CompressedXref_PageCountPreserved(int pageCount)
    {
        await using var original =
            await _processor.LoadAsync(new MemoryStream(PdfFixtures.WithCompressedXref(pageCount)), TestContext.Current.CancellationToken);

        var saved = await SaveToBytes(original, TestContext.Current.CancellationToken);

        await using var reloaded = await _processor.LoadAsync(new MemoryStream(saved), TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(pageCount);
    }

    [Fact]
    public async Task SaveAndReload_PageDimensionsPreserved()
    {
        await using var original = await _processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()), TestContext.Current.CancellationToken);

        var saved = await SaveToBytes(original, TestContext.Current.CancellationToken);

        await using var reloaded = await _processor.LoadAsync(new MemoryStream(saved), TestContext.Current.CancellationToken);
        reloaded.Pages[1].Width.ShouldBe(595.0, 0.1);
        reloaded.Pages[1].Height.ShouldBe(842.0, 0.1);
    }

    [Fact]
    public async Task SaveAndReload_MetadataPreserved()
    {
        await using var original = await _processor.LoadAsync(new MemoryStream(PdfFixtures.WithInfo("My Title", "Jane Doe")),
            TestContext.Current.CancellationToken);

        var saved = await SaveToBytes(original, TestContext.Current.CancellationToken);

        await using var reloaded = await _processor.LoadAsync(new MemoryStream(saved), TestContext.Current.CancellationToken);
        reloaded.Metadata.Title.ShouldBe("My Title");
        reloaded.Metadata.Author.ShouldBe("Jane Doe");
    }

    // ── Output format ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Save_OutputStartsWithPdfHeader()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()), TestContext.Current.CancellationToken);
        var saved = await SaveToBytes(doc, TestContext.Current.CancellationToken);
        Encoding.Latin1.GetString(saved, 0, 7).ShouldBe("%PDF-1.");
    }

    [Fact]
    public async Task Save_OutputContainsXrefAndEof()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()), TestContext.Current.CancellationToken);
        var saved = await SaveToBytes(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(saved);
        text.ShouldContain("xref");
        text.ShouldContain("startxref");
        text.ShouldContain("%%EOF");
    }

    [Fact]
    public async Task Save_OutputIsSmallerOrSimilarSizeToInput()
    {
        // A full rewrite of a minimal PDF should not inflate it massively.
        // Allow up to 2× the original as a sanity ceiling.
        var input = PdfFixtures.SinglePage();
        await using var doc = await _processor.LoadAsync(new MemoryStream(input), TestContext.Current.CancellationToken);
        var saved = await SaveToBytes(doc, TestContext.Current.CancellationToken);
        saved.Length.ShouldBeLessThan(input.Length * 2);
    }

    [Fact]
    public async Task SaveTwice_ProducesConsistentOutput()
    {
        await using var doc = await _processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()), TestContext.Current.CancellationToken);

        var first = await SaveToBytes(doc, TestContext.Current.CancellationToken);
        var second = await SaveToBytes(doc, TestContext.Current.CancellationToken);

        // Re-serializing the same document twice should produce identical bytes.
        first.ShouldBe(second);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private async Task<byte[]> SaveToBytes(IPdfDocument doc, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await _processor.SaveAsync(doc, ms, ct: ct);
        return ms.ToArray();
    }
}
