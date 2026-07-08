using Unchained.Studio.Models;
using Unchained.Studio.Studio.Xlsx;
using Unchained.Xlsx.Engine;

namespace Unchained.Studio.Services;

/// <summary>
///     Holds the currently-loaded XLSX workbook for one Blazor circuit, along with the navigable
///     tree, the raw bytes, and the active sheet. Mirrors <see cref="PptxSessionState" />.
/// </summary>
/// <remarks>
///     Edits mutate the live <see cref="SpreadsheetDocument" /> in place;
///     <see cref="DocumentSessionBase{TDocument,TProcessor}.MarkDirty" />
///     flags that the in-memory model has diverged from
///     <see cref="DocumentSessionBase{TDocument,TProcessor}.CurrentBytes" />. Call
///     <see cref="DocumentSessionBase{TDocument,TProcessor}.RefreshAsync" /> to re-serialize + reload (canonicalising the
///     model and rebuilding
///     the tree), or download directly from the live document via <see cref="SerializeAsync" />.
/// </remarks>
public sealed class XlsxSessionState : DocumentSessionBase<SpreadsheetDocument, SpreadsheetProcessor>
{
    private XlsxSessionState(
        SpreadsheetProcessor processor,
        SpreadsheetDocument document,
        byte[] bytes,
        string fileName
    ) : base(fileName, bytes, document, processor) { }

    /// <summary>The 1-based index of the active sheet tab.</summary>
    public int CurrentSheet { get; set; } = 1;

    public static async Task<XlsxSessionState> CreateAsync(
        SpreadsheetProcessor processor,
        byte[] bytes,
        string fileName,
        CancellationToken ct = default
    )
    {
        var document = await processor.LoadAsync(bytes, cancellationToken: ct).ConfigureAwait(false);
        return new XlsxSessionState(processor, document, bytes, fileName);
    }

    public static XlsxSessionState CreateBlank(
        SpreadsheetProcessor processor,
        string fileName = "workbook.xlsx"
    )
    {
        var document = processor.CreateBlank("Sheet1");
        return new XlsxSessionState(processor, document, [], fileName) { IsDirty = true };
    }

    protected override async Task<byte[]> SerializeAsync(CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await Processor.SaveAsync(Document, ms, cancellationToken: ct).ConfigureAwait(false);
        return ms.ToArray();
    }

    protected override TreeNode BuildTree(SpreadsheetDocument document, string fileName) =>
        XlsxTreeBuilder.Build(document, fileName);

    protected override Task<SpreadsheetDocument> ReloadAsync(byte[] bytes, CancellationToken ct = default) =>
        Processor.LoadAsync(bytes, cancellationToken: ct);

    // SpreadsheetDocument.Dispose() is synchronous; the base RefreshAsync routes it through its
    // IDisposable switch, so no RefreshAsync override is needed — only the active-sheet reconciliation.
    protected override void OnReloaded() =>
        CurrentSheet = Math.Clamp(CurrentSheet, 1, Math.Max(1, Document.Sheets.Count));

    protected override ValueTask Dispose()
    {
        Document.Dispose();
        return ValueTask.CompletedTask;
    }
}
