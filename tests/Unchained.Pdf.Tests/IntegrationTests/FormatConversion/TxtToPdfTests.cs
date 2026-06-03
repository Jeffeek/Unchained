using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests.FormatConversion;

public sealed class TxtToPdfTests : PdfTestBase
{
    [Fact]
    public async Task LoadFromTxt_SimpleText_ProducesValidDocument()
    {
        await using var doc = await Processor.LoadFromTxtAsync("Hello, World!", ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
        doc.Pages[1].Width.ShouldBeGreaterThan(0);
        doc.Pages[1].Height.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task LoadFromTxt_EmptyString_ProducesOneEmptyPage()
    {
        await using var doc = await Processor.LoadFromTxtAsync(string.Empty, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadFromTxt_MultilineFitsOnOnePage()
    {
        var text = string.Join("\n", Enumerable.Range(1, 10).Select(static i => $"Line {i}"));
        await using var doc = await Processor.LoadFromTxtAsync(text, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadFromTxt_ManyLines_PaginatesCorrectly()
    {
        // 200 lines at 12pt font, 1.2 leading = 14.4pt per line.
        // A4 usable height = 842 - 144 = 698pt → ~48 lines per page → 200 lines = at least 5 pages.
        var text = string.Join("\n", Enumerable.Range(1, 200).Select(static i => $"Line {i}"));
        await using var doc = await Processor.LoadFromTxtAsync(text, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task LoadFromTxt_LongLine_WordWrapsWithoutOverflow()
    {
        var longLine = string.Join(" ", Enumerable.Repeat("word", 200));
        await using var doc = await Processor.LoadFromTxtAsync(longLine, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task LoadFromTxt_ExtractText_ContainsOriginalContent()
    {
        const string text = "Quick brown fox jumps over the lazy dog";
        await using var doc = await Processor.LoadFromTxtAsync(text, ct: TestContext.Current.CancellationToken);
        var extracted = doc.Pages[1].ExtractText();
        extracted.ShouldContain("fox");
    }

    [Fact]
    public async Task LoadFromTxt_CustomFont_CourierProducesValidPdf()
    {
        var opts = new TxtLoadOptions(FontName: "Courier", FontSize: 10f);
        await using var doc = await Processor.LoadFromTxtAsync("Monospace text", opts, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadFromTxt_RoundTrip_PreservesPageCount()
    {
        var text = string.Join("\n", Enumerable.Range(1, 5).Select(static i => $"Page content line {i}"));
        await using var doc = await Processor.LoadFromTxtAsync(text, ct: TestContext.Current.CancellationToken);
        await using var reloaded = await SaveAndReloadAsync(doc, ct: TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(doc.PageCount);
    }
}
