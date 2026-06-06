using Unchained.Pptx.Themes;
using Unchained.Pptx.Core;
using Unchained.Ooxml;
using Unchained.Pptx.Export;
using Unchained.Pptx.Media;
using Unchained.Pptx.Models;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Security;
using Unchained.Pptx.Slides;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Writing;

namespace Unchained.Pptx.Engine;

/// <summary>
/// The primary entry point for all Unchained PPTX operations.
/// Load, create, and save presentations; thread-safe for concurrent use.
/// </summary>
/// <remarks>
/// <para>
/// All I/O-bound methods are <see langword="async"/> and dispatch CPU-bound parse/serialize
/// work to the thread pool via <see cref="Task.Run"/>. A <see cref="SemaphoreSlim"/> limits
/// concurrency to <see cref="Environment.ProcessorCount"/> simultaneous parse operations,
/// preventing thread-pool saturation on high-throughput ASP.NET or gRPC servers.
/// </para>
/// <para>Dispose the processor when it is no longer needed to release the semaphore.</para>
/// </remarks>
public sealed class PresentationProcessor : IDisposable
{
    private readonly SemaphoreSlim _gate;
    private int _disposed;

    /// <summary>
    /// Initialises a new <see cref="PresentationProcessor"/>.
    /// </summary>
    /// <param name="maxConcurrency">
    /// Maximum number of presentations that may be parsed or serialized simultaneously.
    /// Defaults to <see cref="Environment.ProcessorCount"/>.
    /// </param>
    public PresentationProcessor(int? maxConcurrency = null)
    {
        var limit = maxConcurrency ?? Environment.ProcessorCount;
        if (limit < 1) throw new ArgumentOutOfRangeException(
            nameof(maxConcurrency), "Concurrency limit must be at least 1.");
        _gate = new SemaphoreSlim(limit, limit);
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a presentation from a file path.
    /// </summary>
    /// <param name="path">The path to the <c>.pptx</c> file.</param>
    /// <param name="options">Optional load settings (e.g. password for encrypted files).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <exception cref="PptxEncryptedException">
    /// Thrown when the file is password-protected and no password was supplied.
    /// </exception>
    /// <exception cref="PptxException">Thrown for any other parse error.</exception>
    public async Task<PresentationDocument> LoadAsync(
        string path,
        OpenOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return await ParseBytesAsync(bytes, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads a presentation from a stream. The stream is read to completion and may be closed
    /// after this method returns.
    /// </summary>
    /// <param name="stream">A readable stream containing the PPTX data.</param>
    /// <param name="options">Optional load settings.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task<PresentationDocument> LoadAsync(
        Stream stream,
        OpenOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return await ParseBytesAsync(ms.ToArray(), options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads a presentation from a raw byte array.
    /// </summary>
    /// <param name="data">The raw PPTX bytes.</param>
    /// <param name="options">Optional load settings.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public Task<PresentationDocument> LoadAsync(
        ReadOnlyMemory<byte> data,
        OpenOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ParseBytesAsync(data.ToArray(), options, cancellationToken);

    // ── Create ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new blank presentation in memory (no I/O).
    /// The presentation contains a single default slide master with one blank layout.
    /// </summary>
    /// <param name="size">
    /// The slide size. Defaults to <see cref="SlideSize.Widescreen"/> (16:9).
    /// </param>
    public PresentationDocument CreateBlank(SlideSize? size = null)
    {
        var slideSize = size ?? SlideSize.Widescreen;

        var master = new MasterSlide { Name = "Office Theme", Theme = new PptxTheme() };
        var layout = new SlideLayout { Name = "Blank", LayoutType = Models.Themes.LayoutType.Blank };
        layout.Master = master;
        master.Layouts.Add(layout);

        var masters = new MasterSlideCollection();
        masters.Add(master);

        var slides = new SlideCollection();
        var mediaStore = new MediaStore();
        var properties = new DocumentProperties
        {
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };
        var protection = new ProtectionInfo();

        return new PresentationDocument(slides, masters, mediaStore, properties, protection, slideSize);
    }

    // ── PDF Export (M9) ───────────────────────────────────────────────────────

    /// <summary>
    /// Exports <paramref name="document"/> to a PDF file.
    /// Each non-hidden slide becomes one PDF page at the correct dimensions.
    /// </summary>
    public async Task SaveAsPdfAsync(
        PresentationDocument document,
        string path,
        PdfSaveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var bytes = await ExportPdfAsync(document, options, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Exports <paramref name="document"/> to a PDF stream.
    /// </summary>
    public async Task SaveAsPdfAsync(
        PresentationDocument document,
        Stream stream,
        PdfSaveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        var bytes = await ExportPdfAsync(document, options, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ExportPdfAsync(
        PresentationDocument document,
        PdfSaveOptions? options,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var opts = options ?? PdfSaveOptions.Default;
            return await Task.Run(
                () => PptxToPdfWriter.Write(document, opts),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves the presentation to a file.
    /// </summary>
    /// <param name="document">The presentation to save.</param>
    /// <param name="path">The output file path.</param>
    /// <param name="options">Optional save settings.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task SaveAsync(
        PresentationDocument document,
        string path,
        SaveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var bytes = await SerializeAsync(document, options, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves the presentation to a stream.
    /// </summary>
    /// <param name="document">The presentation to save.</param>
    /// <param name="stream">A writable stream to receive the PPTX data.</param>
    /// <param name="options">Optional save settings.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task SaveAsync(
        PresentationDocument document,
        Stream stream,
        SaveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);

        var bytes = await SerializeAsync(document, options, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private async Task<PresentationDocument> ParseBytesAsync(
        byte[] bytes,
        OpenOptions? options,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var parsed = await Task.Run(
                () => PresentationParser.Parse(bytes, options),
                cancellationToken).ConfigureAwait(false);

            return new PresentationDocument(
                parsed.Slides,
                parsed.Masters,
                parsed.MediaStore,
                parsed.Properties,
                parsed.Protection,
                parsed.SlideSize,
                parsed.CommentAuthors,
                parsed.Sections);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<byte[]> SerializeAsync(
        PresentationDocument document,
        SaveOptions? options,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Keep statistics current before serializing
            document.SyncStatistics();

            return await Task.Run(() =>
                PresentationWriter.Write(
                    document.Slides,
                    document.Masters,
                    document.Media,
                    document.Properties,
                    document.SlideSize,
                    document.CommentAuthors,
                    document.Sections,
                    document.Protection,
                    options),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _gate.Dispose();
    }
}
