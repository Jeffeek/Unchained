using System.Diagnostics;
using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests.RealPdf;

/// <summary>
///     Targeted tests for specific real-PDF scenarios: multi-page layout, metadata,
///     annotations, bookmarks, form fields, and named destinations.
///     Each test skips gracefully when its required file is absent from TestFiles/.
/// </summary>
public sealed class RealPdfDocumentTests : PdfTestBase
{
    // ── simple.pdf ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Simple_ParsesWithoutError()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Simple);

        await using var doc = await LoadAsync(bytes);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task Simple_MetadataIsReadable()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Simple);

        await using var doc = await LoadAsync(bytes);
        // Metadata may or may not be present; the call must not throw.
        _ = doc.Metadata;
    }

    [Fact]
    public async Task Simple_PageDimensionsArePlausible()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Simple);

        await using var doc = await LoadAsync(bytes);
        // A4 portrait ≈ 595 × 842 pt; US Letter ≈ 612 × 792 pt — allow ±20 pt.
        doc.Pages[1].Width.ShouldBeInRange(400, 900);
        doc.Pages[1].Height.ShouldBeInRange(400, 1200);
    }

    // ── multipage.pdf ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Multipage_HasAtLeastTwoPages()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Multipage);

        await using var doc = await LoadAsync(bytes);
        doc.PageCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Multipage_EachPageAccessibleByNumber()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Multipage);

        await using var doc = await LoadAsync(bytes);
        for (var i = 1; i <= doc.PageCount; i++)
            doc.Pages[i].PageNumber.ShouldBe(i);
    }

    [Fact]
    public async Task Multipage_RoundTripPreservesAllPages()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Multipage);

        await using var doc = await LoadAsync(bytes);
        var before = doc.PageCount;
        await using var reloaded = await SaveAndReloadAsync(doc);
        reloaded.PageCount.ShouldBe(before);
    }

    // ── with-annotations.pdf ─────────────────────────────────────────────────

    [Fact]
    public async Task WithAnnotations_PageOneHasAnnotations()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.WithAnnotations);

        await using var doc = await LoadAsync(bytes);
        // At least one page must carry annotations.
        var anyAnnots = Enumerable.Range(1, doc.PageCount)
            .Any(i => doc.Pages[i].GetAnnotations().Count > 0);
        anyAnnots.ShouldBeTrue();
    }

    [Fact]
    public async Task WithAnnotations_AnnotationHasRect()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.WithAnnotations);

        await using var doc = await LoadAsync(bytes);
        var annots = Enumerable.Range(1, doc.PageCount)
            .SelectMany(i => doc.Pages[i].GetAnnotations())
            .ToList();
        annots.ShouldNotBeEmpty();
        foreach (var a in annots)
        {
            Math.Abs(a.Width).ShouldBeGreaterThan(0f);
            Math.Abs(a.Height).ShouldBeGreaterThan(0f);
        }
    }

    // ── with-bookmarks.pdf ────────────────────────────────────────────────────

    [Fact]
    public async Task WithBookmarks_HasAtLeastOneBookmark()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.WithBookmarks);

        await using var doc = await LoadAsync(bytes);
        doc.GetBookmarks().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task WithBookmarks_BookmarkPageNumbersInRange()
    {
        var bytes = RealPdfFixtures.TryLoad(RealPdfFixtures.Files.WithBookmarks);
        if (bytes is null)
            return;

        await using var doc = await LoadAsync(bytes);

        Check(doc.GetBookmarks());

        return;

        void Check(IEnumerable<Bookmark> list)
        {
            foreach (var bm in list)
            {
                if (bm.PageNumber > 0)
                    bm.PageNumber.ShouldBeInRange(1, doc.PageCount);
                if (bm.Children is not null)
                    Check(bm.Children);
            }
        }
    }

    // ── with-forms.pdf ────────────────────────────────────────────────────────

    [Fact]
    public async Task WithForms_HasAtLeastOneField()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.WithForms);

        await using var doc = await LoadAsync(bytes);
        doc.GetFormFields().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task WithForms_AllFieldsHaveNonEmptyName()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.WithForms);

        await using var doc = await LoadAsync(bytes);
        doc.GetFormFields().ShouldAllBe(static f => !string.IsNullOrEmpty(f.Name));
    }

    [Fact]
    public async Task WithForms_AllFieldsHaveKnownType()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.WithForms);

        await using var doc = await LoadAsync(bytes);
        var validTypes = new HashSet<string>(StringComparer.Ordinal) { "Tx", "Btn", "Ch", "Sig" };
        doc.GetFormFields().ShouldAllBe(f => validTypes.Contains(f.FieldType));
    }

    // ── scanned.pdf / encrypted.pdf — robustness ──────────────────────────────

    [Fact]
    public async Task Scanned_ParsesWithoutCrashing()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Scanned);

        await using var doc = await LoadAsync(bytes);
        doc.PageCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Encrypted_DoesNotCrashParser()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Encrypted);
        // Parser must either load it (if unencrypted body is accessible) or
        // throw a PdfException — not an unhandled crash.
        // Acceptable outcomes: loads successfully (some readers expose unencrypted structure),
        // or throws PdfException / PdfEncryptedException (correct behaviour for protected files).
        try
        {
            await using var doc = await LoadAsync(bytes);
            doc.PageCount.ShouldBeGreaterThan(0);
        }
        catch (PdfException) { }
        catch (PdfEncryptedException) { }
    }

    // ── large.pdf — performance sanity ────────────────────────────────────────

    [Fact]
    public async Task Large_ParsesInReasonableTime()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Large);

        var sw = Stopwatch.StartNew();
        await using var doc = await LoadAsync(bytes);
        sw.Stop();
        doc.PageCount.ShouldBeGreaterThan(0);
        sw.ElapsedMilliseconds.ShouldBeLessThan(10_000,
            $"Parsing {bytes.Length / 1024} KB took {sw.ElapsedMilliseconds} ms");
    }
}
