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

    /// <inheritdoc />
    public IReadOnlyList<TextSpan> GetTextSpans() =>
        TextExtractor.Extract(GetContentOperators(), ResolveFontNames());

    /// <inheritdoc />
    public string ExtractText() =>
        TextExtractor.SpansToText(GetTextSpans());

    /// <inheritdoc />
    public IReadOnlyList<Models.Annotation> GetAnnotations()
    {
        var annotsObj = page[PdfName.Annots];
        var annotsArr = annotsObj switch
        {
            PdfArray a => a,
            PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfArray,
            _ => null
        };
        if (annotsArr is null) return [];

        var result = new List<Models.Annotation>();
        foreach (var elem in annotsArr.Elements)
        {
            var dict = elem switch
            {
                PdfDictionary d => d,
                PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfDictionary,
                _ => null
            };
            if (dict is null) continue;

            var subtypeName = dict.GetName(PdfName.Subtype.Value) ?? "";
            var subtype = subtypeName switch
            {
                "Text"      => Models.AnnotationSubtype.Text,
                "Highlight" => Models.AnnotationSubtype.Highlight,
                "Link"      => Models.AnnotationSubtype.Link,
                "FreeText"  => Models.AnnotationSubtype.FreeText,
                "Square"    => Models.AnnotationSubtype.Square,
                "Circle"    => Models.AnnotationSubtype.Circle,
                _           => Models.AnnotationSubtype.Text
            };

            var rect = dict.Get<PdfArray>(PdfName.Rect);
            var x = rect is { Count: >= 4 } ? (float)ReadCoord(rect[0]) : 0f;
            var y = rect is { Count: >= 4 } ? (float)ReadCoord(rect[1]) : 0f;
            var x2 = rect is { Count: >= 4 } ? (float)ReadCoord(rect[2]) : 0f;
            var y2 = rect is { Count: >= 4 } ? (float)ReadCoord(rect[3]) : 0f;

            string? contents = null;
            if (dict[PdfName.Contents] is PdfString cs)
                contents = System.Text.Encoding.Latin1.GetString(cs.Bytes.Span);

            float[]? color = null;
            if (dict.Get<PdfArray>(PdfName.Get("C")) is { Count: 3 } cArr)
                color = [ReadFloat(cArr[0]), ReadFloat(cArr[1]), ReadFloat(cArr[2])];

            result.Add(new Models.Annotation(subtype, x, y, x2 - x, y2 - y, contents, color));
        }
        return result;
    }

    // Walks the page /Resources /Font dictionary and maps each resource name (e.g. "F1")
    // to the actual base font name (e.g. "Helvetica") for AFM width lookup.
    private Dictionary<string, string> ResolveFontNames()
    {
        var result = new Dictionary<string, string>();
        var resources = ResolveDict(page[PdfName.Resources]);
        var fontDict = ResolveDict(resources?[PdfName.Font]);
        if (fontDict is null) return result;

        foreach (var (key, value) in fontDict.Entries)
        {
            var fontEntry = ResolveDict(value);
            var baseFontName = fontEntry?.GetName(PdfName.BaseFont.Value);
            if (baseFontName is not null)
                result[key] = baseFontName;
        }

        return result;
    }

    private PdfDictionary? ResolveDict(PdfObject? obj) => obj switch
    {
        PdfDictionary d => d,
        PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfDictionary,
        _ => null
    };

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
    private static ReadOnlyMemory<byte> ConcatenateStreams(IEnumerable<PdfStream> streams)
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

    private static double ReadCoord(PdfObject obj) => obj switch
    {
        PdfInteger i => i.Value,
        PdfReal r => r.Value,
        _ => 0
    };

    private static float ReadFloat(PdfObject obj) => (float)ReadCoord(obj);
}
