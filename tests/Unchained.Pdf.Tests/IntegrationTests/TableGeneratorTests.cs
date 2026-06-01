using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class TableGeneratorTests
{
    private static readonly TableGenerator Generator = new();
    private static readonly DocumentProcessor Processor = new();

    private static TableData SimpleData(int rows, int cols = 3) => new()
    {
        Headers = Enumerable.Range(1, cols).Select(static i => $"Col{i}").ToList(),
        Rows = Enumerable.Range(0, rows)
            .Select(IReadOnlyList<string> (r) => Enumerable.Range(1, cols)
                .Select(c => $"R{r}C{c}").ToList())
            .ToList()
    };

    // ── GenerateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_EmptyRows_ProducesOnePageDocument()
    {
        var data = SimpleData(rows: 0);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task GenerateAsync_FewRows_FitOnOnePage()
    {
        var data = SimpleData(rows: 5);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task GenerateAsync_ManyRows_SpanMultiplePages()
    {
        var data = SimpleData(rows: 200);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default);
        doc.PageCount.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task GenerateAsync_PageCountMatchesExpectedPageSlices()
    {
        const int rows = 150;
        var data = SimpleData(rows);
        var layout = TableLayout.Compute(data.Headers.Count, TableStyle.Default, hasTitle: false);
        var expectedPages = (int)Math.Ceiling((double)rows / layout.RowsPerPage);

        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default);

        doc.PageCount.ShouldBe(expectedPages);
    }

    [Fact]
    public async Task GenerateAsync_WithTitle_DocumentStillParseableAfterSave()
    {
        var data = new TableData
        {
            Headers = SimpleData(rows: 3).Headers,
            Rows = SimpleData(rows: 3).Rows,
            Title = "My Report"
        };
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);
        ms.Position = 0;
        await using var reloaded = await Processor.LoadAsync(ms);
        reloaded.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task GenerateAsync_RoundTrip_DocumentIsParseableAfterSave()
    {
        var data = SimpleData(rows: 10);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);
        ms.Position = 0;
        await using var reloaded = await Processor.LoadAsync(ms);
        reloaded.PageCount.ShouldBe(doc.PageCount);
    }

    [Fact]
    public async Task GenerateAsync_ContentStream_ContainsBtEtOperators()
    {
        var data = SimpleData(rows: 2);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default);
        var operators = doc.Pages[1].GetContentOperators();
        operators.ShouldContain(static op => op.Name == "BT");
        operators.ShouldContain(static op => op.Name == "ET");
    }

    [Fact]
    public async Task GenerateAsync_ContentStream_ContainsTjOperators()
    {
        var data = SimpleData(rows: 1);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default);
        var operators = doc.Pages[1].GetContentOperators();
        operators.ShouldContain(static op => op.Name == "Tj");
    }

    [Fact]
    public async Task GenerateAsync_DrawBorders_ContentStreamContainsReAndSOPerators()
    {
        var data = SimpleData(rows: 2);
        await using var doc = await Generator.GenerateAsync(data, new TableStyle(DrawBorders: true));
        var operators = doc.Pages[1].GetContentOperators();
        operators.ShouldContain(static op => op.Name == "re");
        operators.ShouldContain(static op => op.Name == "S");
    }

    [Fact]
    public async Task GenerateAsync_AlternatingRowColor_ContentStreamContainsFillOperators()
    {
        var data = SimpleData(rows: 4);
        await using var doc = await Generator.GenerateAsync(data, new TableStyle(AlternatingRowColor: true));
        var operators = doc.Pages[1].GetContentOperators();
        operators.ShouldContain(static op => op.Name == "f");
    }

    [Fact]
    public async Task GenerateAsync_Compact_PageCountSameAsDefaultForSmallTable()
    {
        var data = SimpleData(rows: 5);
        await using var compact = await Generator.GenerateAsync(data, TableStyle.Compact);
        await using var defaultDoc = await Generator.GenerateAsync(data, TableStyle.Default);
        compact.PageCount.ShouldBe(defaultDoc.PageCount);
    }

    [Fact]
    public async Task GenerateAsync_Cancellation_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Generator.GenerateAsync(SimpleData(rows: 1), TableStyle.Default, cts.Token));
    }

    // ── AppendTableAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task AppendTableAsync_SinglePage_PageCountIncreasesByOne()
    {
        var data = SimpleData(rows: 3);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default);
        var before = doc.PageCount;

        await Generator.AppendTableAsync(doc, data, TableStyle.Default);

        doc.PageCount.ShouldBe(before + 1);
    }

    [Fact]
    public async Task AppendTableAsync_MultipleAppends_PageCountAccumulates()
    {
        var data = SimpleData(rows: 2);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default);
        await Generator.AppendTableAsync(doc, data, TableStyle.Default);
        await Generator.AppendTableAsync(doc, data, TableStyle.Default);
        doc.PageCount.ShouldBe(3);
    }

    [Fact]
    public async Task AppendTableAsync_RoundTrip_DocumentIsParseableAfterSave()
    {
        var data = SimpleData(rows: 5);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default);
        await Generator.AppendTableAsync(doc, data, TableStyle.Default);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);
        ms.Position = 0;
        await using var reloaded = await Processor.LoadAsync(ms);

        reloaded.PageCount.ShouldBe(2);
    }

    [Fact]
    public async Task AppendTableAsync_AppendToExistingDocument_PageCountIncreased()
    {
        var fixtures = Helpers.PdfFixtures.MultiPage(count: 3);
        await using var doc = await Processor.LoadAsync(new MemoryStream(fixtures));
        var before = doc.PageCount;

        await Generator.AppendTableAsync(doc, SimpleData(rows: 2), TableStyle.Default);

        doc.PageCount.ShouldBe(before + 1);
    }

    [Fact]
    public async Task AppendTableAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var data = SimpleData(rows: 1);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Generator.AppendTableAsync(doc, data, TableStyle.Default, cts.Token));
    }
}
