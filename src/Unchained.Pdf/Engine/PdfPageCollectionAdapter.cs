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
    public IReadOnlyList<Annotation> GetAnnotations()
    {
        var annotationsObj = page[PdfName.Annots];
        var annotationsArr = annotationsObj switch
        {
            PdfArray a => a,
            PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfArray,
            _ => null
        };
        if (annotationsArr is null)
            return [];

        var result = new List<Annotation>();
        foreach (var elem in annotationsArr.Elements)
        {
            var dict = elem switch
            {
                PdfDictionary d => d,
                PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfDictionary,
                _ => null
            };
            if (dict is null) continue;

            var subtypeName = dict.GetName(PdfName.Subtype.Value) ?? string.Empty;
            var subtype = subtypeName switch
            {
                "Text" => AnnotationSubtype.Text,
                "Highlight" => AnnotationSubtype.Highlight,
                "Link" => AnnotationSubtype.Link,
                "FreeText" => AnnotationSubtype.FreeText,
                "Square" => AnnotationSubtype.Square,
                "Circle" => AnnotationSubtype.Circle,
                _ => AnnotationSubtype.Text
            };

            var rect = dict.Get<PdfArray>(PdfName.Rect);
            var x = rect is { Count: >= 4 } ? (float)ReadCoordinate(rect[0]) : 0f;
            var y = rect is { Count: >= 4 } ? (float)ReadCoordinate(rect[1]) : 0f;
            var x2 = rect is { Count: >= 4 } ? (float)ReadCoordinate(rect[2]) : 0f;
            var y2 = rect is { Count: >= 4 } ? (float)ReadCoordinate(rect[3]) : 0f;

            string? contents = null;
            if (dict[PdfName.Contents] is PdfString cs)
                contents = System.Text.Encoding.Latin1.GetString(cs.Bytes.Span);

            float[]? color = null;
            if (dict.Get<PdfArray>(PdfName.Get("C")) is { Count: 3 } cArr)
                color = [ReadFloat(cArr[0]), ReadFloat(cArr[1]), ReadFloat(cArr[2])];

            result.Add(new Annotation(
                subtype,
                x,
                y,
                x2 - x,
                y2 - y,
                contents,
                color)
            );
        }

        return result;
    }

    // Exposes the document core for XObject resolution and font embedding in M5.
    internal PdfDocumentCore Core => core;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetFontNameMap() => ResolveFontNames();

    /// <inheritdoc />
    public IReadOnlyDictionary<string, byte[]?> GetEmbeddedFontBytes()
    {
        var result = new Dictionary<string, byte[]?>();
        var resources = ResolveDict(page[PdfName.Resources]);
        var fontDict = ResolveDict(resources?[PdfName.Font]);
        if (fontDict is null)
            return result;

        foreach (var (key, value) in fontDict.Entries)
        {
            var fontEntry = ResolveDict(value);
            if (fontEntry is null)
            {
                result[key] = null;
                continue;
            }

            var descriptor = ResolveDict(fontEntry[PdfName.Get("FontDescriptor")]);
            if (descriptor is null)
            {
                result[key] = null;
                continue;
            }

            // Try /FontFile2 (TrueType), /FontFile3 (OpenType/CFF), /FontFile (Type1) in order.
            var streamRef = descriptor[PdfName.Get("FontFile2")]
                            ?? descriptor[PdfName.Get("FontFile3")]
                            ?? descriptor[PdfName.Get("FontFile")];

            if (streamRef is null)
            {
                result[key] = null;
                continue;
            }

            var fontStream = streamRef is PdfIndirectReference r
                ? core.ResolveIndirect(r.ObjectNumber).Value as PdfStream
                : streamRef as PdfStream;

            result[key] = fontStream is not null
                ? StreamFilters.Decode(fontStream).ToArray()
                : null;
        }

        return result;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ImageXObject> GetImageXObjects()
    {
        var result = new Dictionary<string, ImageXObject>();
        var resources = ResolveDict(page[PdfName.Resources]);
        var xobjDict = ResolveDict(resources?[PdfName.Get("XObject")]);
        if (xobjDict is null)
            return result;

        foreach (var (key, value) in xobjDict.Entries)
        {
            var stream = value is PdfIndirectReference r
                ? core.ResolveIndirect(r.ObjectNumber).Value as PdfStream
                : value as PdfStream;

            if (stream is null) continue;
            if (stream.Dictionary.GetName(PdfName.Subtype.Value) != "Image") continue;

            var w = (int)(stream.Dictionary.Get<PdfInteger>(PdfName.Get("Width"))?.Value ?? 0);
            var h = (int)(stream.Dictionary.Get<PdfInteger>(PdfName.Get("Height"))?.Value ?? 0);
            if (w <= 0 || h <= 0) continue;

            var cs = stream.Dictionary.GetName("ColorSpace");
            var bpc = (int)(stream.Dictionary.Get<PdfInteger>(PdfName.Get("BitsPerComponent"))?.Value ?? 8);

            byte[] rgb;
            try
            {
                var decoded = StreamFilters.Decode(stream);
                rgb = cs == "DeviceRGB" && bpc == 8 && decoded.Length == w * h * 3
                    ? decoded.ToArray()
                    : BuildGrayPlaceholder(w, h);
            }
            catch (NotImplementedException)
            {
                // Filter not yet supported (e.g. DCTDecode/JPEG, JPXDecode) — use placeholder.
                rgb = BuildGrayPlaceholder(w, h);
            }

            result[key] = new ImageXObject(w, h, rgb);
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
        if (fontDict is null)
            return result;

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

    private static byte[] BuildGrayPlaceholder(int w, int h)
    {
        var rgb = new byte[w * h * 3];
        Array.Fill(rgb, (byte)128);
        return rgb;
    }

    private static double ReadCoordinate(PdfObject obj) => obj switch
    {
        PdfInteger i => i.Value,
        PdfReal r => r.Value,
        _ => 0
    };

    private static float ReadFloat(PdfObject obj) => (float)ReadCoordinate(obj);
}
