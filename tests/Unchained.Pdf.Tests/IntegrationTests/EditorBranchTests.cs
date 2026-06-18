using System.Text;
using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Additional editor tests covering branches the base suites do not reach:
///     <see cref="PageLabelEditor" /> style/prefix/start round-trips and empty-range guards,
///     and <see cref="Redactor" /> multi-region / multi-page redaction.
/// </summary>
public sealed class EditorBranchTests : PdfTestBase
{
    // ── PageLabelEditor ────────────────────────────────────────────────────────

    [Fact]
    public async Task PageLabels_EmptyRanges_Throws()
    {
        var editor = new PageLabelEditor();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Should.ThrowAsync<ArgumentException>(() =>
            editor.SetPageLabelsAsync(doc, [], TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task PageLabels_StartNumberAndPrefix_RoundTrip()
    {
        var editor = new PageLabelEditor();
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(4), TestContext.Current.CancellationToken);

        await editor.SetPageLabelsAsync(
            doc,
            [new PageLabelRange(0, PageLabelStyle.Decimal, "App-", 5)],
            TestContext.Current.CancellationToken
        );

        var result = editor.GetPageLabels(doc);
        result.Count.ShouldBe(1);
        result[0].Prefix.ShouldBe("App-");
        result[0].FirstLabelNumber.ShouldBe(5);
    }

    [Fact]
    public async Task PageLabels_NoneStyle_RoundTrips()
    {
        var editor = new PageLabelEditor();
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);

        await editor.SetPageLabelsAsync(
            doc,
            [new PageLabelRange(0, PageLabelStyle.None, "Cover")],
            TestContext.Current.CancellationToken
        );

        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        var result = editor.GetPageLabels(reloaded);
        result[0].Prefix.ShouldBe("Cover");
        result[0].Style.ShouldBe(PageLabelStyle.None);
    }

    [Fact]
    public async Task PageLabels_MultipleRanges_SortedByStartIndex()
    {
        var editor = new PageLabelEditor();
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(6), TestContext.Current.CancellationToken);

        await editor.SetPageLabelsAsync(
            doc,
            [
                new PageLabelRange(0, PageLabelStyle.RomanLower),
                new PageLabelRange(4),
                new PageLabelRange(2, PageLabelStyle.AlphaUpper)
            ],
            TestContext.Current.CancellationToken
        );

        var result = editor.GetPageLabels(doc);
        result.Count.ShouldBe(3);
        result[0].StartPageIndex.ShouldBe(0);
        result[1].StartPageIndex.ShouldBe(2);
        result[2].StartPageIndex.ShouldBe(4);
    }

    [Fact]
    public async Task RemovePageLabels_WhenNoneSet_NoOp()
    {
        var editor = new PageLabelEditor();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        // Removing when none exist must not throw.
        await Should.NotThrowAsync(() => editor.RemovePageLabelsAsync(doc, TestContext.Current.CancellationToken));
        editor.GetPageLabels(doc).ShouldBeEmpty();
    }

    // ── Redactor ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Redact_MultipleRegionsSamePage_RemovesAllCovered()
    {
        var redactor = new Redactor();
        await using var doc = await LoadAsync(
            PdfFixtures.WithRawContent(
                "BT /F1 12 Tf 100 700 Td (Alpha) Tj ET BT /F1 12 Tf 100 600 Td (Beta) Tj ET"
            ),
            TestContext.Current.CancellationToken
        );

        await redactor.RedactAsync(
            doc,
            [
                new RedactionRegion(1, 80, 690, 200, 30),
                new RedactionRegion(1, 80, 590, 200, 30)
            ],
            TestContext.Current.CancellationToken
        );

        var text = doc.Pages[1].ExtractText();
        text.ShouldNotContain("Alpha");
        text.ShouldNotContain("Beta");
    }

    [Fact]
    public async Task Redact_MultiPage_RedactsEachPageIndependently()
    {
        var redactor = new Redactor();
        // Build a 2-page doc, each with text at (100,700).
        await using var doc = await LoadAsync(BuildTwoPageTextPdf(), TestContext.Current.CancellationToken);

        await redactor.RedactAsync(
            doc,
            [
                new RedactionRegion(1, 80, 690, 200, 30),
                new RedactionRegion(2, 80, 690, 200, 30)
            ],
            TestContext.Current.CancellationToken
        );

        doc.Pages[1].ExtractText().ShouldNotContain("PageOne");
        doc.Pages[2].ExtractText().ShouldNotContain("PageTwo");
    }

    [Fact]
    public async Task Redact_CustomFillColor_ProducesColoredCover()
    {
        var redactor = new Redactor();
        await using var doc = await LoadAsync(
            PdfFixtures.WithTextContent("Secret"),
            TestContext.Current.CancellationToken
        );

        await redactor.RedactAsync(
            doc,
            [
                new RedactionRegion(
                    1,
                    80,
                    690,
                    200,
                    30,
                    (1.0, 0.0, 0.0)
                )
            ],
            TestContext.Current.CancellationToken
        );

        // The cover rect must emit an 'rg' (set fill colour) followed by a fill.
        var ops = doc.Pages[1].GetContentOperators();
        ops.ShouldContain(static o => o.Name == "rg");
        ops.ShouldContain(static o => o.Name == "f");
    }

    private static byte[] BuildTwoPageTextPdf()
    {
        // Two pages each referencing their own content stream.
        var sb = new StringBuilder();
        var offsets = new List<int>();

        PdfFixtures.Ln(sb, "%PDF-1.7");
        PdfFixtures.Ln(sb, "%\xE2\xE3\xCF\xD3");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "1 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        PdfFixtures.Ln(sb, "endobj");
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "2 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Pages /Kids [3 0 R 5 0 R] /Count 2 >>");
        PdfFixtures.Ln(sb, "endobj");

        const string c1 = "BT /F1 12 Tf 100 700 Td (PageOne) Tj ET";
        const string c2 = "BT /F1 12 Tf 100 700 Td (PageTwo) Tj ET";

        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "3 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R >>");
        PdfFixtures.Ln(sb, "endobj");
        var c1B = Encoding.Latin1.GetBytes(c1);
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "4 0 obj");
        PdfFixtures.Ln(sb, $"<< /Length {c1B.Length} >>");
        sb.Append("stream\n").Append(c1);
        PdfFixtures.Ln(sb, "\nendstream");
        PdfFixtures.Ln(sb, "endobj");

        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "5 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 6 0 R >>");
        PdfFixtures.Ln(sb, "endobj");
        var c2B = Encoding.Latin1.GetBytes(c2);
        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "6 0 obj");
        PdfFixtures.Ln(sb, $"<< /Length {c2B.Length} >>");
        sb.Append("stream\n").Append(c2);
        PdfFixtures.Ln(sb, "\nendstream");
        PdfFixtures.Ln(sb, "endobj");

        var xref = PdfFixtures.Len(sb);
        PdfFixtures.Ln(sb, "xref");
        PdfFixtures.Ln(sb, "0 7");
        PdfFixtures.Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets)
            PdfFixtures.Ln(sb, $"{o:D10} 00000 n ");
        PdfFixtures.Ln(sb, "trailer");
        PdfFixtures.Ln(sb, "<< /Size 7 /Root 1 0 R >>");
        PdfFixtures.Ln(sb, "startxref");
        PdfFixtures.Ln(sb, xref.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }
}
