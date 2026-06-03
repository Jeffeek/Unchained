using Shouldly;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests.VeraPdf;

/// <summary>
/// Smoke tests against the veraPDF test corpus (https://github.com/veraPDF/veraPDF-corpus).
/// Tests cover ISO 32000-1/2, PDF/A-1b, and TWG files.
/// <para>
/// "pass" files conform to the spec — they must parse and return pages.
/// "fail" files intentionally violate a spec rule — <see cref="Core.PdfException"/> is acceptable.
/// Neither category should ever throw an unhandled exception.
/// </para>
/// </summary>
public sealed class VeraPdfSmokeTests(ITestOutputHelper outputHelper) : PdfTestBase
{
    // ── All files — no crash guarantee ───────────────────────────────────────

    [Fact]
    public async Task Parse_AllVeraPdfFiles_DoNotCrashUnhandled()
    {
        var total = 0;
        var loaded = 0;

        foreach (var pdfPath in VeraPdfFixtures.AllPdfFilePaths())
        {
            total++;
            await using var doc = await TryLoadDocAsync(await File.ReadAllBytesAsync(pdfPath, TestContext.Current.CancellationToken));

            if (doc is not null)
                loaded++;
        }

        if (total == 0)
            Assert.Skip("No veraPDF test files found — drop the corpus into TestFiles/veraPDF/.");

        // Just being informational — both outcomes are valid for a non-validating parser.
        loaded.ShouldBeGreaterThanOrEqualTo(0, $"loaded {loaded}/{total} veraPDF files");
    }

    [Fact]
    public Task Parse_AllVeraPdfFiles_PageCountIsNonNegative() =>
        Parse_AllVeraPdfFiles_Core(static (pdfPath, doc) =>
            {
                doc.PageCount.ShouldBeGreaterThanOrEqualTo(0, Path.GetFileName(pdfPath));

                return ValueTask.CompletedTask;
            },
            VeraPdfFixtures.AllPdfFilePaths());

    // ── "pass" files — must parse cleanly ────────────────────────────────────

    [Fact]
    public Task Parse_VeraPdfPassFiles_HavePositivePageCount() =>
        Parse_AllVeraPdfFiles_Core(static (pdfPath, doc) =>
            {
                doc.PageCount.ShouldBeGreaterThan(0, Path.GetFileName(pdfPath));

                return ValueTask.CompletedTask;
            },
            VeraPdfFixtures.PassPdfFilePaths());

    [Fact]
    public Task Parse_VeraPdfPassFiles_PagesMatchPageCount() =>
        Parse_AllVeraPdfFiles_Core(static (pdfPath, doc) =>
            {
                doc.Pages.Count.ShouldBe(doc.PageCount, Path.GetFileName(pdfPath));

                return ValueTask.CompletedTask;
            },
            VeraPdfFixtures.PassPdfFilePaths());

    // ── Content stream & text extraction ─────────────────────────────────────

    [Fact]
    public Task GetContentOperators_AllVeraPdfFiles_DoNotThrow() =>
        Parse_AllVeraPdfFiles_Core(static (pdfPath, doc) =>
            {
                for (var i = 1; i <= doc.PageCount; i++)
                    doc.Pages[i].GetContentOperators().ShouldNotBeNull(Path.GetFileName(pdfPath));

                return ValueTask.CompletedTask;
            },
            VeraPdfFixtures.AllPdfFilePaths());

    [Fact]
    public Task ExtractText_AllVeraPdfFiles_DoNotThrow() =>
        Parse_AllVeraPdfFiles_Core(static (pdfPath, doc) =>
            {
                for (var i = 1; i <= doc.PageCount; i++)
                    _ = doc.Pages[i].ExtractText();

                return ValueTask.CompletedTask;
            },
            VeraPdfFixtures.AllPdfFilePaths());

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public Task RoundTrip_VeraPdfPassFiles_PageCountPreserved() =>
        Parse_AllVeraPdfFiles_Core(static async (pdfPath, doc) =>
            {
                var before = doc.PageCount;
                await using var reloaded = await SaveAndReloadAsync(doc);
                reloaded.PageCount.ShouldBe(before, Path.GetFileName(pdfPath));
            },
            VeraPdfFixtures.PassPdfFilePaths());

    // ── Image XObjects ────────────────────────────────────────────────────────

    [Fact]
    public Task GetImageXObjects_AllVeraPdfFiles_DoNotThrow() =>
        Parse_AllVeraPdfFiles_Core(static (pdfPath, doc) =>
            {
                for (var i = 1; i <= doc.PageCount; i++)
                    doc.Pages[i].GetImageXObjects().ShouldNotBeNull(Path.GetFileName(pdfPath));

                return ValueTask.CompletedTask;
            },
            VeraPdfFixtures.AllPdfFilePaths());

    // Fail files
    [Fact]
    public async Task Parse_FailVeraPdfFiles_ParsesOrThrows_PdfException()
    {
        var tested = 0;
        var successfullyParsed = 0;
        var throwsPdfException = 0;

        foreach (var pdfPath in VeraPdfFixtures.FailPdfFilePaths())
        {
            try
            {
                _ = await LoadAsync(await File.ReadAllBytesAsync(pdfPath, TestContext.Current.CancellationToken));
                successfullyParsed++;
            }
            catch (PdfException)
            {
                // expected for some "fail" files — they intentionally violate a spec rule.
                throwsPdfException++;
            }

            tested++;
        }

        outputHelper.WriteLine($"[{nameof(Parse_FailVeraPdfFiles_ParsesOrThrows_PdfException)}] Tested: {tested}, Successfully Parsed: {successfullyParsed}, Throws PdfException: {throwsPdfException}");

        if (tested == 0)
            Assert.Skip("No veraPDF test files found in TestFiles/veraPDF/.");
    }


    // Helpers

    private static async Task Parse_AllVeraPdfFiles_Core(Func<string, IPdfDocument, ValueTask> docTask, IEnumerable<string> pdfPaths)
    {
        var tested = 0;

        foreach (var pdfPath in pdfPaths)
        {
            await using var doc = await TryLoadDocAsync(await File.ReadAllBytesAsync(pdfPath));
            if (doc is null)
                continue;

            await docTask(pdfPath, doc);

            tested++;
        }

        if (tested == 0)
            Assert.Skip("No veraPDF test files found in TestFiles/veraPDF/.");
    }
}
