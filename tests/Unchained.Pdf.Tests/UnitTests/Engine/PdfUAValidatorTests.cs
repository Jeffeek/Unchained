using System.Text;
using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Engine;

/// <summary>
///     Unit tests for <see cref="PdfUAValidator" /> that drive specific ISO 14289-1 rule branches
///     by crafting minimal object graphs (struct trees, role maps, annotations, actions, XMP).
///     The validator is invoked directly via its internal <c>Validate</c> entry point.
/// </summary>
public sealed class PdfUAValidatorTests
{
    private const string Catalog =
        "<< /Type /Catalog /Pages 2 0 R /Lang (en-US) /MarkInfo << /Marked true >> " +
        "/ViewerPreferences << /DisplayDocTitle true >> /StructTreeRoot 4 0 R >>";

    private const string Pages = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";
    private const string PlainPage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>";

    // StructTreeRoot referencing object 5 as its single child; carries /ParentTree so the
    // §7.5 ParentTree check passes and we isolate the rule under test.
    private static string StructRoot(string extra = "") =>
        $"<< /Type /StructTreeRoot /ParentTree << /Nums [] >> /K [5 0 R]{extra} >>";

    private static PdfUAValidationResult Validate(params string[] bodiesAfterPage)
    {
        var bodies = new List<string> { Catalog, Pages, PlainPage };
        bodies.AddRange(bodiesAfterPage);
        return PdfUAValidator.Validate(RawPdfBuilder.Build(bodies));
    }

    // ── §7.1 file header ──────────────────────────────────────────────────────

    [Fact]
    public void TooShortFile_ReportsVersionViolation() =>
        PdfUAValidator.Validate("%PD"u8.ToArray())
            .Violations.ShouldContain(static v => v.RuleId == "7.1");

    [Fact]
    public void NonPdfHeader_ReportsVersionViolation() =>
        PdfUAValidator.Validate("NOTAPDF1234"u8.ToArray())
            .Violations.ShouldContain(static v => v.RuleId == "7.1");

    [Fact]
    public void OldPdfVersion_BelowMinimum_ReportsVersionViolation()
    {
        var bytes = RawPdfBuilder.Build([Catalog, Pages, PlainPage, StructRoot(), "<< /S /P >>"], "1.2");
        PdfUAValidator.Validate(bytes).Violations.ShouldContain(static v => v.RuleId == "7.1");
    }

    // ── §7.6 RoleMap ──────────────────────────────────────────────────────────

    [Fact]
    public void RoleMap_NonStandardTarget_ReportsWarning()
    {
        var result = Validate(StructRoot(" /RoleMap << /MyKind /Bogus >>"), "<< /S /P >>");
        result.Violations.ShouldContain(static v => v.RuleId == "7.6");
    }

    [Fact]
    public void RoleMap_MappingToNonName_ReportsViolation()
    {
        var result = Validate(StructRoot(" /RoleMap << /MyKind 99 >>"), "<< /S /P >>");
        result.Violations.ShouldContain(static v => v.RuleId == "7.6");
    }

    [Fact]
    public void RoleMap_StandardTarget_NoRoleMapViolation()
    {
        var result = Validate(StructRoot(" /RoleMap << /MyKind /P >>"), "<< /S /P >>");
        result.Violations.ShouldNotContain(static v => v.RuleId == "7.6");
    }

    // ── §7.7 Figure alt text ──────────────────────────────────────────────────

    [Fact]
    public void Figure_WithoutAlt_ReportsViolation()
    {
        var result = Validate(StructRoot(), "<< /S /Figure >>");
        result.Violations.ShouldContain(static v => v.RuleId == "7.7" && v.Description.Contains("Figure"));
    }

    [Fact]
    public void Figure_WithAlt_NoFigureViolation()
    {
        var result = Validate(StructRoot(), "<< /S /Figure /Alt (a chart) >>");
        result.Violations.ShouldNotContain(static v => v.RuleId == "7.7" && v.Description.Contains("Figure"));
    }

    [Fact]
    public void Formula_WithActualText_NoFigureViolation()
    {
        var result = Validate(StructRoot(), "<< /S /Formula /ActualText (x squared) >>");
        result.Violations.ShouldNotContain(static v => v.RuleId == "7.7" && v.Description.Contains("Formula"));
    }

    // ── §7.8 Headings ─────────────────────────────────────────────────────────

    [Fact]
    public void Headings_SkippedLevel_ReportsWarning()
    {
        // Root /K references both heading elements (objects 5 and 6) in order.
        var bodies = new List<string>
        {
            Catalog,
            Pages,
            PlainPage,
            "<< /Type /StructTreeRoot /ParentTree << /Nums [] >> /K [5 0 R 6 0 R] >>",
            "<< /S /H1 >>",
            "<< /S /H3 >>"
        };
        PdfUAValidator.Validate(RawPdfBuilder.Build(bodies))
            .Violations.ShouldContain(static v => v.RuleId == "7.8");
    }

    [Fact]
    public void Headings_Consecutive_NoHeadingViolation()
    {
        var bodies = new List<string>
        {
            Catalog,
            Pages,
            PlainPage,
            "<< /Type /StructTreeRoot /ParentTree << /Nums [] >> /K [5 0 R 6 0 R] >>",
            "<< /S /H1 >>",
            "<< /S /H2 >>"
        };
        PdfUAValidator.Validate(RawPdfBuilder.Build(bodies))
            .Violations.ShouldNotContain(static v => v.RuleId == "7.8");
    }

    // ── §7.9 Tables ───────────────────────────────────────────────────────────

    [Fact]
    public void Table_WithoutRows_ReportsWarning()
    {
        var result = Validate(StructRoot(), "<< /S /Table >>");
        result.Violations.ShouldContain(static v => v.RuleId == "7.9");
    }

    [Fact]
    public void Table_WithRow_NoTableViolation()
    {
        var result = Validate(StructRoot(), "<< /S /Table /K [6 0 R] >>", "<< /S /TR >>");
        result.Violations.ShouldNotContain(static v => v.RuleId == "7.9");
    }

    // ── §7.10 Lists ───────────────────────────────────────────────────────────

    [Fact]
    public void List_ItemWithoutLBody_ReportsWarning()
    {
        // L (obj5) → LI (obj6) with no LBody child.
        var result = Validate(StructRoot(), "<< /S /L /K [6 0 R] >>", "<< /S /LI >>");
        result.Violations.ShouldContain(static v => v.RuleId == "7.10");
    }

    [Fact]
    public void List_ItemWithLBody_NoListViolation()
    {
        var result = Validate(
            StructRoot(),
            "<< /S /L /K [6 0 R] >>",
            "<< /S /LI /K [7 0 R] >>",
            "<< /S /LBody >>"
        );
        result.Violations.ShouldNotContain(static v => v.RuleId == "7.10");
    }

    // ── §7.11 Untagged content ────────────────────────────────────────────────

    [Fact]
    public void UntaggedContent_PageWithDrawingButNoMarkedContent_ReportsViolation()
    {
        // Build a page that draws a rectangle but has no BDC/BMC sequences.
        var result = PdfUAValidator.Validate(PdfFixtures.WithRawContent("0 0 50 50 re f\n"));
        result.Violations.ShouldContain(static v => v.RuleId == "7.11");
    }

    // ── §7.13 Annotations ─────────────────────────────────────────────────────

    [Fact]
    public void Annotation_WithoutContentsOrTu_ReportsWarning()
    {
        const string page = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Annots [6 0 R] >>";
        var bodies = new List<string>
        {
            Catalog,
            Pages,
            page,
            StructRoot(),
            "<< /S /P >>",
            "<< /Type /Annot /Subtype /Square /Rect [0 0 10 10] >>"
        };
        PdfUAValidator.Validate(RawPdfBuilder.Build(bodies))
            .Violations.ShouldContain(static v => v.RuleId == "7.13");
    }

    // ── §7.14 Actions ─────────────────────────────────────────────────────────

    [Fact]
    public void OpenAction_JavaScript_ReportsViolation()
    {
        const string catalog = "<< /Type /Catalog /Pages 2 0 R /Lang (en-US) /MarkInfo << /Marked true >> " +
                               "/ViewerPreferences << /DisplayDocTitle true >> /StructTreeRoot 4 0 R " +
                               @"/OpenAction << /S /JavaScript /JS (app.alert\(1\)) >> >>";
        var bodies = new List<string> { catalog, Pages, PlainPage, StructRoot(), "<< /S /P >>" };
        PdfUAValidator.Validate(RawPdfBuilder.Build(bodies))
            .Violations.ShouldContain(static v => v.RuleId == "7.14");
    }

    [Fact]
    public void CatalogAdditionalActions_ReportsWarning()
    {
        const string catalog = "<< /Type /Catalog /Pages 2 0 R /Lang (en-US) /MarkInfo << /Marked true >> " +
                               "/ViewerPreferences << /DisplayDocTitle true >> /StructTreeRoot 4 0 R " +
                               "/AA << /WC << /S /JavaScript >> >> >>";
        var bodies = new List<string> { catalog, Pages, PlainPage, StructRoot(), "<< /S /P >>" };
        PdfUAValidator.Validate(RawPdfBuilder.Build(bodies))
            .Violations.ShouldContain(static v => v.RuleId == "7.14");
    }

    // ── §7.2 / §7.4 baseline ──────────────────────────────────────────────────

    [Fact]
    public void TaggedDocument_NoMarkInfoOrLangViolations()
    {
        var result = Validate(StructRoot(), "<< /S /P >>");
        result.Violations.ShouldNotContain(static v => v.RuleId == "7.2");
        result.Violations.ShouldNotContain(static v => v.RuleId == "7.4");
    }

    [Fact]
    public void MissingStructTreeRoot_ReportsStructViolation()
    {
        const string catalog = "<< /Type /Catalog /Pages 2 0 R /Lang (en-US) /MarkInfo << /Marked true >> >>";
        var bodies = new List<string> { catalog, Pages, PlainPage };
        PdfUAValidator.Validate(RawPdfBuilder.Build(bodies))
            .Violations.ShouldContain(static v => v.RuleId == "7.5");
    }

    // ── §7.2 MarkInfo ─────────────────────────────────────────────────────────

    [Fact]
    public void MissingMarkInfo_ReportsViolation()
    {
        const string catalog = "<< /Type /Catalog /Pages 2 0 R /Lang (en-US) " +
                               "/ViewerPreferences << /DisplayDocTitle true >> /StructTreeRoot 4 0 R >>";
        var bodies = new List<string> { catalog, Pages, PlainPage, StructRoot(), "<< /S /P >>" };
        PdfUAValidator.Validate(RawPdfBuilder.Build(bodies))
            .Violations.ShouldContain(static v => v.RuleId == "7.2");
    }

    [Fact]
    public void MarkInfoMarkedFalse_ReportsViolation()
    {
        const string catalog = "<< /Type /Catalog /Pages 2 0 R /Lang (en-US) /MarkInfo << /Marked false >> " +
                               "/ViewerPreferences << /DisplayDocTitle true >> /StructTreeRoot 4 0 R >>";
        var bodies = new List<string> { catalog, Pages, PlainPage, StructRoot(), "<< /S /P >>" };
        PdfUAValidator.Validate(RawPdfBuilder.Build(bodies))
            .Violations.ShouldContain(static v => v.RuleId == "7.2");
    }

    // ── §7.3 Document title ───────────────────────────────────────────────────

    [Fact]
    public void MissingDisplayDocTitle_ReportsViolation()
    {
        const string catalog = "<< /Type /Catalog /Pages 2 0 R /Lang (en-US) /MarkInfo << /Marked true >> " +
                               "/StructTreeRoot 4 0 R >>";
        var bodies = new List<string> { catalog, Pages, PlainPage, StructRoot(), "<< /S /P >>" };
        PdfUAValidator.Validate(RawPdfBuilder.Build(bodies))
            .Violations.ShouldContain(static v => v.RuleId == "7.3");
    }

    // ── §7.4 Language ─────────────────────────────────────────────────────────

    [Fact]
    public void MissingLang_ReportsViolation()
    {
        const string catalog = "<< /Type /Catalog /Pages 2 0 R /MarkInfo << /Marked true >> " +
                               "/ViewerPreferences << /DisplayDocTitle true >> /StructTreeRoot 4 0 R >>";
        var bodies = new List<string> { catalog, Pages, PlainPage, StructRoot(), "<< /S /P >>" };
        PdfUAValidator.Validate(RawPdfBuilder.Build(bodies))
            .Violations.ShouldContain(static v => v.RuleId == "7.4");
    }

    [Fact]
    public void EmptyLang_ReportsViolation()
    {
        const string catalog = "<< /Type /Catalog /Pages 2 0 R /Lang () /MarkInfo << /Marked true >> " +
                               "/ViewerPreferences << /DisplayDocTitle true >> /StructTreeRoot 4 0 R >>";
        var bodies = new List<string> { catalog, Pages, PlainPage, StructRoot(), "<< /S /P >>" };
        PdfUAValidator.Validate(RawPdfBuilder.Build(bodies))
            .Violations.ShouldContain(static v => v.RuleId == "7.4");
    }

    // ── §7.5 ParentTree / empty K ─────────────────────────────────────────────

    [Fact]
    public void StructTreeRoot_MissingParentTree_ReportsViolation()
    {
        var bodies = new List<string>
        {
            Catalog,
            Pages,
            PlainPage,
            "<< /Type /StructTreeRoot /K [5 0 R] >>",
            "<< /S /P >>"
        };
        PdfUAValidator.Validate(RawPdfBuilder.Build(bodies))
            .Violations.ShouldContain(static v => v.RuleId == "7.5");
    }

    [Fact]
    public void StructTreeRoot_NoChildren_ReportsViolation()
    {
        var bodies = new List<string>
        {
            Catalog,
            Pages,
            PlainPage,
            "<< /Type /StructTreeRoot /ParentTree << /Nums [] >> >>"
        };
        PdfUAValidator.Validate(RawPdfBuilder.Build(bodies))
            .Violations.ShouldContain(static v => v.RuleId == "7.5");
    }

    // ── §7.7 Widget tooltip ───────────────────────────────────────────────────

    [Fact]
    public void WidgetWithoutTu_ReportsTooltipWarning()
    {
        const string page = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Annots [6 0 R] >>";
        var bodies = new List<string>
        {
            Catalog,
            Pages,
            page,
            StructRoot(),
            "<< /S /P >>",
            "<< /Type /Annot /Subtype /Widget /Rect [0 0 10 10] >>"
        };
        PdfUAValidator.Validate(RawPdfBuilder.Build(bodies))
            .Violations.ShouldContain(static v => v.RuleId == "7.7");
    }

    // ── §7.17 XMP metadata ────────────────────────────────────────────────────

    [Fact]
    public void MissingXmpMetadata_ReportsViolation()
    {
        // No /Metadata stream in the catalog → §7.17.
        var result = Validate(StructRoot(), "<< /S /P >>");
        result.Violations.ShouldContain(static v => v.RuleId == "7.17");
    }

    [Fact]
    public void XmpWithoutPdfuaid_ReportsViolation()
    {
        const string catalog = "<< /Type /Catalog /Pages 2 0 R /Lang (en-US) /MarkInfo << /Marked true >> " +
                               "/ViewerPreferences << /DisplayDocTitle true >> /StructTreeRoot 4 0 R /Metadata 6 0 R >>";
        var bodies = new List<string> { catalog, Pages, PlainPage, StructRoot(), "<< /S /P >>", "<< >>" };
        var xmp = "<?xpacket begin=\"\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\"></x:xmpmeta>"u8.ToArray();
        var bytes = RawPdfBuilder.BuildWithStream(bodies, 6, "<< /Type /Metadata /Subtype /XML >>", xmp);

        PdfUAValidator.Validate(bytes).Violations.ShouldContain(static v => v.RuleId == "7.17");
    }

    [Fact]
    public void XmpWithPdfuaidPart1_NoXmpViolation()
    {
        const string catalog = "<< /Type /Catalog /Pages 2 0 R /Lang (en-US) /MarkInfo << /Marked true >> " +
                               "/ViewerPreferences << /DisplayDocTitle true >> /StructTreeRoot 4 0 R /Metadata 6 0 R >>";
        var bodies = new List<string> { catalog, Pages, PlainPage, StructRoot(), "<< /S /P >>", "<< >>" };
        var xmp = Encoding.UTF8.GetBytes(
            "<?xpacket begin=\"\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">" +
            "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\" xmlns:pdfuaid=\"http://www.aiim.org/pdfua/ns/id/\">" +
            "<rdf:Description><pdfuaid:part>1</pdfuaid:part></rdf:Description></rdf:RDF></x:xmpmeta>"
        );
        var bytes = RawPdfBuilder.BuildWithStream(bodies, 6, "<< /Type /Metadata /Subtype /XML >>", xmp);

        PdfUAValidator.Validate(bytes).Violations.ShouldNotContain(static v => v.RuleId == "7.17");
    }
}
