using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Engine;
using Unchained.Studio.Models;
using Unchained.Studio.Studio.Pdf;

namespace Unchained.Studio.Services;

public sealed class PdfSessionState : IAsyncDisposable
{
    private PdfSessionState(
        DocumentProcessor processor,
        IPdfDocument document,
        byte[] bytes,
        string fileName
    )
    {
        Processor = processor;
        Document = document;
        CurrentBytes = bytes;
        FileName = fileName;
        FileSizeBytes = bytes.LongLength;
        Tree = PdfTreeBuilder.Build(document, fileName);
    }

    public DocumentProcessor Processor { get; }
    public IPdfDocument Document { get; private set; }
    public string FileName { get; }
    public long FileSizeBytes { get; }
    public byte[] CurrentBytes { get; private set; }
    public TreeNode Tree { get; private set; }
    public TreeNode? SelectedNode { get; set; }
    public int CurrentPage { get; set; } = 1;

    // Injected so RefreshAsync can invalidate stale render cache entries
    internal RenderingService? RenderCache { private get; set; }

    public async ValueTask DisposeAsync() => await Document.DisposeAsync().ConfigureAwait(false);

    /// <summary>
    ///     Raised after <see cref="RefreshAsync" /> completes so that UI components
    ///     can call <c>StateHasChanged()</c> to pick up the new tree and bytes.
    /// </summary>
    public event Action? Refreshed;

    public static async Task<PdfSessionState> CreateAsync(
        DocumentProcessor processor,
        byte[] bytes,
        string fileName,
        CancellationToken ct = default
    )
    {
        using var stream = new MemoryStream(bytes);
        var document = await processor.LoadAsync(stream, ct).ConfigureAwait(false);
        return new PdfSessionState(processor, document, bytes, fileName);
    }

    public static async Task<PdfSessionState> CreateEncryptedAsync(
        DocumentProcessor processor,
        byte[] bytes,
        string password,
        string fileName,
        CancellationToken ct = default
    )
    {
        using var stream = new MemoryStream(bytes);
        var document = await processor.LoadAsync(stream, password, ct).ConfigureAwait(false);
        return new PdfSessionState(processor, document, bytes, fileName);
    }

    /// <summary>
    ///     Serialises the current document, reloads it, and rebuilds the tree.
    ///     Raises <see cref="Refreshed" /> so the UI can re-render.
    /// </summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await Processor.SaveAsync(Document, ms, ct: ct).ConfigureAwait(false);
        var newBytes = ms.ToArray();

        var oldDocument = Document;

        // Invalidate render cache for the old document before reloading
        RenderCache?.InvalidateDocument(oldDocument);

        using var reloadStream = new MemoryStream(newBytes);
        Document = await Processor.LoadAsync(reloadStream, ct).ConfigureAwait(false);
        CurrentBytes = newBytes;
        Tree = PdfTreeBuilder.Build(Document, FileName);

        // Dispose the old document only after the new one is ready
        await oldDocument.DisposeAsync().ConfigureAwait(false);

        Refreshed?.Invoke();
    }
}
