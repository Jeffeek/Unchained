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
        var data = SimpleData(0);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task GenerateAsync_FewRows_FitOnOnePage()
    {
        var data = SimpleData(5);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task GenerateAsync_ManyRows_SpanMultiplePages()
    {
        var data = SimpleData(200);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task GenerateAsync_PageCountMatchesExpectedPageSlices()
    {
        const int rows = 150;
        var data = SimpleData(rows);
        var layout = TableLayout.Compute(data.Headers.Count, TableStyle.Default, false);
        var expectedPages = (int)Math.Ceiling((double)rows / layout.RowsPerPage);

        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);

        doc.PageCount.ShouldBe(expectedPages);
    }

    [Fact]
    public async Task GenerateAsync_WithTitle_DocumentStillParseableAfterSave()
    {
        var data = new TableData
        {
            Headers = SimpleData(3).Headers,
            Rows = SimpleData(3).Rows,
            Title = "My Report"
        };
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task GenerateAsync_RoundTrip_DocumentIsParseableAfterSave()
    {
        var data = SimpleData(10);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(doc.PageCount);
    }

    [Fact]
    public async Task GenerateAsync_ContentStream_ContainsBtEtOperators()
    {
        var data = SimpleData(2);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        var operators = doc.Pages[1].GetContentOperators();
        operators.ShouldContain(static op => op.Name == "BT");
        operators.ShouldContain(static op => op.Name == "ET");
    }

    [Fact]
    public async Task GenerateAsync_ContentStream_ContainsTjOperators()
    {
        var data = SimpleData(1);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        var operators = doc.Pages[1].GetContentOperators();
        operators.ShouldContain(static op => op.Name == "Tj");
    }

    [Fact]
    public async Task GenerateAsync_DrawBorders_ContentStreamContainsReAndSOPerators()
    {
        var data = SimpleData(2);
        await using var doc = await Generator.GenerateAsync(data, new TableStyle(DrawBorders: true), TestContext.Current.CancellationToken);
        var operators = doc.Pages[1].GetContentOperators();
        operators.ShouldContain(static op => op.Name == "re");
        operators.ShouldContain(static op => op.Name == "S");
    }

    [Fact]
    public async Task GenerateAsync_AlternatingRowColor_ContentStreamContainsFillOperators()
    {
        var data = SimpleData(4);
        await using var doc = await Generator.GenerateAsync(data, new TableStyle(AlternatingRowColor: true), TestContext.Current.CancellationToken);
        var operators = doc.Pages[1].GetContentOperators();
        operators.ShouldContain(static op => op.Name == "f");
    }

    [Fact]
    public async Task GenerateAsync_Compact_PageCountSameAsDefaultForSmallTable()
    {
        var data = SimpleData(5);
        await using var compact = await Generator.GenerateAsync(data, TableStyle.Compact, TestContext.Current.CancellationToken);
        await using var defaultDoc = await Generator.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        compact.PageCount.ShouldBe(defaultDoc.PageCount);
    }

    [Fact]
    public async Task GenerateAsync_Cancellation_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Generator.GenerateAsync(SimpleData(1), TableStyle.Default, cts.Token));
    }

    // ── AppendTableAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task AppendTableAsync_SinglePage_PageCountIncreasesByOne()
    {
        var data = SimpleData(3);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        var before = doc.PageCount;

        await Generator.AppendTableAsync(doc, data, TableStyle.Default, TestContext.Current.CancellationToken);

        doc.PageCount.ShouldBe(before + 1);
    }

    [Fact]
    public async Task AppendTableAsync_MultipleAppends_PageCountAccumulates()
    {
        var data = SimpleData(2);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        await Generator.AppendTableAsync(doc, data, TableStyle.Default, TestContext.Current.CancellationToken);
        await Generator.AppendTableAsync(doc, data, TableStyle.Default, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(3);
    }

    [Fact]
    public async Task AppendTableAsync_RoundTrip_DocumentIsParseableAfterSave()
    {
        var data = SimpleData(5);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        await Generator.AppendTableAsync(doc, data, TableStyle.Default, TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);

        reloaded.PageCount.ShouldBe(2);
    }

    [Fact]
    public async Task AppendTableAsync_AppendToExistingDocument_PageCountIncreased()
    {
        var fixtures = PdfFixtures.MultiPage(3);
        await using var doc = await LoadAsync(fixtures, TestContext.Current.CancellationToken);
        var before = doc.PageCount;

        await Generator.AppendTableAsync(doc, SimpleData(2), TableStyle.Default, TestContext.Current.CancellationToken);

        doc.PageCount.ShouldBe(before + 1);
    }

    [Fact]
    public async Task AppendTableAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var data = SimpleData(1);
        await using var doc = await Generator.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Generator.AppendTableAsync(doc, data, TableStyle.Default, cts.Token));
    }
}
