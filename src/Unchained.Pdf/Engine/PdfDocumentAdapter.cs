using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
/// Adapts <see cref="PdfDocumentCore"/> to the public <see cref="IPdfDocument"/> interface.
/// Owns both the core document model and the raw source byte array.
/// </summary>
internal sealed class PdfDocumentAdapter : IPdfDocument
{
    private readonly PdfDocumentCore _core;
    private readonly byte[] _sourceBytes;
    private int _disposed;

    internal PdfDocumentAdapter(PdfDocumentCore core, byte[] sourceBytes)
    {
        _core = core;
        _sourceBytes = sourceBytes;
        Pages = new PdfPageCollectionAdapter(core);
    }

    /// <inheritdoc />
    public int PageCount => _core.PageCount;

    /// <inheritdoc />
    public IPageCollection Pages { get; }

    /// <inheritdoc />
    public DocumentMetadata Metadata
    {
        get
        {
            var info = _core.Info;
            return info is null
                ? DocumentMetadata.Empty
                : new DocumentMetadata(
                    Title: GetInfoString(info, "Title"),
                    Author: GetInfoString(info, "Author"),
                    Subject: GetInfoString(info, "Subject"),
                    Keywords: GetInfoString(info, "Keywords"),
                    Creator: GetInfoString(info, "Creator"),
                    Producer: GetInfoString(info, "Producer"),
                    CreationDate: null,
                    ModificationDate: null);
        }
    }

    /// <inheritdoc />
    public bool IsDisposed => _disposed == 1;

    /// <summary>
    /// Returns the PDF bytes for this document.
    /// For documents that have not been modified this is a direct pass-through of the
    /// original source bytes. Once mutation support is implemented, this method will
    /// re-serialize the modified object graph via <see cref="Unchained.Pdf.Writing.PdfWriter"/>.
    /// </summary>
    internal byte[] Serialize(SaveOptions? options) =>
        _sourceBytes;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _core.Dispose();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private static string? GetInfoString(PdfDictionary info, string key) =>
        info[key] is not PdfString str
            ? null
            : System.Text.Encoding.Latin1.GetString(str.Bytes.Span);
}
