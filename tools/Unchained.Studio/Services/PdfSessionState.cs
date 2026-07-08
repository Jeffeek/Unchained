using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Engine;
using Unchained.Studio.Models;
using Unchained.Studio.Studio.Pdf;

namespace Unchained.Studio.Services;

public sealed class PdfSessionState : DocumentSessionBase<IPdfDocument, DocumentProcessor>
{
    private PdfSessionState(
        DocumentProcessor processor,
        IPdfDocument document,
        byte[] bytes,
        string fileName
    ) : base(fileName, bytes, document, processor) { }

    public int CurrentPage { get; set; } = 1;

    /// <summary>The playboard state for the current document.</summary>
    public PdfPlayboardState PlayboardState { get; } = new();

    /// <summary>Inject the render cache after construction so RefreshAsync can invalidate stale entries.</summary>
    internal RenderingService? RenderCache { get; set; }

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

    protected override async Task<byte[]> SerializeAsync(CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await Processor.SaveAsync(Document, ms, ct: ct).ConfigureAwait(false);
        return ms.ToArray();
    }

    protected override TreeNode BuildTree(IPdfDocument document, string fileName) =>
        PdfTreeBuilder.Build(document, fileName);

    protected override async Task<IPdfDocument> ReloadAsync(byte[] bytes, CancellationToken ct = default)
    {
        using var stream = new MemoryStream(bytes);
        return await Processor.LoadAsync(stream, ct).ConfigureAwait(false);
    }

    protected override ValueTask Dispose() => Document.DisposeAsync();
}
