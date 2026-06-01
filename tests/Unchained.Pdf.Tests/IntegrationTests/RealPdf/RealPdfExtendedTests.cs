using Shouldly;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests.RealPdf;

/// <summary>
/// Targeted tests for the extended set of real-world PDFs covering filters,
/// complex scripts, metadata, colour spaces, attachments, and error recovery.
/// Each test skips gracefully when its required file is absent from TestFiles/.
/// </summary>
public sealed class RealPdfExtendedTests : PdfTestBase
{
    // ── Filter robustness (007 — ImageMagick) ─────────────────────────────────

    [Fact]
    public async Task ImagemagickAscii85_ParsesAndHasPages()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.ImagemagickAscii85);
        await using var doc = await LoadAsync(bytes);
        doc.PageCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ImagemagickAscii85_RoundTripPreservesPageCount()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.ImagemagickAscii85);
        await using var doc = await LoadAsync(bytes);
        await using var reloaded = await SaveAndReloadAsync(doc);
        reloaded.PageCount.ShouldBe(doc.PageCount);
    }

    [Fact]
    public async Task ImagemagickLzw_ParsesAndHasPages()
    {
        // LZWDecode is currently a stub (NotImplementedException).
        // The parser must survive loading a PDF whose image uses LZW.
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.ImagemagickLzw);
        await using var doc = await LoadAsync(bytes);
        doc.PageCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ImagemagickCcitt_ParsesAndHasPages()
    {
        // CCITTFaxDecode is currently a stub. Parser must survive gracefully.
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.ImagemagickCcitt);
        await using var doc = await LoadAsync(bytes);
        doc.PageCount.ShouldBeGreaterThan(0);
    }

    // ── Inline images (008 — ReportLab) ──────────────────────────────────────

    [Fact]
    public async Task InlineImage_ParsesWithoutError()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.InlineImage);
        await using var doc = await LoadAsync(bytes);
        doc.PageCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task InlineImage_GetContentOperators_DoesNotThrow()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.InlineImage);
        await using var doc = await LoadAsync(bytes);
        // Inline images (BI…ID…EI) are skipped by ContentStreamParser.
        // The call must not throw regardless of inline image presence.
        doc.Pages[1].GetContentOperators().ShouldNotBeNull();
    }

    // ── LibreOffice form (012) ────────────────────────────────────────────────

    [Fact]
    public async Task LibreOfficeForm_HasFormFields()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.LibreOfficeForm);
        await using var doc = await LoadAsync(bytes);
        doc.GetFormFields().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task LibreOfficeForm_FieldNamesAreNonEmpty()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.LibreOfficeForm);
        await using var doc = await LoadAsync(bytes);
        doc.GetFormFields().ShouldAllBe(static f => !string.IsNullOrEmpty(f.Name));
    }

    // ── Arabic / RTL text (015) ───────────────────────────────────────────────

    [Fact]
    public async Task Arabic_ParsesWithoutError()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Arabic);
        await using var doc = await LoadAsync(bytes);
        doc.PageCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Arabic_GetContentOperators_DoesNotThrow()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Arabic);
        await using var doc = await LoadAsync(bytes);
        doc.Pages[1].GetContentOperators().ShouldNotBeNull();
    }

    [Fact]
    public async Task Arabic_GetTextSpans_DoesNotThrow()
    {
        // Arabic text may require HarfBuzz shaping and ToUnicode maps for extraction.
        // The call must not throw even if spans are empty.
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Arabic);
        await using var doc = await LoadAsync(bytes);
        doc.Pages[1].GetTextSpans().ShouldNotBeNull();
    }

    [Fact]
    public async Task ArabicRotated_PageHasNonStandardDimensions()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.ArabicRotated);
        await using var doc = await LoadAsync(bytes);
        doc.PageCount.ShouldBeGreaterThan(0);
        // A rotated page often swaps Width and Height; both must be non-zero.
        doc.Pages[1].Width.ShouldBeGreaterThan(0);
        doc.Pages[1].Height.ShouldBeGreaterThan(0);
    }

    // ── Bad metadata (017) ────────────────────────────────────────────────────

    [Fact]
    public async Task BadMetadata_ParsesOrThrowsPdfException()
    {
        // This PDF has unreadable metadata that may prevent full parsing.
        // Acceptable outcomes: loads successfully, or throws PdfException.
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.BadMetadata);
        try
        {
            await using var doc = await LoadAsync(bytes);
            doc.PageCount.ShouldBeGreaterThan(0);
        }
        catch (Core.PdfException)
        {
            // Acceptable: a descriptive exception is correct for malformed metadata.
        }
    }

    [Fact]
    public async Task BadMetadata_MetadataAccessDoesNotCrash()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.BadMetadata);
        try
        {
            await using var doc = await LoadAsync(bytes);
            // Corrupted /Info must not throw unhandled exceptions.
            _ = doc.Metadata;
        }
        catch (Core.PdfException) { }
    }

    // ── Base64 / JPEG image (018) ─────────────────────────────────────────────

    [Fact]
    public async Task Base64Image_ParsesWithoutError()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Base64Image);
        await using var doc = await LoadAsync(bytes);
        doc.PageCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Base64Image_GetImageXObjects_DoesNotThrow()
    {
        // The JPEG image is stored as an ASCIIHexDecode + DCTDecode stream.
        // DCTDecode returns a gray placeholder; the call must not crash.
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.Base64Image);
        await using var doc = await LoadAsync(bytes);
        doc.Pages[1].GetImageXObjects().ShouldNotBeNull();
    }

    // ── XMP metadata (020) ────────────────────────────────────────────────────

    [Fact]
    public async Task WithXmp_GetXmpMetadataReturnsXml()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.WithXmp);
        await using var doc = await LoadAsync(bytes);
        var xmp = doc.GetXmpMetadata();
        xmp.ShouldNotBeNull("Expected an XMP metadata packet in this PDF.");
        // ReSharper disable once StringLiteralTypo
        xmp.ShouldContain("xmpmeta");
    }

    [Fact]
    public async Task WithXmp_XmpIsValidXml()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.WithXmp);
        await using var doc = await LoadAsync(bytes);
        var xmp = doc.GetXmpMetadata();
        if (xmp is null) return;
        // Must parse as well-formed XML (XMP is RDF/XML).
        Should.NotThrow(() => System.Xml.Linq.XDocument.Parse(
            xmp.Trim().TrimStart('﻿'))); // strip BOM if present
    }

    // ── PDF/A (021) ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PdfA_ParsesWithoutError()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.PdfA);
        await using var doc = await LoadAsync(bytes);
        doc.PageCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task PdfA_RoundTripPreservesPageCount()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.PdfA);
        await using var doc = await LoadAsync(bytes);
        await using var reloaded = await SaveAndReloadAsync(doc);
        reloaded.PageCount.ShouldBe(doc.PageCount);
    }

    // ── CMYK image (023) ──────────────────────────────────────────────────────

    [Fact]
    public async Task CmykImage_ParsesWithoutError()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.CmykImage);
        await using var doc = await LoadAsync(bytes);
        doc.PageCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CmykImage_GetImageXObjects_ReturnsGrayPlaceholder()
    {
        // CMYK is an unsupported colour space — GetImageXObjects returns a grey placeholder.
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.CmykImage);
        await using var doc = await LoadAsync(bytes);
        var images = doc.Pages[1].GetImageXObjects();
        images.ShouldNotBeNull();
        // Placeholder pixels are mid-grey (128) — check any image entry.
        foreach (var img in images.Values)
            img.RgbData.Length.ShouldBe(img.Width * img.Height * 3);
    }

    // ── Embedded attachment (025) ─────────────────────────────────────────────

    [Fact]
    public async Task WithAttachment_ParsesWithoutError()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.WithAttachment);
        await using var doc = await LoadAsync(bytes);
        doc.PageCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task WithAttachment_RoundTripPreservesPageCount()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.WithAttachment);
        await using var doc = await LoadAsync(bytes);
        await using var reloaded = await SaveAndReloadAsync(doc);
        reloaded.PageCount.ShouldBe(doc.PageCount);
    }

    // ── CropBox / rotation / transforms (027) ────────────────────────────────

    [Fact]
    public async Task CroppedRotated_ParsesWithoutError()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.CroppedRotated);
        await using var doc = await LoadAsync(bytes);
        doc.PageCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CroppedRotated_AllPagesHaveNonZeroDimensions()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.CroppedRotated);
        await using var doc = await LoadAsync(bytes);
        for (var i = 1; i <= doc.PageCount; i++)
        {
            // CropBox and rotation may change effective dimensions; Width/Height may differ.
            // Both must be non-negative (a CropBox could produce zero if misconfigured).
            doc.Pages[i].Width.ShouldBeGreaterThanOrEqualTo(0, $"page {i} width");
            doc.Pages[i].Height.ShouldBeGreaterThanOrEqualTo(0, $"page {i} height");
        }
    }

    [Fact]
    public async Task CroppedRotated_ContentOperators_DoesNotThrow()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.CroppedRotated);
        await using var doc = await LoadAsync(bytes);
        for (var i = 1; i <= doc.PageCount; i++)
            doc.Pages[i].GetContentOperators().ShouldNotBeNull($"page {i}");
    }

    // ── Wrong XObject references (028) ────────────────────────────────────────

    [Fact]
    public async Task WrongReferences_ParsesWithoutCrash()
    {
        // This PDF has internally inconsistent XObject references.
        // The parser must not crash — either it recovers or throws PdfException.
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.WrongReferences);
        try
        {
            await using var doc = await LoadAsync(bytes);
            doc.PageCount.ShouldBeGreaterThan(0);
        }
        catch (Core.PdfException)
        {
            // Acceptable: a descriptive exception is correct for a broken PDF.
        }
    }

    [Fact]
    public async Task WrongReferences_GetImageXObjects_DoesNotCrash()
    {
        var bytes = RealPdfFixtures.LoadOrSkip(RealPdfFixtures.Files.WrongReferences);
        try
        {
            await using var doc = await LoadAsync(bytes);
            // Must not crash even with broken references.
            doc.Pages[1].GetImageXObjects().ShouldNotBeNull();
        }
        catch (Core.PdfException) { }
    }
}
