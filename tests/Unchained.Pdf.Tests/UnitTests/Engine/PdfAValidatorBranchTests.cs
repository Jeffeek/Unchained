using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Engine;

/// <summary>
///     Unit tests for <see cref="PdfAValidator" /> driving ISO 19005-1 rule branches that the
///     integration suite does not reach: transparency (§6.4), prohibited annotations / actions
///     (§6.5–6.6), embedded files and collections (§6.8). Crafts minimal object graphs and calls
///     the internal <c>Validate</c> entry point directly.
/// </summary>
public sealed class PdfAValidatorBranchTests
{
    private const string Pages = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";
    private const string PlainPage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>";

    private const string PlainCatalog = "<< /Type /Catalog /Pages 2 0 R >>";

    private static PdfAValidationResult Validate(string catalog, params string[] extra)
    {
        var bodies = new List<string> { catalog, Pages, PlainPage };
        bodies.AddRange(extra);
        return PdfAValidator.Validate(RawPdfBuilder.Build(bodies, "1.4"), PdfAProfile.PdfA1B);
    }

    // ── §6.4 Transparency ─────────────────────────────────────────────────────

    [Fact]
    public void ExtGState_WithSMask_ReportsTransparencyViolation()
    {
        var result = Validate(PlainCatalog, "<< /Type /ExtGState /SMask << /S /Alpha >> >>");
        result.Violations.ShouldContain(static v => v.RuleId == "6.4" && v.Description.Contains("SMask"));
    }

    [Fact]
    public void ExtGState_SMaskNone_NoTransparencyViolation()
    {
        var result = Validate(PlainCatalog, "<< /Type /ExtGState /SMask /None >>");
        result.Violations.ShouldNotContain(static v => v.RuleId == "6.4" && v.Description.Contains("SMask"));
    }

    [Fact]
    public void ExtGState_NonNormalBlendMode_ReportsViolation()
    {
        var result = Validate(PlainCatalog, "<< /Type /ExtGState /BM /Multiply >>");
        result.Violations.ShouldContain(static v => v.RuleId == "6.4" && v.Description.Contains("Multiply"));
    }

    [Fact]
    public void ExtGState_NormalBlendMode_NoBlendViolation()
    {
        var result = Validate(PlainCatalog, "<< /Type /ExtGState /BM /Normal >>");
        result.Violations.ShouldNotContain(static v => v.RuleId == "6.4" && v.Description.Contains("blend"));
    }

    [Fact]
    public void ExtGState_StrokeOpacityBelowOne_ReportsViolation()
    {
        var result = Validate(PlainCatalog, "<< /Type /ExtGState /CA 0.5 >>");
        result.Violations.ShouldContain(static v => v.RuleId == "6.4" && v.Description.Contains("/CA"));
    }

    [Fact]
    public void ExtGState_FillOpacityBelowOne_ReportsViolation()
    {
        var result = Validate(PlainCatalog, "<< /Type /ExtGState /ca 0.25 >>");
        result.Violations.ShouldContain(static v => v.RuleId == "6.4" && v.Description.Contains("/ca"));
    }

    // ── §6.5 Annotations ──────────────────────────────────────────────────────

    [Fact]
    public void ProhibitedAnnotationType_ReportsViolation()
    {
        var page = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [4 0 R] >>";
        var bodies = new List<string>
        {
            PlainCatalog,
            Pages,
            page,
            "<< /Type /Annot /Subtype /Movie /Rect [0 0 10 10] /F 4 >>"
        };
        var result = PdfAValidator.Validate(RawPdfBuilder.Build(bodies, "1.4"), PdfAProfile.PdfA1B);
        result.Violations.ShouldContain(static v => v.RuleId == "6.5.3" && v.Description.Contains("Movie"));
    }

    [Fact]
    public void Annotation_WithoutPrintFlag_ReportsViolation()
    {
        var page = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [4 0 R] >>";
        var bodies = new List<string>
        {
            PlainCatalog,
            Pages,
            page,
            // /Text annotation with /F 0 (no Print bit set).
            "<< /Type /Annot /Subtype /Text /Rect [0 0 10 10] /F 0 >>"
        };
        var result = PdfAValidator.Validate(RawPdfBuilder.Build(bodies, "1.4"), PdfAProfile.PdfA1B);
        result.Violations.ShouldContain(static v => v.RuleId == "6.5.3" && v.Description.Contains("Print"));
    }

    [Fact]
    public void WidgetAnnotation_WithoutAppearance_ReportsViolation()
    {
        var page = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [4 0 R] >>";
        var bodies = new List<string>
        {
            PlainCatalog,
            Pages,
            page,
            "<< /Type /Annot /Subtype /Widget /Rect [0 0 10 10] /F 4 >>"
        };
        var result = PdfAValidator.Validate(RawPdfBuilder.Build(bodies, "1.4"), PdfAProfile.PdfA1B);
        result.Violations.ShouldContain(static v => v.RuleId == "6.5.4");
    }

    // ── §6.6 Actions ──────────────────────────────────────────────────────────

    [Fact]
    public void ProhibitedActionInAnnotation_ReportsViolation()
    {
        var page = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [4 0 R] >>";
        var bodies = new List<string>
        {
            PlainCatalog,
            Pages,
            page,
            "<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /F 4 /A << /S /Launch >> >>"
        };
        var result = PdfAValidator.Validate(RawPdfBuilder.Build(bodies, "1.4"), PdfAProfile.PdfA1B);
        result.Violations.ShouldContain(static v => v.RuleId == "6.6.2" && v.Description.Contains("Launch"));
    }

    [Fact]
    public void CatalogAdditionalActions_ReportsViolation()
    {
        var catalog = "<< /Type /Catalog /Pages 2 0 R /AA << /WC << /S /JavaScript >> >> >>";
        var result = Validate(catalog);
        result.Violations.ShouldContain(static v => v.RuleId == "6.6.1");
    }

    // ── §6.8 Embedded files / collections ─────────────────────────────────────

    [Fact]
    public void EmbeddedFiles_ReportsViolation()
    {
        var catalog = "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [] >> >> >>";
        var result = Validate(catalog);
        result.Violations.ShouldContain(static v => v.RuleId == "6.8" && v.Description.Contains("EmbeddedFiles"));
    }

    [Fact]
    public void Collection_ReportsViolation()
    {
        var catalog = "<< /Type /Catalog /Pages 2 0 R /Collection << /View /D >> >>";
        var result = Validate(catalog);
        result.Violations.ShouldContain(static v => v.RuleId == "6.8" && v.Description.Contains("Collection"));
    }

    // ── §6.2.2 Output intent (warning when absent) ────────────────────────────

    [Fact]
    public void NoOutputIntent_ReportsWarning()
    {
        var result = Validate(PlainCatalog);
        result.Warnings.ShouldContain(static v => v.RuleId == "6.2.2");
    }

    // ── §6.1.2 version per profile ────────────────────────────────────────────

    [Fact]
    public void Pdf14_WithinPdfA1Limit_NoVersionViolation()
    {
        var result = Validate(PlainCatalog);
        result.Violations.ShouldNotContain(static v => v.RuleId == "6.1.2");
    }
}
