using System.Collections;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Content;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Engine;

/// <summary>
/// Adapts <see cref="PdfDocumentCore"/> page access to the <see cref="IPageCollection"/> interface.
/// Pages are resolved lazily from the document core on each access; no pages are
/// preloaded at construction time.
/// </summary>
internal sealed class PdfPageCollectionAdapter(PdfDocumentCore core) : IPageCollection
{
    /// <inheritdoc cref="IPageCollection.this[int]" />
    public IPdfPage this[int pageNumber] =>
        new PdfPageAdapter(core.GetPage(pageNumber), pageNumber, core);

    /// <inheritdoc />
    public int Count => core.PageCount;

    /// <inheritdoc />
    public IEnumerator<IPdfPage> GetEnumerator()
    {
        for (var i = 1; i <= Count; i++)
            yield return new PdfPageAdapter(core.GetPage(i), i, core);
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Adapts a raw PDF page dictionary to the <see cref="IPdfPage"/> interface.
/// Dimensions are read from the page's <c>/MediaBox</c> array (ISO 32000-1 §14.11.2).
/// Content operators are parsed on demand from the page's <c>/Contents</c> stream(s).
/// </summary>
internal sealed class PdfPageAdapter(PdfDictionary page, int pageNumber, PdfDocumentCore core) : IPdfPage
{
    /// <inheritdoc />
    public int PageNumber { get; } = pageNumber;

    /// <inheritdoc />
    public double Width => GetMediaBoxValue(2);

    /// <inheritdoc />
    public double Height => GetMediaBoxValue(3);

    /// <inheritdoc />
    public IReadOnlyList<ContentOperator> GetContentOperators()
    {
        var contents = page[PdfName.Contents];
        if (contents is null) return [];

        var decoded = DecodeContents(contents);
        return decoded.Length == 0 ? [] : ContentStreamParser.Parse(decoded);
    }

    // ── Content stream resolution ─────────────────────────────────────────────

    // /Contents can be a single indirect reference to a stream, or an array of them.
    // Multiple streams are treated as one continuous stream (§7.8.1).
    private ReadOnlyMemory<byte> DecodeContents(PdfObject contents)
    {
        var streams = CollectStreams(contents);

        return streams switch
        {
            { Count: 0 } => ReadOnlyMemory<byte>.Empty,
            { Count: 1 } => StreamFilters.Decode(streams[0]),
            _ => ConcatenateStreams(streams)
        };
    }

    private List<PdfStream> CollectStreams(PdfObject contents) =>
        contents switch
        {
            PdfIndirectReference r => TryResolveStream(r),
            PdfArray array => array.Elements
                .OfType<PdfIndirectReference>()
                .SelectMany(TryResolveStream)
                .ToList(),
            PdfStream s => [s],
            _ => []
        };

    private List<PdfStream> TryResolveStream(PdfIndirectReference r) =>
        core.ResolveIndirect(r.ObjectNumber).Value is PdfStream s ? [s] : [];

    // Decodes and concatenates multiple content streams with a newline separator
    // so that operator sequences spanning stream boundaries parse correctly (§7.8.1).
    private static ReadOnlyMemory<byte> ConcatenateStreams(IReadOnlyList<PdfStream> streams)
    {
        using var ms = new MemoryStream();
        foreach (var stream in streams)
        {
            ms.Write(StreamFilters.Decode(stream).Span);
            ms.WriteByte((byte)'\n');
        }
        return ms.ToArray();
    }

    // ── MediaBox helpers ──────────────────────────────────────────────────────

    // MediaBox is [llx lly urx ury]; width = urx (index 2), height = ury (index 3).
    private double GetMediaBoxValue(int index)
    {
        if (page[PdfName.MediaBox] is not PdfArray mediaBox || mediaBox.Count < 4)
            return 0;

        return mediaBox[index] switch
        {
            PdfInteger i => i.Value,
            PdfReal r => r.Value,
            _ => 0
        };
    }
}
