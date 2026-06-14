using Shouldly;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class TextExtractionTests : PdfTestBase
{
    private static async Task<IPdfPage> LoadFirstPageAsync(byte[] pdfBytes)
    {
        var doc = await LoadAsync(pdfBytes);
        return doc.Pages[1];
    }

    // ── GetTextSpans ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTextSpans_PageWithTextContent_ReturnsSpans()
    {
        var page = await LoadFirstPageAsync(PdfFixtures.WithTextContent());
        var spans = page.GetTextSpans();
        spans.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetTextSpans_PageWithTextContent_ContainsExpectedText()
    {
        var page = await LoadFirstPageAsync(PdfFixtures.WithTextContent());
        var spans = page.GetTextSpans();
        var allText = string.Concat(spans.Select(static s => s.Text));
        allText.ShouldContain("Hello");
        allText.ShouldContain("Unchained");
    }

    [Fact]
    public async Task GetTextSpans_SpansHavePositiveYCoordinate()
    {
        var page = await LoadFirstPageAsync(PdfFixtures.WithTextContent("Test"));
        var spans = page.GetTextSpans();
        spans.ShouldAllBe(static s => s.Y >= 0);
    }

    [Fact]
    public async Task GetTextSpans_SpansHavePositiveFontSize()
    {
        var page = await LoadFirstPageAsync(PdfFixtures.WithTextContent("Test"));
        var spans = page.GetTextSpans();
        spans.ShouldAllBe(static s => s.FontSize > 0);
    }

    [Fact]
    public async Task GetTextSpans_SpansHaveNonEmptyFontName()
    {
        var page = await LoadFirstPageAsync(PdfFixtures.WithTextContent("Test"));
        var spans = page.GetTextSpans();
        spans.ShouldAllBe(static s => !string.IsNullOrEmpty(s.FontName));
    }

    [Fact]
    public async Task GetTextSpans_EmptyPage_ReturnsEmptyList()
    {
        var page = await LoadFirstPageAsync(PdfFixtures.SinglePage());
        var spans = page.GetTextSpans();
        spans.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTextSpans_SortedTopToBottom()
    {
        var page = await LoadFirstPageAsync(PdfFixtures.WithTextContent("Hello"));
        var spans = page.GetTextSpans();
        for (var i = 1; i < spans.Count; i++)
            spans[i].Y.ShouldBeLessThanOrEqualTo(spans[i - 1].Y + 0.01);
    }

    // ── ExtractText ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractText_PageWithTextContent_ReturnsNonEmptyString()
    {
        var page = await LoadFirstPageAsync(PdfFixtures.WithTextContent());
        var text = page.ExtractText();
        text.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExtractText_PageWithTextContent_ContainsOriginalWords()
    {
        var page = await LoadFirstPageAsync(PdfFixtures.WithTextContent());
        var text = page.ExtractText();
        text.ShouldContain("Hello");
        text.ShouldContain("Unchained");
    }

    [Fact]
    public async Task ExtractText_EmptyPage_ReturnsEmptyString()
    {
        var page = await LoadFirstPageAsync(PdfFixtures.SinglePage());
        page.ExtractText().ShouldBe(string.Empty);
    }

    // ── Table document text extraction ───────────────────────────────────────

    [Fact]
    public async Task ExtractText_TableGeneratedDocument_ContainsHeaderText()
    {
        var tableGen = new TableGenerator();
        var data = new TableData
        {
            Headers = ["Name", "Value", "Status"],
            Rows = [["Alice", "42", "Active"], ["Bob", "17", "Inactive"]]
        };
        await using var doc = await tableGen.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        var text = doc.Pages[1].ExtractText();
        text.ShouldContain("Name");
        text.ShouldContain("Value");
    }

    [Fact]
    public async Task ExtractText_TableGeneratedDocument_ContainsRowData()
    {
        var tableGen = new TableGenerator();
        var data = new TableData
        {
            Headers = ["Col"],
            Rows = [["CellValue"]]
        };
        await using var doc = await tableGen.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        var text = doc.Pages[1].ExtractText();
        text.ShouldContain("CellValue");
    }

    // ── Width and advance sanity ──────────────────────────────────────────────

    [Fact]
    public async Task GetTextSpans_SpanWidthIsPositive()
    {
        var page = await LoadFirstPageAsync(PdfFixtures.WithTextContent("ABC"));
        var spans = page.GetTextSpans();
        foreach (var span in spans.Where(static s => !string.IsNullOrEmpty(s.Text)))
            span.Width.ShouldBeGreaterThan(0);
    }

    // ── CTM-aware positioning ──────────────────────────────────────────────────

    [Fact]
    public async Task GetTextSpans_CmTranslate_ShiftsTextOrigin()
    {
        // Text drawn at Tm origin (0,0) but the CTM translates by (200, 500).
        var page = await LoadFirstPageAsync(
            PdfFixtures.WithRawContent(
                "q 1 0 0 1 200 500 cm BT /F1 12 Tf 0 0 Td (Shift) Tj ET Q"
            )
        );
        var span = page.GetTextSpans().First(static s => s.Text.Contains("Shift"));
        span.X.ShouldBe(200, 0.5);
        span.Y.ShouldBe(500, 0.5);
    }

    [Fact]
    public async Task GetTextSpans_CmScale_ScalesPositionAndFontSize()
    {
        // CTM scales ×2; a 10pt font at Td origin (50,100) → device (100,200), 20pt.
        var page = await LoadFirstPageAsync(
            PdfFixtures.WithRawContent(
                "q 2 0 0 2 0 0 cm BT /F1 10 Tf 50 100 Td (Scaled) Tj ET Q"
            )
        );
        var span = page.GetTextSpans().First(static s => s.Text.Contains("Scaled"));
        span.X.ShouldBe(100, 0.5);
        span.Y.ShouldBe(200, 0.5);
        span.FontSize.ShouldBe(20, 0.5);
    }

    [Fact]
    public async Task GetTextSpans_QRestoresCtm_LaterTextUnaffected()
    {
        // First block translated by (300,300) inside q/Q; second block after Q at (0,0).
        var page = await LoadFirstPageAsync(
            PdfFixtures.WithRawContent(
                "q 1 0 0 1 300 300 cm BT /F1 12 Tf 0 0 Td (Inner) Tj ET Q " +
                "BT /F1 12 Tf 10 20 Td (Outer) Tj ET"
            )
        );
        var inner = page.GetTextSpans().First(static s => s.Text.Contains("Inner"));
        var outer = page.GetTextSpans().First(static s => s.Text.Contains("Outer"));
        inner.X.ShouldBe(300, 0.5);
        inner.Y.ShouldBe(300, 0.5);
        outer.X.ShouldBe(10, 0.5); // CTM restored to identity by Q
        outer.Y.ShouldBe(20, 0.5);
    }

    [Fact]
    public async Task GetTextSpans_IdentityCtm_UnchangedFromTextMatrix()
    {
        // Regression guard: with no cm, position equals the text-matrix origin.
        var page = await LoadFirstPageAsync(
            PdfFixtures.WithRawContent(
                "BT /F1 12 Tf 72 144 Td (Plain) Tj ET"
            )
        );
        var span = page.GetTextSpans().First(static s => s.Text.Contains("Plain"));
        span.X.ShouldBe(72, 0.5);
        span.Y.ShouldBe(144, 0.5);
    }
}
