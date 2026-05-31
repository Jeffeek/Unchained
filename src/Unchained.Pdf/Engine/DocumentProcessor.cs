using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
/// Default <see cref="IDocumentProcessor"/> implementation backed by Unchained's
/// own parser (<see cref="Unchained.Pdf.Parsing.PdfParser"/>) and writer
/// (<see cref="Unchained.Pdf.Writing.PdfWriter"/>).
/// <para>
/// Because the PDF parser is CPU-bound and synchronous, all operations are
/// dispatched to the thread-pool via <see cref="Task.Run(System.Action)"/>.
/// A <see cref="SemaphoreSlim"/> limits the number of concurrent parse operations
/// to <see cref="Environment.ProcessorCount"/> (or the value supplied at construction)
/// so that bursts of parallel requests do not over-subscribe the thread-pool.
/// </para>
/// </summary>
public sealed class DocumentProcessor : IDocumentProcessor
{
    private readonly SemaphoreSlim _gate;
    private int _disposed;

    /// <summary>
    /// Creates a new <see cref="DocumentProcessor"/>.
    /// </summary>
    /// <param name="maxConcurrency">
    /// Maximum number of PDF parse operations that may run concurrently.
    /// Defaults to <see cref="Environment.ProcessorCount"/> when <see langword="null"/>.
    /// </param>
    public DocumentProcessor(int? maxConcurrency = null)
    {
        var concurrency = maxConcurrency ?? Environment.ProcessorCount;
        _gate = new SemaphoreSlim(concurrency, concurrency);
    }

    /// <inheritdoc />
    public async Task<IPdfDocument> LoadAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var bytes = await File.ReadAllBytesAsync(filePath, ct).ConfigureAwait(false);
        return await ParseAsync(bytes, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IPdfDocument> LoadAsync(Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
        return await ParseAsync(ms.ToArray(), ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        IPdfDocument document,
        string filePath,
        SaveOptions? options = null,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var adapter = CastAdapter(document);
        var bytes = await SerializeAsync(adapter, options, ct).ConfigureAwait(false);
        await File.WriteAllBytesAsync(filePath, bytes, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        IPdfDocument document,
        Stream stream,
        SaveOptions? options = null,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        var adapter = CastAdapter(document);
        var bytes = await SerializeAsync(adapter, options, ct).ConfigureAwait(false);
        await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
    }

    // Acquires a gate slot and parses the byte array on the thread-pool.
    private async Task<IPdfDocument> ParseAsync(byte[] bytes, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var core = await Task.Run(
                () => PdfDocumentCore.Parse(bytes),
                ct).ConfigureAwait(false);
            return new PdfDocumentAdapter(core, bytes);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static Task<byte[]> SerializeAsync(PdfDocumentAdapter adapter, SaveOptions? options, CancellationToken ct) =>
        Task.Run(() => adapter.Serialize(options), ct);

    // DocumentProcessor creates PdfDocumentAdapter instances exclusively, so any
    // IPdfDocument argument that is not one indicates a programming error.
    private static PdfDocumentAdapter CastAdapter(IPdfDocument document) =>
        document as PdfDocumentAdapter
        ?? throw new ArgumentException(
            $"Document was not created by this processor. Expected {nameof(PdfDocumentAdapter)}, got {document.GetType().Name}.",
            nameof(document));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _gate.Dispose();
    }
}
