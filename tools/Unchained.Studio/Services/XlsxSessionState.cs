using Unchained.Studio.Models;
using Unchained.Studio.Studio.Xlsx;
using Unchained.Xlsx.Engine;

namespace Unchained.Studio.Services;

/// <summary>
///     Holds the currently-loaded XLSX workbook for one Blazor circuit, along with the navigable
///     tree, the raw bytes, and the active sheet. Mirrors <see cref="PptxSessionState" />.
/// </summary>
/// <remarks>
///     Edits mutate the live <see cref="SpreadsheetDocument" /> in place; <see cref="MarkDirty" />
///     flags that the in-memory model has diverged from <see cref="CurrentBytes" />. Call
///     <see cref="RefreshAsync" /> to re-serialize + reload (canonicalising the model and rebuilding
///     the tree), or download directly from the live document via <see cref="SerializeAsync" />.
/// </remarks>
public sealed class XlsxSessionState : IAsyncDisposable
{
    private XlsxSessionState(
        SpreadsheetProcessor processor,
        SpreadsheetDocument document,
        byte[] bytes,
        string fileName
    )
    {
        Processor = processor;
        Document = document;
        CurrentBytes = bytes;
        FileName = fileName;
        FileSizeBytes = bytes.LongLength;
        Tree = XlsxTreeBuilder.Build(document, fileName);
    }

    public SpreadsheetProcessor Processor { get; }
    public SpreadsheetDocument Document { get; private set; }
    public string FileName { get; }
    public long FileSizeBytes { get; private set; }
    public byte[] CurrentBytes { get; private set; }
    public TreeNode Tree { get; private set; }
    public TreeNode? SelectedNode { get; set; }

    /// <summary>The 1-based index of the active sheet tab.</summary>
    public int CurrentSheet { get; set; } = 1;

    /// <summary><see langword="true" /> when the in-memory model has unsaved edits vs <see cref="CurrentBytes" />.</summary>
    public bool IsDirty { get; private set; }

    public ValueTask DisposeAsync()
    {
        Document.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Raised after the tree or model changes so the UI can re-render.</summary>
    public event Action? Refreshed;

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

    /// <summary>Flags the model dirty and rebuilds the tree from the live document.</summary>
    public void MarkDirty()
    {
        IsDirty = true;
        Tree = XlsxTreeBuilder.Build(Document, FileName);
        Refreshed?.Invoke();
    }

    /// <summary>Rebuilds the tree and notifies the UI without flagging dirty (e.g. after selection-only changes).</summary>
    public void RebuildTree()
    {
        Tree = XlsxTreeBuilder.Build(Document, FileName);
        Refreshed?.Invoke();
    }

    /// <summary>Serialises the live document to bytes (honouring any password) without reloading.</summary>
    public async Task<byte[]> SerializeAsync(CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await Processor.SaveAsync(Document, ms, cancellationToken: ct).ConfigureAwait(false);
        return ms.ToArray();
    }

    /// <summary>
    ///     Serialises the current document, reloads it (canonicalising), refreshes the cached bytes,
    ///     clears the dirty flag, and rebuilds the tree.
    /// </summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var newBytes = await SerializeAsync(ct).ConfigureAwait(false);
        var oldDocument = Document;

        Document = await Processor.LoadAsync(newBytes, cancellationToken: ct).ConfigureAwait(false);
        CurrentBytes = newBytes;
        FileSizeBytes = newBytes.LongLength;
        IsDirty = false;
        CurrentSheet = Math.Clamp(CurrentSheet, 1, Math.Max(1, Document.Sheets.Count));
        Tree = XlsxTreeBuilder.Build(Document, FileName);

        oldDocument.Dispose();

        Refreshed?.Invoke();
    }
}
