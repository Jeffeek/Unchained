using Shouldly;
using Unchained.Pdf.Engine;
using Xunit;
using Unchained.Pdf.Tests.Helpers;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class TextExtractionTests : PdfTestBase
{
    private static async Task<Abstractions.IPdfPage> LoadFirstPageAsync(byte[] pdfBytes)
    {
        var doc = await LoadAsync(pdfBytes);
        return doc.Pages[1];
    }

    // ── GetTextSpans ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTextSpans_PageWithTextContent_ReturnsSpans()
    {
        var page = await LoadFirstPageAsync(PdfFixtures.WithTextContent(text: "Hello Unchained"));
        var spans = page.GetTextSpans();
        spans.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetTextSpans_PageWithTextContent_ContainsExpectedText()
    {
        const string text = "Hello Unchained";
        var page = await LoadFirstPageAsync(PdfFixtures.WithTextContent(text: text));
        var spans = page.GetTextSpans();
        var allText = string.Concat(spans.Select(static s => s.Text));
        allText.ShouldContain("Hello");
        allText.ShouldContain("Unchained");
    }

    [Fact]
    public async Task GetTextSpans_SpansHavePositiveYCoordinate()
    {
        var page = await LoadFirstPageAsync(PdfFixtures.WithTextContent(text: "Test"));
        var spans = page.GetTextSpans();
        spans.ShouldAllBe(static s => s.Y >= 0);
    }

    [Fact]
    public async Task GetTextSpans_SpansHavePositiveFontSize()
    {
        var page = await LoadFirstPageAsync(PdfFixtures.WithTextContent(text: "Test"));
        var spans = page.GetTextSpans();
        spans.ShouldAllBe(static s => s.FontSize > 0);
    }

    [Fact]
    public async Task GetTextSpans_SpansHaveNonEmptyFontName()
    {
        var page = await LoadFirstPageAsync(PdfFixtures.WithTextContent(text: "Test"));
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
        var page = await LoadFirstPageAsync(PdfFixtures.WithTextContent(text: "Hello"));
        var spans = page.GetTextSpans();
        for (var i = 1; i < spans.Count; i++)
            spans[i].Y.ShouldBeLessThanOrEqualTo(spans[i - 1].Y + 0.01);
    }

    // ── ExtractText ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractText_PageWithTextContent_ReturnsNonEmptyString()
    {
        var page = await LoadFirstPageAsync(PdfFixtures.WithTextContent(text: "Hello Unchained"));
        var text = page.ExtractText();
        text.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExtractText_PageWithTextContent_ContainsOriginalWords()
    {
        const string original = "Hello Unchained";
        var page = await LoadFirstPageAsync(PdfFixtures.WithTextContent(text: original));
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
        var data = new Models.TableData
        {
            Headers = ["Name", "Value", "Status"],
            Rows = [["Alice", "42", "Active"], ["Bob", "17", "Inactive"]]
        };
        await using var doc = await tableGen.GenerateAsync(data, Models.TableStyle.Default, ct: TestContext.Current.CancellationToken);
        var text = doc.Pages[1].ExtractText();
        text.ShouldContain("Name");
        text.ShouldContain("Value");
    }

    [Fact]
    public async Task ExtractText_TableGeneratedDocument_ContainsRowData()
    {
        var tableGen = new TableGenerator();
        var data = new Models.TableData
        {
            Headers = ["Col"],
            Rows = [["CellValue"]]
        };
        await using var doc = await tableGen.GenerateAsync(data, Models.TableStyle.Default, ct: TestContext.Current.CancellationToken);
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
}
