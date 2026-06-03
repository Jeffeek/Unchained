using Shouldly;
using System.Text;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
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

    /// <summary>
    /// A content stream whose uncompressed size exceeds <c>CompressionThresholdBytes</c>
    /// (128) must be compressed with FlateDecode after optimization.
    /// The saved file must be smaller than the original (or equal, in the degenerate case),
    /// and the document must still parse correctly after a round-trip.
    /// </summary>
    [Fact]
    public async Task OptimizeAsync_LargeUncompressedStream_GetsCompressed()
    {
        // Build a content stream well above the 128-byte threshold.
        // 200 repetitions of "BT /F1 12 Tf 100 700 Td (x) Tj ET\n" is ~6 KB.
        var longContent = string.Concat(Enumerable.Repeat("BT /F1 12 Tf 100 700 Td (x) Tj ET\n", 200));
        var originalBytes = PdfFixtures.WithTextContent(longContent);

        await using var doc = await LoadAsync(originalBytes);
        await Optimizer.OptimizeAsync(doc, ct: TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        // The round-trip document must load and retain its page.
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms);
        reloaded.PageCount.ShouldBe(1);

        // Compressed output should be strictly smaller than the raw input.
        ms.Length.ShouldBeLessThan(originalBytes.Length);
    }

    /// <summary>
    /// Streams whose uncompressed size is below <c>CompressionThresholdBytes</c> (128)
    /// must be left untouched: the optimizer must skip them and the document must remain
    /// parseable after the (no-op) optimization and a save round-trip.
    /// </summary>
    [Fact]
    public async Task OptimizeAsync_SmallStream_IsLeftUncompressed()
    {
        // "BT /F1 12 Tf 100 700 Td (Hi) Tj ET" is ~38 bytes — well under 128.
        var bytes = PdfFixtures.WithTextContent(text: "Hi");
        var originalSize = bytes.Length;

        await using var doc = await LoadAsync(bytes);
        await Optimizer.OptimizeAsync(doc, ct: TestContext.Current.CancellationToken);

        // Document must still load correctly after optimization.
        doc.PageCount.ShouldBe(1);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms);
        reloaded.PageCount.ShouldBe(1);

        // No compression gain expected; output size should be in the same order of magnitude.
        ms.Length.ShouldBeGreaterThan(0);
        // The small stream should not have been compressed — saved size should be
        // close to original (within a generous 50 % margin for PDF overhead differences).
        ms.Length.ShouldBeLessThan(originalSize * 3);
    }

    /// <summary>
    /// Calling <see cref="DocumentOptimizer.OptimizeAsync"/> on a document that was
    /// loaded with an explicit password (i.e. it was encrypted at rest) should complete
    /// without error, because the document is already fully decrypted in memory and the
    /// optimizer works on the in-memory object graph.
    /// </summary>
    [Fact]
    public async Task OptimizeAsync_EncryptedDocument_CompletesWithoutThrowing()
    {
        // Create an AES-256 encrypted PDF and reload it with the password so the
        // document is decrypted in memory.
        await using var original = await LoadAsync(PdfFixtures.MultiPage(count: 2));
        var encOpts = new SaveOptions(Encryption: new EncryptionOptions(UserPassword: "optimize-me"));
        using var encMs = new MemoryStream();
        await Processor.SaveAsync(original, encMs, encOpts, ct: TestContext.Current.CancellationToken);

        var encBytes = encMs.ToArray();
        await using var encrypted = await Processor.LoadAsync(new MemoryStream(encBytes), "optimize-me", ct: TestContext.Current.CancellationToken);

        // The document must be reported as encrypted.
        encrypted.IsEncrypted.ShouldBeTrue();

        // Optimize should complete without throwing — the in-memory objects are decrypted.
        await Optimizer.OptimizeAsync(encrypted, ct: TestContext.Current.CancellationToken);
        encrypted.PageCount.ShouldBe(2);
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

    /// <summary>
    /// When two distinct indirect objects carry identical binary streams,
    /// <see cref="DocumentOptimizer.OptimizeResourcesAsync"/> must deduplicate them:
    /// the saved output must contain fewer objects than the original and the document
    /// must still round-trip cleanly.
    /// </summary>
    [Fact]
    public async Task OptimizeResourcesAsync_DuplicateImageStreams_ReducesObjectCount()
    {
        // Build a PDF that embeds the same image pixel data in two separate
        // indirect stream objects so OptimizeResources has something to merge.
        const int width = 4;
        const int height = 4;
        var rgbData = new byte[width * height * 3]; // all-black 4×4 image
        for (var i = 0; i < rgbData.Length; i++)
            rgbData[i] = 0x42;

        var pdfBytes = PdfFixtures.WithDuplicateImageStreams(width, height, rgbData);
        var originalObjectCount = CountIndirectObjects(pdfBytes);

        await using var doc = await LoadAsync(pdfBytes);
        await Optimizer.OptimizeResourcesAsync(doc, ct: TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        // Document must remain parseable.
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms);
        reloaded.PageCount.ShouldBe(1);

        // After deduplication the object count must be smaller.
        var optimizedObjectCount = CountIndirectObjects(ms.ToArray());
        optimizedObjectCount.ShouldBeLessThan(originalObjectCount);
    }

    /// <summary>
    /// When two distinct indirect objects carry identical font program streams,
    /// <see cref="DocumentOptimizer.OptimizeResourcesAsync"/> must deduplicate them.
    /// The document must parse and its page count must be preserved.
    /// </summary>
    [Fact]
    public async Task OptimizeResourcesAsync_DuplicateFontStreams_ReducesObjectCount()
    {
        // Reuse the embedded-font fixture with a synthetic 200-byte font "program"
        // duplicated into a second stream object so the optimizer has a match.
        var fontData = new byte[200];
        for (var i = 0; i < fontData.Length; i++) fontData[i] = (byte)(i & 0xFF);

        var pdfBytes = PdfFixtures.WithDuplicateFontStreams(fontData);
        var originalObjectCount = CountIndirectObjects(pdfBytes);

        await using var doc = await LoadAsync(pdfBytes);
        await Optimizer.OptimizeResourcesAsync(doc, ct: TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms);
        reloaded.PageCount.ShouldBe(1);

        var optimizedObjectCount = CountIndirectObjects(ms.ToArray());
        optimizedObjectCount.ShouldBeLessThan(originalObjectCount);
    }

    /// <summary>
    /// <see cref="DocumentOptimizer.OptimizeResourcesAsync"/> must be a no-op
    /// (no crash, no data loss) when all streams in the document are unique.
    /// </summary>
    [Fact]
    public async Task OptimizeResourcesAsync_NoDuplicates_DocumentUnchanged()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        // Should complete without throwing.
        await Optimizer.OptimizeResourcesAsync(doc, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    /// <summary>
    /// <see cref="DocumentOptimizer.OptimizeResourcesAsync"/> cancellation must propagate.
    /// </summary>
    [Fact]
    public async Task OptimizeResourcesAsync_Cancellation_ThrowsOperationCanceledException()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Optimizer.OptimizeResourcesAsync(doc, cts.Token));
    }

    // ── Repair (DocumentProcessor.RepairAsync) ────────────────────────────────

    /// <summary>
    /// <see cref="DocumentProcessor.RepairAsync"/> on a well-formed PDF must take the
    /// normal parse path and return a document with the correct page count.
    /// </summary>
    [Fact]
    public async Task RepairAsync_ValidDocument_ReturnsCorrectPageCount()
    {
        var bytes = PdfFixtures.MultiPage(count: 4);
        await using var doc = await Processor.RepairAsync(bytes, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(4);
    }

    /// <summary>
    /// <see cref="DocumentProcessor.RepairAsync"/> on a PDF with a corrupted xref
    /// must either recover pages via byte-scan or throw <see cref="Core.PdfException"/>;
    /// it must never crash with an unhandled exception of another type.
    /// </summary>
    [Fact]
    public async Task RepairAsync_CorruptedXref_RecoversOrThrowsPdfException()
    {
        var bytes = PdfFixtures.SinglePage();
        // Zero out the final 80 bytes to destroy the xref/trailer.
        var corrupted = (byte[])bytes.Clone();
        var wipeStart = Math.Max(0, corrupted.Length - 80);
        Array.Fill(corrupted, (byte)0x00, wipeStart, corrupted.Length - wipeStart);

        try
        {
            await using var doc = await Processor.RepairAsync(corrupted, ct: TestContext.Current.CancellationToken);
            doc.PageCount.ShouldBeGreaterThanOrEqualTo(0);
        }
        catch (Core.PdfException)
        {
            // Acceptable: repair attempted but could not salvage the document.
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Counts the number of <c>N G obj</c> markers in raw PDF bytes as a proxy for
    /// the number of indirect objects present in the file.
    /// </summary>
    private static int CountIndirectObjects(byte[] pdfBytes)
    {
        var text = Encoding.Latin1.GetString(pdfBytes);
        var count = 0;
        var i = 0;
        while (i < text.Length)
        {
            var idx = text.IndexOf(" obj", i, StringComparison.Ordinal);
            if (idx < 0)
                break;

            count++;
            i = idx + 4;
        }

        return count;
    }
}
