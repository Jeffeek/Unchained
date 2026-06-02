using Shouldly;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests.FormatConversion;

public sealed class MarkdownToPdfTests : PdfTestBase
{
    private const string SimpleMarkdown = """
        # Heading 1

        This is a paragraph with **bold** and *italic* text.

        ## Heading 2

        Another paragraph.

        - Item one
        - Item two
        - Item three

        ```
        code block
        ```
        """;

    [Fact]
    public async Task LoadFromMarkdown_SimpleDocument_ProducesValidPdf()
    {
        await using var doc = await Processor.LoadFromMarkdownAsync(SimpleMarkdown);
        doc.PageCount.ShouldBeGreaterThan(0);
        doc.Pages[1].Width.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task LoadFromMarkdown_EmptyString_ProducesAtLeastOnePage()
    {
        await using var doc = await Processor.LoadFromMarkdownAsync(string.Empty);
        doc.PageCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task LoadFromMarkdown_Headings_ProducesSinglePage()
    {
        const string md = "# Title\n\n## Section\n\nBody text here.";
        await using var doc = await Processor.LoadFromMarkdownAsync(md);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadFromMarkdown_LongDocument_Paginates()
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 1; i <= 30; i++)
            sb.AppendLine($"## Section {i}\n\n{string.Join(" ", Enumerable.Repeat("This is body text.", 10))}\n");
        await using var doc = await Processor.LoadFromMarkdownAsync(sb.ToString());
        doc.PageCount.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task LoadFromMarkdown_OrderedList_ProducesValidPdf()
    {
        const string md = "1. First item\n2. Second item\n3. Third item\n";
        await using var doc = await Processor.LoadFromMarkdownAsync(md);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadFromMarkdown_ThematicBreak_ProducesValidPdf()
    {
        const string md = "Before\n\n---\n\nAfter";
        await using var doc = await Processor.LoadFromMarkdownAsync(md);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadFromMarkdown_RoundTrip_PreservesPageCount()
    {
        await using var doc = await Processor.LoadFromMarkdownAsync(SimpleMarkdown);
        await using var reloaded = await SaveAndReloadAsync(doc);
        reloaded.PageCount.ShouldBe(doc.PageCount);
    }
}
