using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
/// Tests for PDF/A conformance validation and conversion.
/// Uses both synthetic PDFs and the veraPDF test corpus where available.
/// </summary>
public sealed class PdfATests : PdfTestBase
{
    // ── Validation — synthetic documents ─────────────────────────────────────

    [Fact]
    public async Task Validate_UnencryptedSimplePdf_ReturnsResult()
    {
        // Any non-encrypted PDF should return a result (may have violations).
        var result = await Processor.ValidatePdfAAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Profile.ShouldBe(PdfAProfile.PdfA1B);
    }

    [Fact]
    public async Task Validate_EncryptedPdf_ReportsEncryptionViolation()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, new SaveOptions(Encryption: new EncryptionOptions(UserPassword: "pw")), ct: TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfAAsync(ms.ToArray(), ct: TestContext.Current.CancellationToken);

        // ReSharper disable once StringLiteralTypo
        result.Errors.ShouldContain(static v => v.RuleId == "6.1.3" && v.Description.Contains("ncrypt"), "Encryption must be reported as a §6.1.3 violation.");
    }

    [Fact]
    public async Task Validate_MissingFileId_ReportsViolation()
    {
        // A freshly-created in-memory PDF (via format imports) has no /ID.
        await using var doc = await Processor.LoadFromTxtAsync("hello", ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfAAsync(ms.ToArray(), ct: TestContext.Current.CancellationToken);
        result.Violations.ShouldContain(static v => v.RuleId == "6.1.3", "Missing /ID must be reported.");
    }

    [Fact]
    public async Task Validate_MissingXmpMetadata_ReportsViolation()
    {
        var result = await Processor.ValidatePdfAAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        result.Violations.ShouldContain(static v => v.RuleId == "6.7.2", "Missing /Metadata must be reported.");
    }

    [Fact]
    public async Task Validate_AllErrorsHaveRuleId()
    {
        var result = await Processor.ValidatePdfAAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        result.Violations.ShouldAllBe(static v => !string.IsNullOrWhiteSpace(v.RuleId), "Every violation must have a rule ID.");
    }

    // ── Conversion ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConvertToPdfA_ProducesLoadablePdf()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        await using var reloaded = await LoadAsync(ms.ToArray(), ct: TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task ConvertToPdfA_AddsXmpWithPdfaidProperties()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        await using var reloaded = await LoadAsync(ms.ToArray(), ct: TestContext.Current.CancellationToken);
        var xmp = reloaded.GetXmpMetadata();
        xmp.ShouldNotBeNull("Converted document must have XMP metadata.");
        xmp.ShouldContain("pdfaid", caseSensitivity: Case.Insensitive);
    }

    [Fact]
    public async Task ConvertToPdfA_AddsPdfaidPart1AndConformanceB()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms, profile: PdfAProfile.PdfA1B, ct: TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfAAsync(ms.ToArray(), profile: PdfAProfile.PdfA1B, ct: TestContext.Current.CancellationToken);

        // After conversion, the XMP violation should be resolved
        result.Errors.ShouldNotContain(static v => v.RuleId == "6.7.2", "pdfaid XMP properties should be present after conversion.");
    }

    [Fact]
    public async Task ConvertToPdfA_AddsFileId()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(count: 2), ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfAAsync(ms.ToArray(), ct: TestContext.Current.CancellationToken);

        result.Errors.ShouldNotContain(static v => v.RuleId == "6.1.3" && v.Description.Contains("ID"), "/ID should be present after conversion.");
    }

    [Fact]
    public async Task ConvertToPdfA_ThenValidate_FewerErrorsThanOriginal()
    {
        var original = PdfFixtures.SinglePage();
        var originalResult = await Processor.ValidatePdfAAsync(original, ct: TestContext.Current.CancellationToken);

        await using var doc = await LoadAsync(original, ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        var convertedResult = await Processor.ValidatePdfAAsync(ms.ToArray(), ct: TestContext.Current.CancellationToken);

        convertedResult.Errors.Count.ShouldBeLessThan(originalResult.Errors.Count, "Conversion must resolve at least some violations.");
    }

    [Fact]
    public async Task ConvertToPdfA_IsIdempotent()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        using var ms1 = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms1, ct: TestContext.Current.CancellationToken);

        await using var doc2 = await LoadAsync(ms1.ToArray(), ct: TestContext.Current.CancellationToken);
        using var ms2 = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc2, ms2, ct: TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfAAsync(ms2.ToArray(), ct: TestContext.Current.CancellationToken);
        // Second conversion should not add duplicate pdfaid properties
        result.Errors.ShouldNotContain(static v => v.RuleId == "6.7.2");
    }

    [Fact]
    public async Task ConvertToPdfA_Encrypted_Throws()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        using var encMs = new MemoryStream();
        await Processor.SaveAsync(doc, encMs, new SaveOptions(Encryption: new EncryptionOptions(UserPassword: "pw")), ct: TestContext.Current.CancellationToken);

        await using var encDoc = await Processor.LoadAsync(new MemoryStream(encMs.ToArray()), "pw", ct: TestContext.Current.CancellationToken);
        using var outMs = new MemoryStream();

        await Should.ThrowAsync<InvalidOperationException>(() => Processor.ConvertToPdfAAsync(encDoc, outMs));
    }

    // ── veraPDF corpus ────────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_VeraPdfPassFiles_LessViolationsThanFailFiles()
    {
        // "pass" files in the veraPDF corpus should be closer to conformant than "fail" files.
        var passFiles = VeraPdfFixtures.PassPdfFilePaths().Take(10).ToList();
        var failFiles = VeraPdfFixtures.FailPdfFilePaths().Take(10).ToList();

        if (passFiles.Count == 0 || failFiles.Count == 0)
        {
            Assert.Skip("veraPDF test files not available in TestFiles/veraPDF/.");
            return;
        }

        double avgPassErrors = 0, avgFailErrors = 0;
        var passCount = 0;

        foreach (var path in passFiles)
        {
            var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            var r = await Processor.ValidatePdfAAsync(bytes, ct: TestContext.Current.CancellationToken);
            avgPassErrors += r.Errors.Count;
            passCount++;
        }

        var failCount = 0;
        foreach (var path in failFiles)
        {
            var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            var r = await Processor.ValidatePdfAAsync(bytes, ct: TestContext.Current.CancellationToken);
            avgFailErrors += r.Errors.Count;
            failCount++;
        }

        if (passCount > 0 && failCount > 0)
        {
            avgPassErrors /= passCount;
            avgFailErrors /= failCount;
            // Pass files should on average have fewer errors (they're spec-conforming PDFs)
            avgPassErrors.ShouldBeLessThanOrEqualTo(avgFailErrors, $"Average errors: pass={avgPassErrors:F1} vs fail={avgFailErrors:F1}");
        }
    }

    [Fact]
    public async Task Validate_AllVeraPdfFiles_DoNotCrash()
    {
        var paths = VeraPdfFixtures.AllPdfFilePaths().Take(30).ToList();
        if (paths.Count == 0)
        {
            Assert.Skip("veraPDF test files not available in TestFiles/veraPDF/.");
            return;
        }

        foreach (var path in paths)
        {
            var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            // Must not throw — encrypted PDFs or broken ones should return results gracefully
            var result = await Processor.ValidatePdfAAsync(bytes, ct: TestContext.Current.CancellationToken);
            result.ShouldNotBeNull(Path.GetFileName(path));
        }
    }
}
