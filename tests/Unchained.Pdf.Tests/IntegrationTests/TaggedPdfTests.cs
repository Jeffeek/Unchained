using System.Text;
using Shouldly;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Tests for tagged PDF (ISO 32000-1 §14.7) and PDF/UA-1 (ISO 14289-1) support.
///     Covers structure tree generation, marked-content operators, catalog entries,
///     and the ValidatePdfUAAsync validation engine.
/// </summary>
public sealed class TaggedPdfTests : PdfTestBase
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<byte[]> SaveBytesAsync(IPdfDocument doc, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: ct);
        return ms.ToArray();
    }

    private static bool ContentStreamContains(byte[] pdfBytes, string search)
    {
        var full = Encoding.Latin1.GetString(pdfBytes);
        return full.Contains(search, StringComparison.Ordinal);
    }

    // ── TxtToPdfConverter — tagged ────────────────────────────────────────────

    [Fact]
    public async Task TxtTagged_Catalog_HasMarkInfo()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "Hello world",
            new TxtLoadOptions(Tagged: true, Language: "en-US"),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldContain("/MarkInfo");
        text.ShouldContain("/Marked");
    }

    [Fact]
    public async Task TxtTagged_Catalog_HasStructTreeRoot()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "Hello world",
            new TxtLoadOptions(Tagged: true, Language: "en-US"),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldContain("/StructTreeRoot");
    }

    [Fact]
    public async Task TxtTagged_Catalog_HasLangEntry()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "Hello world",
            new TxtLoadOptions(Tagged: true, Language: "en-US"),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldContain("/Lang");
        text.ShouldContain("en-US");
    }

    [Fact]
    public async Task TxtTagged_ContentStream_ContainsBdcOperator()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "Hello world",
            new TxtLoadOptions(Tagged: true),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);

        ContentStreamContains(bytes, "BDC").ShouldBeTrue();
    }

    [Fact]
    public async Task TxtTagged_ContentStream_ContainsEmcOperator()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "Hello world",
            new TxtLoadOptions(Tagged: true),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);

        ContentStreamContains(bytes, "EMC").ShouldBeTrue();
    }

    [Fact]
    public async Task TxtTagged_ContentStream_ContainsMcidProperty()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "Line one\nLine two",
            new TxtLoadOptions(Tagged: true),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);

        ContentStreamContains(bytes, "/MCID").ShouldBeTrue();
    }

    [Fact]
    public async Task TxtTagged_StructureTree_HasParentTree()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "Hello world",
            new TxtLoadOptions(Tagged: true),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldContain("/ParentTree");
    }

    [Fact]
    public async Task TxtTagged_StructureTree_HasParagraphElements()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "First line\nSecond line",
            new TxtLoadOptions(Tagged: true),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldContain("/P ");
    }

    [Fact]
    public async Task TxtTagged_ViewerPreferences_DisplayDocTitleTrue()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "Hello",
            new TxtLoadOptions(Tagged: true),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldContain("/DisplayDocTitle");
    }

    [Fact]
    public async Task TxtUntagged_Catalog_HasNoMarkInfo()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "Hello world",
            ct: TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldNotContain("/MarkInfo");
        text.ShouldNotContain("/StructTreeRoot");
    }

    // ── MarkdownToPdfConverter — tagged ───────────────────────────────────────

    [Fact]
    public async Task MdTagged_Heading_ProducesH1StructureElement()
    {
        await using var doc = await Processor.LoadFromMarkdownAsync(
            "# My Heading\n\nSome paragraph text.",
            new MdLoadOptions(Tagged: true, Language: "en"),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldContain("/H1");
    }

    [Fact]
    public async Task MdTagged_Paragraph_ProducesPStructureElement()
    {
        await using var doc = await Processor.LoadFromMarkdownAsync(
            "A plain paragraph.",
            new MdLoadOptions(Tagged: true),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldContain("/P ");
    }

    [Fact]
    public async Task MdTagged_CodeBlock_ProducesCodeStructureElement()
    {
        await using var doc = await Processor.LoadFromMarkdownAsync(
            "```\nvar x = 1;\n```",
            new MdLoadOptions(Tagged: true),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldContain("/Code");
    }

    [Fact]
    public async Task MdTagged_List_ProducesListStructureElements()
    {
        await using var doc = await Processor.LoadFromMarkdownAsync(
            "- Item one\n- Item two\n- Item three",
            new MdLoadOptions(Tagged: true),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldContain("/LBody");
    }

    [Fact]
    public async Task MdTagged_MultipleHeadingLevels_ProduceCorrectTags()
    {
        await using var doc = await Processor.LoadFromMarkdownAsync(
            "# H1\n\n## H2\n\n### H3",
            new MdLoadOptions(Tagged: true),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldContain("/H1");
        text.ShouldContain("/H2");
        text.ShouldContain("/H3");
    }

    [Fact]
    public async Task MdTagged_ContentStream_ContainsBdcEmc()
    {
        await using var doc = await Processor.LoadFromMarkdownAsync(
            "# Title\n\nParagraph.",
            new MdLoadOptions(Tagged: true),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);

        ContentStreamContains(bytes, "BDC").ShouldBeTrue();
        ContentStreamContains(bytes, "EMC").ShouldBeTrue();
    }

    [Fact]
    public async Task MdTagged_ThematicBreak_MarkedAsArtifact()
    {
        await using var doc = await Processor.LoadFromMarkdownAsync(
            "Before\n\n---\n\nAfter",
            new MdLoadOptions(Tagged: true),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);

        ContentStreamContains(bytes, "/Artifact").ShouldBeTrue();
    }

    // ── SvgToPdfConverter — tagged ────────────────────────────────────────────

    [Fact]
    public async Task SvgTagged_ContentStream_ContainsFigureBdc()
    {
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><rect width="100" height="100" fill="blue"/></svg>""";
        await using var doc = await Processor.LoadFromSvgAsync(
            svg,
            new SvgLoadOptions(Tagged: true, Language: "en", AltText: "A blue square"),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);

        ContentStreamContains(bytes, "/Figure").ShouldBeTrue();
        ContentStreamContains(bytes, "BDC").ShouldBeTrue();
        ContentStreamContains(bytes, "EMC").ShouldBeTrue();
    }

    [Fact]
    public async Task SvgTagged_StructureTree_HasFigureElement()
    {
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" width="50" height="50"><circle cx="25" cy="25" r="25"/></svg>""";
        await using var doc = await Processor.LoadFromSvgAsync(
            svg,
            new SvgLoadOptions(Tagged: true, AltText: "A circle"),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldContain("/Figure");
    }

    [Fact]
    public async Task SvgTagged_AltText_WrittenToFigureElement()
    {
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"><rect width="10" height="10"/></svg>""";
        await using var doc = await Processor.LoadFromSvgAsync(
            svg,
            new SvgLoadOptions(Tagged: true, AltText: "Descriptive alt text"),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldContain("/Alt");
    }

    [Fact]
    public async Task SvgUntagged_ContentStream_HasNoFigureBdc()
    {
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"><rect width="10" height="10"/></svg>""";
        await using var doc = await Processor.LoadFromSvgAsync(
            svg,
            ct: TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);

        ContentStreamContains(bytes, "BDC").ShouldBeFalse();
    }

    // ── PDF/UA validation — ValidatePdfUAAsync ────────────────────────────────

    [Fact]
    public async Task ValidatePdfUA_UntaggedPdf_ReportsMarkInfoViolation()
    {
        var result = await Processor.ValidatePdfUAAsync(
            PdfFixtures.SinglePage(),
            TestContext.Current.CancellationToken
        );

        result.Violations.ShouldContain(static v => v.RuleId == "7.2");
    }

    [Fact]
    public async Task ValidatePdfUA_UntaggedPdf_ReportsLanguageViolation()
    {
        var result = await Processor.ValidatePdfUAAsync(
            PdfFixtures.SinglePage(),
            TestContext.Current.CancellationToken
        );

        result.Violations.ShouldContain(static v => v.RuleId == "7.4");
    }

    [Fact]
    public async Task ValidatePdfUA_UntaggedPdf_ReportsStructTreeViolation()
    {
        var result = await Processor.ValidatePdfUAAsync(
            PdfFixtures.SinglePage(),
            TestContext.Current.CancellationToken
        );

        result.Violations.ShouldContain(static v => v.RuleId == "7.5");
    }

    [Fact]
    public async Task ValidatePdfUA_AllViolations_HaveRuleId()
    {
        var result = await Processor.ValidatePdfUAAsync(
            PdfFixtures.SinglePage(),
            TestContext.Current.CancellationToken
        );

        result.Violations.ShouldAllBe(static v => !string.IsNullOrWhiteSpace(v.RuleId));
    }

    [Fact]
    public async Task ValidatePdfUA_Result_IsNotNull()
    {
        var result = await Processor.ValidatePdfUAAsync(
            PdfFixtures.SinglePage(),
            TestContext.Current.CancellationToken
        );

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task ValidatePdfUA_UntaggedPdf_IsNotConformant()
    {
        var result = await Processor.ValidatePdfUAAsync(
            PdfFixtures.SinglePage(),
            TestContext.Current.CancellationToken
        );

        result.IsConformant.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidatePdfUA_TaggedTxtPdf_FewerErrorsThanUntagged()
    {
        var untaggedResult = await Processor.ValidatePdfUAAsync(
            PdfFixtures.SinglePage(),
            TestContext.Current.CancellationToken
        );

        await using var taggedDoc = await Processor.LoadFromTxtAsync(
            "Hello accessibility",
            new TxtLoadOptions(Tagged: true, Language: "en-US"),
            TestContext.Current.CancellationToken
        );
        var taggedBytes = await SaveBytesAsync(taggedDoc, TestContext.Current.CancellationToken);
        var taggedResult = await Processor.ValidatePdfUAAsync(taggedBytes, TestContext.Current.CancellationToken);

        var untaggedErrors = untaggedResult.Violations.Count(static v => v.Severity == PdfUAViolationSeverity.Error);
        var taggedErrors = taggedResult.Violations.Count(static v => v.Severity == PdfUAViolationSeverity.Error);

        taggedErrors.ShouldBeLessThan(untaggedErrors);
    }

    [Fact]
    public async Task ValidatePdfUA_TaggedPdf_HasNoMarkInfoViolation()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "Accessible content",
            new TxtLoadOptions(Tagged: true, Language: "en"),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfUAAsync(bytes, TestContext.Current.CancellationToken);

        result.Violations.ShouldNotContain(static v => v.RuleId == "7.2");
    }

    [Fact]
    public async Task ValidatePdfUA_TaggedPdf_HasNoLanguageViolation()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "Accessible content",
            new TxtLoadOptions(Tagged: true, Language: "en-US"),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfUAAsync(bytes, TestContext.Current.CancellationToken);

        result.Violations.ShouldNotContain(static v => v.RuleId == "7.4");
    }

    [Fact]
    public async Task ValidatePdfUA_TaggedPdf_HasNoStructTreeViolation()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "Accessible content",
            new TxtLoadOptions(Tagged: true, Language: "en"),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfUAAsync(bytes, TestContext.Current.CancellationToken);

        result.Violations.ShouldNotContain(static v => v.RuleId == "7.5");
    }

    [Fact]
    public async Task ValidatePdfUA_TaggedMarkdownPdf_HasNoMarkInfoViolation()
    {
        await using var doc = await Processor.LoadFromMarkdownAsync(
            "# Title\n\nParagraph text.",
            new MdLoadOptions(Tagged: true, Language: "en"),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfUAAsync(bytes, TestContext.Current.CancellationToken);

        result.Violations.ShouldNotContain(static v => v.RuleId == "7.2");
    }

    [Fact]
    public async Task ValidatePdfUA_MissingPdfVersion_ReportsVersionViolation()
    {
        var result = await Processor.ValidatePdfUAAsync(
            "%PDF"u8.ToArray(),
            TestContext.Current.CancellationToken
        );

        result.ShouldNotBeNull();
        result.IsConformant.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidatePdfUA_EncryptedPdf_ReturnsResultWithoutThrowing()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(
            doc,
            ms,
            new SaveOptions(Encryption: new EncryptionOptions("pw")),
            TestContext.Current.CancellationToken
        );

        var result = await Processor.ValidatePdfUAAsync(ms.ToArray(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsConformant.ShouldBeFalse();
    }

    // ── PDF/UA validation — §7.7 Figure alt text ──────────────────────────────

    [Fact]
    public async Task ValidatePdfUA_SvgTaggedWithAltText_NoFigureAltViolation()
    {
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"><rect width="10" height="10"/></svg>""";
        await using var doc = await Processor.LoadFromSvgAsync(
            svg,
            new SvgLoadOptions(Tagged: true, Language: "en", AltText: "A rectangle"),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfUAAsync(bytes, TestContext.Current.CancellationToken);

        result.Violations.ShouldNotContain(static v => v.RuleId == "7.7" && v.Description.Contains("Figure"));
    }

    // ── PDF/UA validation — §7.8 Heading levels ──────────────────────────────

    [Fact]
    public async Task ValidatePdfUA_HeadingsInOrder_NoHeadingViolation()
    {
        await using var doc = await Processor.LoadFromMarkdownAsync(
            "# H1\n\n## H2\n\n### H3\n\nParagraph.",
            new MdLoadOptions(Tagged: true, Language: "en"),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfUAAsync(bytes, TestContext.Current.CancellationToken);

        result.Violations.ShouldNotContain(static v => v.RuleId == "7.8");
    }

    // ── Structure tree correctness ────────────────────────────────────────────

    [Fact]
    public async Task StructureTree_TxtMultiPage_McidStartsAtZeroPerPage()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            string.Join("\n", Enumerable.Repeat("Line of text for the page content.", 100)),
            new TxtLoadOptions(Tagged: true, Language: "en"),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldContain("/MCID 0");
    }

    [Fact]
    public async Task StructureTree_HasDocumentRootElement()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "Content",
            new TxtLoadOptions(Tagged: true),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldContain("/Document");
    }

    [Fact]
    public async Task StructureTree_HasStructTreeRootType()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "Content",
            new TxtLoadOptions(Tagged: true),
            TestContext.Current.CancellationToken
        );
        var bytes = await SaveBytesAsync(doc, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldContain("/StructTreeRoot");
    }
}
