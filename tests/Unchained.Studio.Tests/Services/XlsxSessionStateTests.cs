using Unchained.Studio.Services;
using Unchained.Xlsx.Abstractions;
using Unchained.Xlsx.Engine;

namespace Unchained.Studio.Tests.Services;

/// <summary>
///     Tests for <see cref="XlsxSessionState" /> and the shared
///     <see cref="DocumentSessionBase{TDocument,TProcessor}" /> lifecycle it drives:
///     create, dirty tracking, tree rebuild, refresh (serialize + reload), and disposal.
/// </summary>
public sealed class XlsxSessionStateTests
{
    private static async Task<byte[]> BlankWorkbookBytesAsync(ISpreadsheetProcessor processor)
    {
        using var document = processor.CreateBlank("Sheet1");
        using var ms = new MemoryStream();
        await processor.SaveAsync(document, ms, cancellationToken: TestContext.Current.CancellationToken);
        return ms.ToArray();
    }

    [Fact]
    public void CreateBlank_StartsDirtyWithEmptyBytesAndTree()
    {
        using var processor = new SpreadsheetProcessor();

        var state = XlsxSessionState.CreateBlank(processor, "wb.xlsx");

        state.FileName.ShouldBe("wb.xlsx");
        state.IsDirty.ShouldBeTrue();
        state.CurrentBytes.ShouldBeEmpty();
        state.CurrentSheet.ShouldBe(1);
        state.Tree.Label.ShouldBe("wb.xlsx");
    }

    [Fact]
    public async Task CreateAsync_LoadsBytesAndStartsClean()
    {
        using var processor = new SpreadsheetProcessor();
        var bytes = await BlankWorkbookBytesAsync(processor);

        var state = await XlsxSessionState.CreateAsync(processor, bytes, "loaded.xlsx", TestContext.Current.CancellationToken);

        state.IsDirty.ShouldBeFalse();
        state.FileSizeBytes.ShouldBe(bytes.LongLength);
        state.Document.Sheets.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task MarkDirty_SetsDirtyAndRaisesRefreshed()
    {
        using var processor = new SpreadsheetProcessor();
        var state = await XlsxSessionState.CreateAsync(processor, await BlankWorkbookBytesAsync(processor), "wb.xlsx", TestContext.Current.CancellationToken);
        var raised = false;
        state.Refreshed += () => raised = true;

        state.MarkDirty();

        state.IsDirty.ShouldBeTrue();
        raised.ShouldBeTrue();
    }

    [Fact]
    public async Task RebuildTree_RaisesRefreshedWithoutMarkingDirty()
    {
        using var processor = new SpreadsheetProcessor();
        var state = await XlsxSessionState.CreateAsync(processor, await BlankWorkbookBytesAsync(processor), "wb.xlsx", TestContext.Current.CancellationToken);
        var raised = false;
        state.Refreshed += () => raised = true;

        state.RebuildTree();

        state.IsDirty.ShouldBeFalse();
        raised.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshAsync_SerializesReloadsAndClearsDirty()
    {
        using var processor = new SpreadsheetProcessor();
        var state = XlsxSessionState.CreateBlank(processor);
        state.Document.Sheets.Add("Extra");
        var raised = false;
        state.Refreshed += () => raised = true;

        await state.RefreshAsync(TestContext.Current.CancellationToken);

        state.IsDirty.ShouldBeFalse();
        state.CurrentBytes.ShouldNotBeEmpty();
        state.FileSizeBytes.ShouldBe(state.CurrentBytes.LongLength);
        state.Document.Sheets.Count.ShouldBe(2);
        raised.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshAsync_ClampsCurrentSheetToSheetCount()
    {
        using var processor = new SpreadsheetProcessor();
        var state = XlsxSessionState.CreateBlank(processor);
        state.CurrentSheet = 99;

        await state.RefreshAsync(TestContext.Current.CancellationToken);

        state.CurrentSheet.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        using var processor = new SpreadsheetProcessor();
        var state = XlsxSessionState.CreateBlank(processor);

        await state.DisposeAsync();

        await Should.NotThrowAsync(async () => await state.DisposeAsync());
    }
}
