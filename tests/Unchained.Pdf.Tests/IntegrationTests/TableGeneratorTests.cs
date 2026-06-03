using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class TableGeneratorTests : PdfTestBase
{
    private static readonly TableGenerator Generator = new();

    // Shortcut to the shared fixture factory.
    private static TableData SimpleData(int rows, int cols = 3) =>
        PdfFixtures.SimpleTableData(rows, cols);

    // ── GenerateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_EmptyRows_ProducesOnePageDocument()
    {
        var data = SimpleData(rows: 0);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task GenerateAsync_FewRows_FitOnOnePage()
    {
        var data = SimpleData(rows: 5);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task GenerateAsync_ManyRows_SpanMultiplePages()
    {
        var data = SimpleData(rows: 200);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task GenerateAsync_PageCountMatchesExpectedPageSlices()
    {
        const int rows = 150;
        var data = SimpleData(rows);
        var layout = TableLayout.Compute(data.Headers.Count, TableStyle.Default, hasTitle: false);
        var expectedPages = (int)Math.Ceiling((double)rows / layout.RowsPerPage);

        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, ct: TestContext.Current.CancellationToken);

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
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, ct: TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task GenerateAsync_RoundTrip_DocumentIsParseableAfterSave()
    {
        var data = SimpleData(rows: 10);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, ct: TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(doc.PageCount);
    }

    [Fact]
    public async Task GenerateAsync_ContentStream_ContainsBtEtOperators()
    {
        var data = SimpleData(rows: 2);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, ct: TestContext.Current.CancellationToken);
        var operators = doc.Pages[1].GetContentOperators();
        operators.ShouldContain(static op => op.Name == "BT");
        operators.ShouldContain(static op => op.Name == "ET");
    }

    [Fact]
    public async Task GenerateAsync_ContentStream_ContainsTjOperators()
    {
        var data = SimpleData(rows: 1);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, ct: TestContext.Current.CancellationToken);
        var operators = doc.Pages[1].GetContentOperators();
        operators.ShouldContain(static op => op.Name == "Tj");
    }

    [Fact]
    public async Task GenerateAsync_DrawBorders_ContentStreamContainsReAndSOPerators()
    {
        var data = SimpleData(rows: 2);
        await using var doc = await Generator.GenerateAsync(data, new TableStyle(DrawBorders: true), ct: TestContext.Current.CancellationToken);
        var operators = doc.Pages[1].GetContentOperators();
        operators.ShouldContain(static op => op.Name == "re");
        operators.ShouldContain(static op => op.Name == "S");
    }

    [Fact]
    public async Task GenerateAsync_AlternatingRowColor_ContentStreamContainsFillOperators()
    {
        var data = SimpleData(rows: 4);
        await using var doc = await Generator.GenerateAsync(data, new TableStyle(AlternatingRowColor: true), ct: TestContext.Current.CancellationToken);
        var operators = doc.Pages[1].GetContentOperators();
        operators.ShouldContain(static op => op.Name == "f");
    }

    [Fact]
    public async Task GenerateAsync_Compact_PageCountSameAsDefaultForSmallTable()
    {
        var data = SimpleData(rows: 5);
        await using var compact = await Generator.GenerateAsync(data, TableStyle.Compact, ct: TestContext.Current.CancellationToken);
        await using var defaultDoc = await Generator.GenerateAsync(data, TableStyle.Default, ct: TestContext.Current.CancellationToken);
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
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, ct: TestContext.Current.CancellationToken);
        var before = doc.PageCount;

        await Generator.AppendTableAsync(doc, data, TableStyle.Default, ct: TestContext.Current.CancellationToken);

        doc.PageCount.ShouldBe(before + 1);
    }

    [Fact]
    public async Task AppendTableAsync_MultipleAppends_PageCountAccumulates()
    {
        var data = SimpleData(rows: 2);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, ct: TestContext.Current.CancellationToken);
        await Generator.AppendTableAsync(doc, data, TableStyle.Default, ct: TestContext.Current.CancellationToken);
        await Generator.AppendTableAsync(doc, data, TableStyle.Default, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(3);
    }

    [Fact]
    public async Task AppendTableAsync_RoundTrip_DocumentIsParseableAfterSave()
    {
        var data = SimpleData(rows: 5);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, ct: TestContext.Current.CancellationToken);
        await Generator.AppendTableAsync(doc, data, TableStyle.Default, ct: TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, ct: TestContext.Current.CancellationToken);

        reloaded.PageCount.ShouldBe(2);
    }

    [Fact]
    public async Task AppendTableAsync_AppendToExistingDocument_PageCountIncreased()
    {
        var fixtures = PdfFixtures.MultiPage(count: 3);
        await using var doc = await LoadAsync(fixtures, ct: TestContext.Current.CancellationToken);
        var before = doc.PageCount;

        await Generator.AppendTableAsync(doc, SimpleData(rows: 2), TableStyle.Default, ct: TestContext.Current.CancellationToken);

        doc.PageCount.ShouldBe(before + 1);
    }

    [Fact]
    public async Task AppendTableAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var data = SimpleData(rows: 1);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, ct: TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Generator.AppendTableAsync(doc, data, TableStyle.Default, cts.Token));
    }
}
