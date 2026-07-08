using Unchained.Studio.Models;

namespace Unchained.Studio.Services;

/// <summary>
///     Base class for document session state.
///     Eliminates the duplicated save→reload→rebuild-tree→raise-Refreshed cycle across Pdf/Pptx/Xlsx.
///     Subclasses implement:
///     - <see cref="BuildTree" /> — tree construction for the document type
///     - <see cref="Dispose" /> — document disposal (usually just DisposeAsync)
///     The base class owns:
///     - <see cref="Document" /> / <see cref="CurrentBytes" /> / <see cref="Tree" /> lifecycle
///     - <see cref="RefreshAsync" /> — canonicalise by serialising + reloading
///     - <see cref="MarkDirty" /> — flags dirty, rebuilds tree, raises Refreshed
/// </summary>
public abstract class DocumentSessionBase<TDocument, TProcessor> : IAsyncDisposable
    where TDocument : class
    where TProcessor : class
{
    private bool _disposed;

    protected DocumentSessionBase(
        string fileName,
        byte[] bytes,
        TDocument document,
        TProcessor processor
    )
    {
        FileName = fileName;
        CurrentBytes = bytes;
        FileSizeBytes = bytes.LongLength;
        Document = document;
        Processor = processor;
        Tree = BuildTree(document, fileName);
    }

    /// <summary>The parsed document model.</summary>
    public TDocument Document { get; protected set; }

    /// <summary>The processor that loads and serialises this document type.</summary>
    public TProcessor Processor { get; }

    public string FileName { get; }
    public long FileSizeBytes { get; protected set; }
    public byte[] CurrentBytes { get; protected set; }

    public TreeNode Tree { get; protected set; }
    public TreeNode? SelectedNode { get; set; }

    public bool IsDirty { get; protected set; }

    public virtual ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        return Dispose();
    }

    /// <summary>
    ///     Raised after <see cref="RefreshAsync" />, <see cref="MarkDirty" />,
    ///     or <see cref="RebuildTree" /> completes so the UI can re-render.
    /// </summary>
    public event Action? Refreshed;

    /// <summary>Raises the Refreshed event.</summary>
    protected virtual void RaiseRefreshed() => Refreshed?.Invoke();

    /// <summary>Serialise the live document to bytes without reloading.</summary>
    protected abstract Task<byte[]> SerializeAsync(CancellationToken ct = default);

    /// <summary>Serialise, reload, update bytes, clear dirty, rebuild tree, raise Refreshed.</summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var newBytes = await SerializeAsync(ct).ConfigureAwait(false);
        var oldDocument = Document;

        Document = await ReloadAsync(newBytes, ct).ConfigureAwait(false);
        CurrentBytes = newBytes;
        FileSizeBytes = newBytes.LongLength;
        IsDirty = false;
        OnReloaded();
        Tree = BuildTree(Document, FileName);

        switch (oldDocument)
        {
            case IAsyncDisposable oldAsync:
                await oldAsync.DisposeAsync().ConfigureAwait(false);
            break;
            case IDisposable oldSync:
                oldSync.Dispose();
            break;
        }

        RaiseRefreshed();
    }

    /// <summary>Flags dirty, rebuilds tree, raises Refreshed.</summary>
    public void MarkDirty()
    {
        IsDirty = true;
        Tree = BuildTree(Document, FileName);
        RaiseRefreshed();
    }

    /// <summary>Rebuilds tree and raises Refreshed without flagging dirty.</summary>
    public void RebuildTree()
    {
        Tree = BuildTree(Document, FileName);
        RaiseRefreshed();
    }

    /// <summary>
    ///     Called inside <see cref="RefreshAsync" /> after the document is reloaded but before the
    ///     tree is rebuilt, so subclasses can reconcile derived state (e.g. clamp an active-sheet index).
    /// </summary>
    protected virtual void OnReloaded() { }

    /// <summary>Constructs the navigable tree for the current document.</summary>
    protected abstract TreeNode BuildTree(TDocument document, string fileName);

    /// <summary>Loads a document from bytes (format-specific implementation).</summary>
    protected abstract Task<TDocument> ReloadAsync(byte[] bytes, CancellationToken ct = default);

    /// <summary>Disposes the underlying document. Override in subclass.</summary>
    protected virtual ValueTask Dispose() => ValueTask.CompletedTask;
}
