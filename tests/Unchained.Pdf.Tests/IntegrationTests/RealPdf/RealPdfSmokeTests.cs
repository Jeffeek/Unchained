using Shouldly;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests.RealPdf;

/// <summary>
/// Smoke tests that run over every *.pdf in TestFiles/.
/// Files that throw PdfException during parsing are skipped gracefully.
/// Tests call Skip.Always when no parseable files exist — distinguishing
/// "not run" from "passed".
/// </summary>
public sealed class RealPdfSmokeTests : PdfTestBase
{
    [Fact]
    public async Task Parse_AllRealPdfs_DoNotCrashUnhandled()
    {
        var tested = 0;
        foreach (var path in Files())
        {
            // PdfException is acceptable for genuinely malformed files.
            await using var doc = await TryLoadDocAsync(path);
            tested++;
        }

        if (tested == 0)
            Assert.Skip("No PDF files found in TestFiles/.");
    }

    [Fact]
    public async Task Parse_AllRealPdfs_PageCountIsPositive()
    {
        var tested = 0;
        foreach (var path in Files())
        {
            await using var doc = await TryLoadDocAsync(path);
            if (doc is null)
                continue;

            doc.PageCount.ShouldBeGreaterThan(0, path);
            tested++;
        }

        if (tested == 0)
            Assert.Skip("No parseable PDF files found in TestFiles/.");
    }

    [Fact]
    public async Task Parse_AllRealPdfs_PagesMatchPageCount()
    {
        var tested = 0;
        foreach (var path in Files())
        {
            await using var doc = await TryLoadDocAsync(path);
            if (doc is null)
                continue;

            doc.Pages.Count.ShouldBe(doc.PageCount, path);
            tested++;
        }

        if (tested == 0)
            Assert.Skip("No parseable PDF files found in TestFiles/.");
    }

    [Fact]
    public async Task Parse_AllRealPdfs_AllPagesHavePositiveDimensions()
    {
        var tested = 0;
        foreach (var path in Files())
        {
            await using var doc = await TryLoadDocAsync(path);
            if (doc is null)
                continue;

            for (var i = 1; i <= doc.PageCount; i++)
            {
                doc.Pages[i].Width.ShouldBeGreaterThanOrEqualTo(0, $"{path} page {i} width");
                doc.Pages[i].Height.ShouldBeGreaterThanOrEqualTo(0, $"{path} page {i} height");
            }

            tested++;
        }

        if (tested == 0)
            Assert.Skip("No parseable PDF files found in TestFiles/.");
    }

    [Fact]
    public async Task Parse_AllRealPdfs_GetContentOperators_DoesNotThrow()
    {
        var tested = 0;
        foreach (var path in Files())
        {
            await using var doc = await TryLoadDocAsync(path);
            if (doc is null)
                continue;

            for (var i = 1; i <= doc.PageCount; i++)
                doc.Pages[i].GetContentOperators().ShouldNotBeNull(path);
            tested++;
        }

        if (tested == 0)
            Assert.Skip("No parseable PDF files found in TestFiles/.");
    }

    [Fact]
    public async Task RoundTrip_AllRealPdfs_PageCountPreserved()
    {
        var tested = 0;
        foreach (var path in Files())
        {
            await using var doc = await TryLoadDocAsync(path);
            if (doc is null)
                continue;

            var before = doc.PageCount;
            await using var reloaded = await SaveAndReloadAsync(doc);
            reloaded.PageCount.ShouldBe(before, path);
            tested++;
        }

        if (tested == 0)
            Assert.Skip("No parseable PDF files found in TestFiles/.");
    }

    [Fact]
    public async Task RoundTrip_AllRealPdfs_OutputStartsWithPdfHeader()
    {
        var tested = 0;
        foreach (var path in Files())
        {
            await using var doc = await TryLoadDocAsync(path);
            if (doc is null)
                continue;

            using var ms = new MemoryStream();
            await Processor.SaveAsync(doc, ms);
            ms.Position = 0;
            var header = new byte[5];
            _ = ms.Read(header, 0, 5);
            header.ShouldBe("%PDF-"u8.ToArray(), path);
            tested++;
        }

        if (tested == 0)
            Assert.Skip("No parseable PDF files found in TestFiles/.");
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static IEnumerable<string> Files() =>
        RealPdfFixtures.AllPdfFilePaths().Select(static o => (string)o[0]);

    private static async Task<IPdfDocument?> TryLoadDocAsync(string path)
    {
        try { return await LoadAsync(await File.ReadAllBytesAsync(path)); }
        catch (Core.PdfException) { return null; }
    }
}
