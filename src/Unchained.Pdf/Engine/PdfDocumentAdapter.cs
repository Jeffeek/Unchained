using System.Buffers;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;
using Unchained.Pdf.Writing;

namespace Unchained.Pdf.Engine;

/// <summary>
/// Adapts <see cref="PdfDocumentCore"/> to the public <see cref="IPdfDocument"/> interface.
/// </summary>
internal sealed class PdfDocumentAdapter : IPdfDocument
{
    private int _disposed;

    internal PdfDocumentAdapter(PdfDocumentCore core)
    {
        Core = core;
        Pages = new PdfPageCollectionAdapter(core);
    }

    internal PdfDocumentCore Core { get; private set; }

    /// <summary>
    /// Disposes the current core and replaces it with <paramref name="newCore"/>.
    /// Called by <see cref="TableGenerator"/> and <see cref="DocumentMerger"/> after
    /// in-place document mutation (e.g. <c>AppendTableAsync</c>, <c>MergeAsync</c>).
    /// </summary>
    internal void ReplaceCore(PdfDocumentCore newCore)
    {
        Core.Dispose();
        Core = newCore;
        Pages = new PdfPageCollectionAdapter(Core);
    }

    /// <inheritdoc />
    public int PageCount => Core.PageCount;

    /// <inheritdoc />
    public IPageCollection Pages { get; private set; }

    /// <inheritdoc />
    public DocumentMetadata Metadata
    {
        get
        {
            var info = Core.Info;
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
    /// Performs a full-rewrite serialization: collects every in-use indirect object
    /// from the document, rebuilds the xref table with fresh byte offsets, and
    /// writes a clean PDF via <see cref="PdfWriter"/>.
    /// </summary>
    internal byte[] Serialize(SaveOptions? options)
    {
        _ = options; // Currently ignored — reserved for future use (e.g. incremental update support)

        var objects = Core.CollectObjects();
        var maxObjNum = objects.Count > 0 ? objects.Max(static o => o.ObjectNumber) : 0;

        // Build a clean trailer — preserve /Root and /Info, drop /Prev and other
        // incremental-update entries that belong to the old file structure.
        var trailerEntries = new Dictionary<string, PdfObject>
        {
            [PdfName.Size.Value] = new PdfInteger(maxObjNum + 1),
            [PdfName.Root.Value] = Core.Trailer[PdfName.Root] ?? throw new PdfException("Document trailer is missing required /Root entry.")
        };

        if (Core.Trailer[PdfName.Info] is { } infoRef)
            trailerEntries[PdfName.Info.Value] = infoRef;

        var trailer = new PdfDictionary(trailerEntries);

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new PdfWriter(buffer);
        writer.Write(objects, trailer);
        return buffer.WrittenMemory.ToArray();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            Core.Dispose();
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
