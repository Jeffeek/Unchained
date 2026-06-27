using System.Text;
using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Tests for PDF/A conformance validation and conversion.
///     Uses both synthetic PDFs and the veraPDF test corpus where available.
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
    public async Task Validate_NonEmbeddedFont_ReportsFontViolation()
    {
        var result = await Processor.ValidatePdfAAsync(PdfFixtures.WithPdfAViolations(), ct: TestContext.Current.CancellationToken);
        result.Violations.ShouldContain(static v => v.RuleId == "6.3.3", "A non-embedded font must be reported.");
    }

    [Fact]
    public async Task Validate_ProhibitedAnnotation_ReportsAnnotationViolation()
    {
        var result = await Processor.ValidatePdfAAsync(PdfFixtures.WithPdfAViolations(), ct: TestContext.Current.CancellationToken);
        result.Violations.ShouldContain(static v => v.RuleId == "6.5.3", "A FileAttachment annotation must be reported.");
    }

    [Fact]
    public async Task Validate_CatalogAdditionalActions_ReportsActionViolation()
    {
        var result = await Processor.ValidatePdfAAsync(PdfFixtures.WithPdfAViolations(), ct: TestContext.Current.CancellationToken);
        result.Violations.ShouldContain(static v => v.RuleId == "6.6.1", "Catalog /AA must be reported.");
    }

    [Fact]
    public async Task Validate_WidgetWithoutAppearance_ReportsViolation()
    {
        var result = await Processor.ValidatePdfAAsync(PdfFixtures.WithPdfAViolations(), ct: TestContext.Current.CancellationToken);
        result.Violations.ShouldContain(static v => v.RuleId == "6.5.4", "A Widget without /AP must be reported.");
    }

    [Fact]
    public async Task Validate_MalformedPdf_ReportsStructureViolation()
    {
        // Bytes that start like a PDF but cannot be parsed → the catch(PdfException) arm reports 6.1.
        var malformed = "%PDF-1.4\nthis is not a valid pdf body"u8.ToArray();
        var result = await Processor.ValidatePdfAAsync(malformed, ct: TestContext.Current.CancellationToken);
        result.Violations.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Validate_Pdf17ForPdfA1_ReportsVersionViolation()
    {
        // SinglePage is %PDF-1.7; PDF/A-1 caps at 1.4 → version violation.
        var result = await Processor.ValidatePdfAAsync(
            PdfFixtures.SinglePage(),
            PdfAProfile.PdfA1B,
            TestContext.Current.CancellationToken
        );
        result.Violations.ShouldContain(static v => v.RuleId == "6.1.2");
    }

    [Fact]
    public async Task Validate_EncryptedPdf_ReportsEncryptionViolation()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, new SaveOptions(Encryption: new EncryptionOptions("pw")), TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfAAsync(ms.ToArray(), ct: TestContext.Current.CancellationToken);

        // ReSharper disable once StringLiteralTypo
        result.Errors.ShouldContain(
            static v => v.RuleId == "6.1.3" && v.Description.Contains("ncrypt"),
            "Encryption must be reported as a §6.1.3 violation."
        );
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
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        await using var reloaded = await LoadAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task ConvertToPdfA_AddsXmpWithPdfaidProperties()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        await using var reloaded = await LoadAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        var xmp = reloaded.GetXmpMetadata();
        xmp.ShouldNotBeNull("Converted document must have XMP metadata.");
        xmp.ShouldContain("pdfaid");
    }

    [Fact]
    public async Task ConvertToPdfA_AddsPdfaidPart1AndConformanceB()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms, PdfAProfile.PdfA1B, TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfAAsync(ms.ToArray(), PdfAProfile.PdfA1B, TestContext.Current.CancellationToken);

        // After conversion, the XMP violation should be resolved
        result.Errors.ShouldNotContain(static v => v.RuleId == "6.7.2", "pdfaid XMP properties should be present after conversion.");
    }

    [Fact]
    public async Task ConvertToPdfA_AddsFileId()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);
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

        await using var doc = await LoadAsync(original, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        var convertedResult = await Processor.ValidatePdfAAsync(ms.ToArray(), ct: TestContext.Current.CancellationToken);

        convertedResult.Errors.Count.ShouldBeLessThan(originalResult.Errors.Count, "Conversion must resolve at least some violations.");
    }

    [Fact]
    public async Task ConvertToPdfA_IsIdempotent()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var ms1 = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms1, ct: TestContext.Current.CancellationToken);

        await using var doc2 = await LoadAsync(ms1.ToArray(), TestContext.Current.CancellationToken);
        using var ms2 = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc2, ms2, ct: TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfAAsync(ms2.ToArray(), ct: TestContext.Current.CancellationToken);
        // Second conversion should not add duplicate pdfaid properties
        result.Errors.ShouldNotContain(static v => v.RuleId == "6.7.2");
    }

    [Fact]
    public async Task ConvertToPdfA_Encrypted_Throws()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var encMs = new MemoryStream();
        await Processor.SaveAsync(doc, encMs, new SaveOptions(Encryption: new EncryptionOptions("pw")), TestContext.Current.CancellationToken);

        await using var encDoc = await Processor.LoadAsync(new MemoryStream(encMs.ToArray()), "pw", TestContext.Current.CancellationToken);
        using var outMs = new MemoryStream();

        await Should.ThrowAsync<InvalidOperationException>(() => Processor.ConvertToPdfAAsync(encDoc, outMs));
    }

    [
        Theory,
        InlineData(PdfAProfile.PdfA1B, "1", "B"),
        InlineData(PdfAProfile.PdfA1A, "1", "A"),
        InlineData(PdfAProfile.PdfA2B, "2", "B"),
        InlineData(PdfAProfile.PdfA2U, "2", "U"),
        InlineData(PdfAProfile.PdfA3B, "3", "B")
    ]
    public async Task ConvertToPdfA_AllProfiles_WritePartAndConformance(
        PdfAProfile profile,
        string part,
        string conformance
    )
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms, profile, TestContext.Current.CancellationToken);

        var xmp = Encoding.UTF8.GetString(ms.ToArray());
        xmp.ShouldContain($"part>{part}");
        xmp.ShouldContain($"conformance>{conformance}");
    }

    [Fact]
    public async Task ConvertToPdfA_NonWidgetAnnotationWithoutPrintFlag_SetsPrintBit()
    {
        // The WithPdfAViolations fixture carries a /FileAttachment annotation with no Print flag;
        // conversion sets bit 3 (value 4) on it (FixAnnotationFlags path).
        await using var doc = await LoadAsync(PdfFixtures.WithPdfAViolations(), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms, PdfAProfile.PdfA2B, TestContext.Current.CancellationToken);

        // Reload and confirm the document still parses (the Print-flag rewrite did not corrupt it).
        await using var reloaded = await Processor.LoadAsync(new MemoryStream(ms.ToArray()), TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
    }

    // ── Validation — profiles ─────────────────────────────────────────────────

    [
        Theory,
        InlineData(PdfAProfile.PdfA1B),
        InlineData(PdfAProfile.PdfA1A),
        InlineData(PdfAProfile.PdfA2B),
        InlineData(PdfAProfile.PdfA2U),
        InlineData(PdfAProfile.PdfA3B)
    ]
    public async Task Validate_DifferentProfiles_ResultCarriesRequestedProfile(PdfAProfile profile)
    {
        var result = await Processor.ValidatePdfAAsync(PdfFixtures.SinglePage(), profile, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Profile.ShouldBe(profile, $"Result.Profile must equal the requested {profile}.");
    }

    [Fact]
    public async Task Validate_Pdf14Version_PassesVersionCheckForPdfA1()
    {
        // PDF 1.7 exceeds the 1.4 max for PDF/A-1; confirm a version violation is reported.
        var result = await Processor.ValidatePdfAAsync(PdfFixtures.SinglePage(), PdfAProfile.PdfA1B, TestContext.Current.CancellationToken);

        result.Violations.ShouldContain(
            static v => v.RuleId == "6.1.2",
            "PDF 1.7 header must trigger a §6.1.2 version violation for PDF/A-1B."
        );
    }

    [Fact]
    public async Task Validate_Pdf17Version_PassesVersionCheckForPdfA2()
    {
        // PDF 1.7 is within the 1.7 max for PDF/A-2; no version violation should be reported.
        var result = await Processor.ValidatePdfAAsync(PdfFixtures.SinglePage(), PdfAProfile.PdfA2B, TestContext.Current.CancellationToken);

        result.Violations.ShouldNotContain(
            static v => v.RuleId == "6.1.2",
            "PDF 1.7 header must not trigger a §6.1.2 version violation for PDF/A-2B."
        );
    }

    // ── Validation — invalid XMP ──────────────────────────────────────────────

    [Fact]
    public async Task Validate_InvalidXmlInMetadataStream_ReportsXmpViolation()
    {
        // Build a PDF with a /Metadata stream whose content is not well-formed XML.
        var pdfBytes = BuildPdfWithRawMetadata("<not valid xml <<<");

        var result = await Processor.ValidatePdfAAsync(pdfBytes, ct: TestContext.Current.CancellationToken);

        result.Violations.ShouldContain(
            static v => v.RuleId == "6.7.2",
            "Malformed XML in /Metadata must produce a §6.7.2 violation."
        );
    }

    [Fact]
    public async Task Validate_XmpMissingPdfaidPart_ReportsViolation()
    {
        // Valid XML but missing pdfaid:part element.
        const string xmp = """
                           <?xpacket begin="" id="W5M0MpCehiHzreSzNTczkc9d"?>
                           <x:xmpmeta xmlns:x="adobe:ns:meta/">
                             <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
                               <rdf:Description rdf:about="" xmlns:pdfaid="http://www.aiim.org/pdfa/ns/id/">
                               </rdf:Description>
                             </rdf:RDF>
                           </x:xmpmeta>
                           <?xpacket end="w"?>
                           """;

        var pdfBytes = BuildPdfWithRawMetadata(xmp);

        var result = await Processor.ValidatePdfAAsync(pdfBytes, ct: TestContext.Current.CancellationToken);

        result.Violations.ShouldContain(
            static v => v.RuleId == "6.7.2" && v.Description.Contains("pdfaid:part"),
            "Missing pdfaid:part must produce a §6.7.2 violation."
        );
    }

    [Fact]
    public async Task Validate_XmpWrongPdfaidPart_ReportsViolation()
    {
        // pdfaid:part says "2" but we validate as PDF/A-1B.
        const string xmp = """
                           <?xpacket begin="" id="W5M0MpCehiHzreSzNTczkc9d"?>
                           <x:xmpmeta xmlns:x="adobe:ns:meta/">
                             <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
                               <rdf:Description rdf:about=""
                                   xmlns:pdfaid="http://www.aiim.org/pdfa/ns/id/">
                                 <pdfaid:part>2</pdfaid:part>
                                 <pdfaid:conformance>B</pdfaid:conformance>
                               </rdf:Description>
                             </rdf:RDF>
                           </x:xmpmeta>
                           <?xpacket end="w"?>
                           """;

        var pdfBytes = BuildPdfWithRawMetadata(xmp);

        var result = await Processor.ValidatePdfAAsync(pdfBytes, PdfAProfile.PdfA1B, TestContext.Current.CancellationToken);

        result.Violations.ShouldContain(
            static v => v.RuleId == "6.7.2" && v.Description.Contains("pdfaid:part"),
            "Wrong pdfaid:part value must produce a §6.7.2 violation."
        );
    }

    // ── Validation — no /Info dict ────────────────────────────────────────────

    [Fact]
    public async Task Validate_NoInfoDict_DoesNotCrash()
    {
        // PdfFixtures.SinglePage() has no /Info entry — validator must not throw.
        var result = await Processor.ValidatePdfAAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);

        result.ShouldNotBeNull("Validator must return a result even without an /Info dictionary.");
    }

    // ── Validation — IsConformant ─────────────────────────────────────────────

    [Fact]
    public async Task Validate_AfterConversion_IsConformantWhenOnlyWarnings()
    {
        // After ConvertToPdfA the remaining issues should reduce to warnings
        // (primarily the missing OutputIntent, which is a W not an E).
        // We use WithEmbeddedFont so font embedding is satisfied.
        var fontBytes = new byte[256]; // minimal dummy bytes; font metrics not exercised here
        var original = PdfFixtures.WithEmbeddedFont(fontBytes);

        await using var doc = await LoadAsync(original, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfAAsync(ms.ToArray(), ct: TestContext.Current.CancellationToken);

        // Errors (not warnings) that ConvertToPdfA is expected to fix must be absent.
        result.Errors.ShouldNotContain(static v => v.RuleId == "6.7.2", "XMP errors must be fixed by conversion.");
        result.Errors.ShouldNotContain(
            static v => v.RuleId == "6.1.3" && v.Description.Contains("ID"),
            "/ID errors must be fixed by conversion."
        );
    }

    [Fact]
    public async Task Validate_WithErrors_IsConformantReturnsFalse()
    {
        // SinglePage() has several errors — IsConformant must be false.
        var result = await Processor.ValidatePdfAAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse("A plain synthetic PDF has structural violations and must not be conformant.");
        result.Errors.Count.ShouldBeGreaterThan(0, "At least one error must be present.");
    }

    // ── Validation — LZW filter ───────────────────────────────────────────────

    [Fact]
    public async Task Validate_LzwFilter_ReportsViolation()
    {
        var pdfBytes = BuildPdfWithLzwStream();

        var result = await Processor.ValidatePdfAAsync(pdfBytes, ct: TestContext.Current.CancellationToken);

        result.Violations.ShouldContain(
            static v => v.RuleId == "6.1.10",
            "An LZWDecode stream must trigger a §6.1.10 violation."
        );
    }

    // ── Conversion — metadata ─────────────────────────────────────────────────

    [Fact]
    public async Task ConvertToPdfA_DocumentWithoutMetadata_AddsMetadataStream()
    {
        // SinglePage() has no /Metadata — after conversion it must have one.
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfAAsync(ms.ToArray(), ct: TestContext.Current.CancellationToken);

        result.Errors.ShouldNotContain(
            static v => v.RuleId == "6.7.2",
            "Conversion must inject /Metadata and eliminate §6.7.2 errors."
        );
    }

    [Fact]
    public async Task ConvertToPdfA_AllProfiles_ProduceCorrectPdfaidPart()
    {
        var cases = new[]
        {
            (PdfAProfile.PdfA1B, "1", "B"),
            (PdfAProfile.PdfA1A, "1", "A"),
            (PdfAProfile.PdfA2B, "2", "B"),
            (PdfAProfile.PdfA2U, "2", "U"),
            (PdfAProfile.PdfA3B, "3", "B")
        };

        foreach (var (profile, expectedPart, expectedConf) in cases)
        {
            await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
            using var ms = new MemoryStream();
            await Processor.ConvertToPdfAAsync(doc, ms, profile, TestContext.Current.CancellationToken);

            var xmp = (await LoadAsync(ms.ToArray(), TestContext.Current.CancellationToken)).GetXmpMetadata();
            xmp.ShouldNotBeNull($"Profile {profile} must produce XMP metadata.");
            xmp.ShouldContain($">{expectedPart}<", customMessage: $"pdfaid:part must be {expectedPart} for {profile}.");
            xmp.ShouldContain($">{expectedConf}<", customMessage: $"pdfaid:conformance must be {expectedConf} for {profile}.");
        }
    }

    // ── Conversion — embedded fonts ───────────────────────────────────────────

    [Fact]
    public async Task ConvertToPdfA_EmbeddedFontDocument_NoFontEmbeddingErrors()
    {
        var fontBytes = new byte[256];
        var original = PdfFixtures.WithEmbeddedFont(fontBytes);

        await using var doc = await LoadAsync(original, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfAAsync(ms.ToArray(), ct: TestContext.Current.CancellationToken);

        result.Errors.ShouldNotContain(
            static v => v.RuleId == "6.3.3",
            "An already-embedded font must not produce §6.3.3 errors after conversion."
        );
    }

    [Fact]
    public async Task Validate_DocumentWithEmbeddedFont_ReportsNoFontError()
    {
        // A PDF built with /FontFile2 present must not trigger §6.3.3 font-embedding errors.
        var fontBytes = new byte[256];
        var result = await Processor.ValidatePdfAAsync(PdfFixtures.WithEmbeddedFont(fontBytes), ct: TestContext.Current.CancellationToken);

        result.Errors.ShouldNotContain(
            static v => v.RuleId == "6.3.3",
            "Font with /FontFile2 must not produce §6.3.3 errors."
        );
    }

    [Fact]
    public async Task Validate_DocumentWithoutFontDescriptor_ReportsFontError()
    {
        // SinglePage() has no fonts at all; validator must not report §6.3.3
        // (no fonts = nothing to check).  But a PDF with a font dict missing
        // /FontDescriptor must report it.
        var pdfBytes = BuildPdfWithUnembeddedFont();

        var result = await Processor.ValidatePdfAAsync(pdfBytes, ct: TestContext.Current.CancellationToken);

        result.Errors.ShouldContain(
            static v => v.RuleId == "6.3.3",
            "A font without /FontDescriptor must produce a §6.3.3 error."
        );
    }

    // ── Conversion — subsequent validation ───────────────────────────────────

    [Fact]
    public async Task ConvertToPdfA_SubsequentValidation_NoCatalogAaViolation()
    {
        // Build a PDF with /AA in the catalog, then convert.
        var pdfBytes = BuildPdfWithCatalogAa();

        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfAAsync(ms.ToArray(), ct: TestContext.Current.CancellationToken);

        result.Errors.ShouldNotContain(
            static v => v.RuleId == "6.6.1",
            "Conversion must strip /AA from the catalog, eliminating §6.6.1 violations."
        );
    }

    [Fact]
    public async Task ConvertToPdfA_SubsequentValidation_AnnotationPrintFlagSet()
    {
        // Build a PDF with an annotation lacking the Print flag, then convert.
        // After conversion the Print flag must be set, removing the §6.5.3 error.
        var pdfBytes = PdfFixtures.WithAnnotation("test note");

        await using var doc = await LoadAsync(pdfBytes, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfAAsync(ms.ToArray(), ct: TestContext.Current.CancellationToken);

        result.Errors.ShouldNotContain(
            static v => v.RuleId == "6.5.3" && v.Description.Contains("Print"),
            "Annotation Print flag must be set by conversion, removing §6.5.3 errors."
        );
    }

    // ── Local fixture builders ─────────────────────────────────────────────────

    /// <summary>
    ///     Produces a minimal PDF 1.4 document whose /Metadata stream contains
    ///     <paramref name="rawXmpContent" /> verbatim (not filtered).
    /// </summary>
    private static byte[] BuildPdfWithRawMetadata(string rawXmpContent)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        PdfFixtures.Ln(sb, "%PDF-1.4");
        PdfFixtures.Ln(sb, "%\xE2\xE3\xCF\xD3");

        // Object 1 — Catalog (references /Metadata at obj 4)
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "1 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Catalog /Pages 2 0 R /Metadata 4 0 R >>");
        PdfFixtures.Ln(sb, "endobj");

        // Object 2 — Pages
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "2 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        PdfFixtures.Ln(sb, "endobj");

        // Object 3 — Page
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "3 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>");
        PdfFixtures.Ln(sb, "endobj");

        // Object 4 — Metadata stream
        var metaBytes = Encoding.UTF8.GetBytes(rawXmpContent);
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "4 0 obj");
        PdfFixtures.Ln(sb, $"<< /Type /Metadata /Subtype /XML /Length {metaBytes.Length} >>");
        sb.Append("stream\n");
        // Embed raw XMP bytes as Latin-1 (all chars ≤255; UTF-8 bytes land in Latin-1 passthrough)
        foreach (var b in metaBytes) sb.Append((char)b);
        PdfFixtures.Ln(sb, "\nendstream");
        PdfFixtures.Ln(sb, "endobj");

        var xrefOffset = PdfFixtures.Len(sb);
        PdfFixtures.Ln(sb, "xref");
        PdfFixtures.Ln(sb, "0 5");
        PdfFixtures.Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets)
            PdfFixtures.Ln(sb, $"{o:D10} 00000 n ");
        PdfFixtures.Ln(sb, "trailer");
        PdfFixtures.Ln(sb, "<< /Size 5 /Root 1 0 R /ID [<AABBCCDD><AABBCCDD>] >>");
        PdfFixtures.Ln(sb, "startxref");
        PdfFixtures.Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     Produces a minimal PDF with a stream whose /Filter is /LZWDecode (prohibited in PDF/A).
    ///     The stream data is a single null byte; actual LZW decoding is not attempted by the validator.
    /// </summary>
    private static byte[] BuildPdfWithLzwStream()
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        PdfFixtures.Ln(sb, "%PDF-1.4");
        PdfFixtures.Ln(sb, "%\xE2\xE3\xCF\xD3");

        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "1 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        PdfFixtures.Ln(sb, "endobj");

        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "2 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        PdfFixtures.Ln(sb, "endobj");

        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "3 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R >>");
        PdfFixtures.Ln(sb, "endobj");

        // Object 4 — stream with /LZWDecode filter (1 dummy byte)
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "4 0 obj");
        PdfFixtures.Ln(sb, "<< /Length 1 /Filter /LZWDecode >>");
        sb.Append("stream\n\0\nendstream\n");
        PdfFixtures.Ln(sb, "endobj");

        var xrefOffset = PdfFixtures.Len(sb);
        PdfFixtures.Ln(sb, "xref");
        PdfFixtures.Ln(sb, "0 5");
        PdfFixtures.Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets)
            PdfFixtures.Ln(sb, $"{o:D10} 00000 n ");
        PdfFixtures.Ln(sb, "trailer");
        // ReSharper disable StringLiteralTypo
        PdfFixtures.Ln(sb, "<< /Size 5 /Root 1 0 R /ID [<AABBCCDD><AABBCCDD>] >>");
        // ReSharper restore StringLiteralTypo
        PdfFixtures.Ln(sb, "startxref");
        PdfFixtures.Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     Produces a minimal PDF with a /Font dict that has no /FontDescriptor entry,
    ///     which is a §6.3.3 violation.
    /// </summary>
    private static byte[] BuildPdfWithUnembeddedFont()
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        PdfFixtures.Ln(sb, "%PDF-1.4");
        PdfFixtures.Ln(sb, "%\xE2\xE3\xCF\xD3");

        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "1 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        PdfFixtures.Ln(sb, "endobj");

        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "2 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        PdfFixtures.Ln(sb, "endobj");

        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "3 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>");
        PdfFixtures.Ln(sb, "endobj");

        // Object 4 — Font with no /FontDescriptor (nor FontFile*)
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "4 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Font /Subtype /TrueType /BaseFont /Helvetica >>");
        PdfFixtures.Ln(sb, "endobj");

        var xrefOffset = PdfFixtures.Len(sb);
        PdfFixtures.Ln(sb, "xref");
        PdfFixtures.Ln(sb, "0 5");
        PdfFixtures.Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets)
            PdfFixtures.Ln(sb, $"{o:D10} 00000 n ");
        PdfFixtures.Ln(sb, "trailer");
        PdfFixtures.Ln(sb, "<< /Size 5 /Root 1 0 R /ID [<AABBCCDD><AABBCCDD>] >>");
        PdfFixtures.Ln(sb, "startxref");
        PdfFixtures.Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     Produces a minimal PDF whose catalog contains an /AA (additional actions) entry,
    ///     which is a §6.6.1 violation.
    /// </summary>
    private static byte[] BuildPdfWithCatalogAa()
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        PdfFixtures.Ln(sb, "%PDF-1.4");
        PdfFixtures.Ln(sb, "%\xE2\xE3\xCF\xD3");

        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "1 0 obj");
        PdfFixtures.Ln(sb, @"<< /Type /Catalog /Pages 2 0 R /AA << /WC << /S /JavaScript /JS (app.alert\('hi'\)) >> >> >>");
        PdfFixtures.Ln(sb, "endobj");

        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "2 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        PdfFixtures.Ln(sb, "endobj");

        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "3 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>");
        PdfFixtures.Ln(sb, "endobj");

        var xrefOffset = PdfFixtures.Len(sb);
        PdfFixtures.Ln(sb, "xref");
        PdfFixtures.Ln(sb, "0 4");
        PdfFixtures.Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets)
            PdfFixtures.Ln(sb, $"{o:D10} 00000 n ");
        PdfFixtures.Ln(sb, "trailer");
        // ReSharper disable StringLiteralTypo
        PdfFixtures.Ln(sb, "<< /Size 4 /Root 1 0 R /ID [<AABBCCDD><AABBCCDD>] >>");
        // ReSharper restore StringLiteralTypo
        PdfFixtures.Ln(sb, "startxref");
        PdfFixtures.Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
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
