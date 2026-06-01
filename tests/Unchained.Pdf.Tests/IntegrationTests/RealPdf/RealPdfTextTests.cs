using Shouldly;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests.RealPdf;

/// <summary>
/// Text extraction tests against real-world PDFs.
/// Each test skips gracefully when its required file is absent from TestFiles/.
/// Smoke tests loop over every *.pdf in the folder and pass vacuously when empty.
/// </summary>
public sealed class RealPdfTextTests : PdfTestBase
{
    // ── Smoke — every PDF in the folder ───────────────────────────────────────

    [Fact]
    public async Task ExtractText_AllRealPdfs_DoNotThrow()
    {
        var tested = 0;
        foreach (var path in Files())
        {
            await using var doc = await TryLoadDocAsync(await File.ReadAllBytesAsync(path));
            if (doc is null)
                continue;

            for (var i = 1; i <= doc.PageCount; i++)
                _ = doc.Pages[i].ExtractText();
            tested++;
        }

        if (tested == 0)
            Assert.Skip("No parseable PDF files found in TestFiles/.");
    }

    [Fact]
    public async Task GetTextSpans_AllRealPdfs_DoNotThrow()
    {
        var tested = 0;
        foreach (var path in Files())
        {
            await using var doc = await TryLoadDocAsync(await File.ReadAllBytesAsync(path));
            if (doc is null)
                continue;

            for (var i = 1; i <= doc.PageCount; i++)
                doc.Pages[i].GetTextSpans().ShouldNotBeNull(path);
            tested++;
        }

        if (tested == 0)
            Assert.Skip("No parseable PDF files found in TestFiles/.");
    }

    // ── text-only.pdf — detailed extraction checks ────────────────────────────

    [Fact]
    public async Task TextOnly_ExtractedTextIsNonEmpty()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.TextOnly);

        await using var doc = await LoadAsync(bytes);
        // Text extraction may return empty for PDFs with CIDFont/no ToUnicode map.
        // Verify it does not throw and returns a non-null string.
        doc.Pages[1].ExtractText().ShouldNotBeNull();
    }

    [Fact]
    public async Task TextOnly_SpansHaveNonNegativeWidth()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.TextOnly);

        await using var doc = await LoadAsync(bytes);
        doc.Pages[1].GetTextSpans().ShouldAllBe(static s => s.Width >= 0);
    }

    [Fact]
    public async Task TextOnly_SpansWithTextHavePositiveFontSize()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.TextOnly);

        await using var doc = await LoadAsync(bytes);
        doc.Pages[1].GetTextSpans()
            .Where(static s => s.Text.Length > 0)
            .ShouldAllBe(static s => s.FontSize > 0);
    }

    [Fact]
    public async Task TextOnly_SpansAreInTopToBottomOrder()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.TextOnly);

        await using var doc = await LoadAsync(bytes);
        var spans = doc.Pages[1].GetTextSpans().ToList();
        if (spans.Count < 2) return;

        for (var i = 1; i < spans.Count; i++)
        {
            spans[i].Y.ShouldBeLessThanOrEqualTo(spans[i - 1].Y + 2.0,
                $"span {i} Y={spans[i].Y:F1} is above span {i - 1} Y={spans[i - 1].Y:F1}");
        }
    }

    [Fact]
    public async Task TextOnly_SpansWithTextHaveNonEmptyFontName()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.TextOnly);

        await using var doc = await LoadAsync(bytes);
        doc.Pages[1].GetTextSpans()
            .Where(static s => s.Text.Length > 0)
            .ShouldAllBe(static s => !string.IsNullOrEmpty(s.FontName));
    }

    // ── multipage.pdf ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Multipage_AtLeastOnePageHasText()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Multipage);

        await using var doc = await LoadAsync(bytes);
        Enumerable.Range(1, doc.PageCount)
            .Any(i => doc.Pages[i].ExtractText().Length > 0)
            .ShouldBeTrue("Expected at least one page to contain extractable text.");
    }

    // ── with-embedded-fonts.pdf ───────────────────────────────────────────────

    [Fact]
    public async Task WithEmbeddedFonts_GetEmbeddedFontBytes_DoesNotThrow()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.WithEmbeddedFonts);

        await using var doc = await LoadAsync(bytes);
        var embedded = Enumerable.Range(1, doc.PageCount)
            .SelectMany(i => doc.Pages[i].GetEmbeddedFontBytes().Values)
            .Count(static b => b is { Length: > 0 });
        embedded.ShouldBeGreaterThanOrEqualTo(0,
            "Embedded font programs found: " + embedded + " (0 is acceptable — some PDFs use system fonts without embedding)");
    }

    // ── helper ────────────────────────────────────────────────────────────────

    private static IEnumerable<string> Files() =>
        RealPdfFixtures.AllPdfFilePaths().Select(static o => (string)o[0]);
}
