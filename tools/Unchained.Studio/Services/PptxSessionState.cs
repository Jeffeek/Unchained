using Unchained.Pptx.Engine;
using Unchained.Studio.Models;
using Unchained.Studio.Studio.Pptx;

namespace Unchained.Studio.Services;

/// <summary>
/// Holds the currently-loaded PPTX presentation for one Blazor circuit, along with the
/// navigable tree and the raw bytes. Mirrors <see cref="PdfSessionState"/>.
/// </summary>
public sealed class PptxSessionState : IAsyncDisposable
{
    public PresentationProcessor Processor { get; }
    public PresentationDocument Document { get; private set; }
    public string FileName { get; }
    public long FileSizeBytes { get; }
    public byte[] CurrentBytes { get; private set; }
    public TreeNode Tree { get; private set; }
    public TreeNode? SelectedNode { get; set; }
    public int CurrentSlide { get; set; } = 1;

    /// <summary>
    /// Raised after <see cref="RefreshAsync"/> completes so the UI can re-render.
    /// </summary>
    public event Action? Refreshed;

    private PptxSessionState(
        PresentationProcessor processor,
        PresentationDocument document,
        byte[] bytes,
        string fileName)
    {
        Processor = processor;
        Document = document;
        CurrentBytes = bytes;
        FileName = fileName;
        FileSizeBytes = bytes.LongLength;
        Tree = PptxTreeBuilder.Build(document, fileName);
    }

    public static async Task<PptxSessionState> CreateAsync(
        PresentationProcessor processor,
        byte[] bytes,
        string fileName,
        CancellationToken ct = default)
    {
        var document = await processor.LoadAsync(bytes, cancellationToken: ct).ConfigureAwait(false);
        return new PptxSessionState(processor, document, bytes, fileName);
    }

    /// <summary>
    /// Serialises the current document, reloads it, and rebuilds the tree.
    /// Raises <see cref="Refreshed"/> so the UI can re-render.
    /// </summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await Processor.SaveAsync(Document, ms, cancellationToken: ct).ConfigureAwait(false);
        var newBytes = ms.ToArray();

        var oldDocument = Document;

        Document = await Processor.LoadAsync(newBytes, cancellationToken: ct).ConfigureAwait(false);
        CurrentBytes = newBytes;
        Tree = PptxTreeBuilder.Build(Document, FileName);

        await oldDocument.DisposeAsync().ConfigureAwait(false);

        Refreshed?.Invoke();
    }

    public async ValueTask DisposeAsync() => await Document.DisposeAsync().ConfigureAwait(false);
}
