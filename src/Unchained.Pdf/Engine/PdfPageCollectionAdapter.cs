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
    // MediaBox is [llx lly urx ury]; width = |urx - llx|, height = |ury - lly|.
    // Rotation swaps the meaning: for Rotate 90/270 the logical width = ury-lly.
    public double Width
    {
        get
        {
            var rotate = Rotate;
            return rotate is 90 or 270
                ? Math.Abs(GetMediaBoxValue(3) - GetMediaBoxValue(1))
                : Math.Abs(GetMediaBoxValue(2) - GetMediaBoxValue(0));
        }
    }

    /// <inheritdoc />
    public double Height
    {
        get
        {
            var rotate = Rotate;
            return rotate is 90 or 270
                ? Math.Abs(GetMediaBoxValue(2) - GetMediaBoxValue(0))
                : Math.Abs(GetMediaBoxValue(3) - GetMediaBoxValue(1));
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ContentOperator> GetContentOperators()
    {
        var contents = page[PdfName.Contents];
        if (contents is null) return [];

        var decoded = DecodeContents(contents);
        if (decoded.Length == 0) return [];

        var operators = ContentStreamParser.Parse(decoded);
        var resources = ResolveDict(page[PdfName.Resources]);
        var budget = MaxExpandedOperatorsPerPage;
        return ExpandFormXObjects(operators, resources, depth: 0, ref budget);
    }

    private const int MaxFormXObjectDepth = 10;

    // 100 000 operators per page is a generous ceiling that covers any real PDF.
    // An expanded count above this indicates recursive or excessively large form XObjects
    // that would take too long to render; we stop expanding beyond the limit.
    private const int MaxExpandedOperatorsPerPage = 100_000;

    // Recursively expands Do operators that reference /Subtype /Form XObjects.
    // Each form XObject is inlined as q [cm] <form content> Q.
    // Image Do operators are left in place for the renderer to handle.
    // `budget` is a shared counter tracking total operators emitted for this page;
    // expansion stops when it reaches MaxExpandedOperatorsPerPage.
    private IReadOnlyList<ContentOperator> ExpandFormXObjects(
        IReadOnlyList<ContentOperator> operators,
        PdfDictionary? resources,
        int depth,
        ref int budget
    )
    {
        if (depth >= MaxFormXObjectDepth ||
            budget <= 0 || // ceiling reached — no further expansion
            !operators.Any(static op => op.Name == "Do"))
            return operators;

        var xObjDict = ResolveDict(resources?[PdfName.Get("XObject")]);
        var result = new List<ContentOperator>(operators.Count + 4);

        foreach (var op in operators)
        {
            if (budget <= 0)
            {
                result.Add(op); // ceiling hit — emit remaining ops unexpanded
                continue;
            }

            if (op.Name != "Do" || op.Operands.Count == 0 ||
                op.Operands[0] is not PdfName xName || xObjDict is null)
            {
                result.Add(op);
                budget--;
                continue;
            }

            var xObj = xObjDict[PdfName.Get(xName.Value)];
            var xStream = xObj switch
            {
                PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfStream,
                PdfStream s => s,
                _ => null
            };

            if (xStream?.Dictionary.GetName(PdfName.Subtype.Value) != "Form")
            {
                result.Add(op); // image XObject or unresolved — leave Do intact
                budget--;
                continue;
            }

            result.Add(new ContentOperator("q", []));
            budget--;

            var matrixArr = xStream.Dictionary.Get<PdfArray>(PdfName.Get("Matrix"));
            if (matrixArr is { Count: 6 })
            {
                result.Add(new ContentOperator("cm", matrixArr.Elements.ToArray()));
                budget--;
            }

            ReadOnlyMemory<byte> formData;
            try
            {
                formData = StreamFilters.Decode(xStream);
            }
            catch
            {
                result.Add(new ContentOperator("Q", []));
                budget--;
                continue;
            }

            if (formData.Length > 0)
            {
                var formResources = ResolveDict(xStream.Dictionary[PdfName.Resources]) ?? resources;
                var formOps = ContentStreamParser.Parse(formData);
                result.AddRange(ExpandFormXObjects(formOps, formResources, depth + 1, ref budget));
            }

            result.Add(new ContentOperator("Q", []));
            budget--;
        }

        return result;
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
    public int Rotate
    {
        get
        {
            var val = GetInheritedInteger(PdfName.Get("Rotate"), 0);
            return ((val % 360) + 360) % 360; // normalise to 0/90/180/270
        }
    }

    // Walk up the page tree to find an inherited integer value.
    private int GetInheritedInteger(PdfName key, int defaultValue)
    {
        PdfObject? current = page;
        while (current is not null)
        {
            PdfDictionary? dict = current switch
            {
                PdfDictionary d => d,
                PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfDictionary,
                _ => null
            };
            if (dict is null) break;

            var obj = dict[key];
            if (obj is PdfInteger n)  return (int)n.Value;
            if (obj is PdfReal r2)    return (int)r2.Value;

            // Climb to parent
            current = dict[PdfName.Get("Parent")];
        }
        return defaultValue;
    }

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

            var cs  = stream.Dictionary.GetName("ColorSpace");
            var bpc = (int)(stream.Dictionary.Get<PdfInteger>(PdfName.Get("BitsPerComponent"))?.Value ?? 8);

            byte[] rgb;
            try
            {
                var decoded = StreamFilters.Decode(stream);
                rgb = DecodeImageToRgb(decoded, w, h, cs, bpc);
            }
            catch (Exception ex) when (ex is NotImplementedException or NotSupportedException or InvalidOperationException)
            {
                // Unsupported filter or malformed data — use gray placeholder.
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

    // Returns the raw value at the given index from /MediaBox [llx lly urx ury].
    // Callers compute width = |urx - llx| and height = |ury - lly|.
    private double GetMediaBoxValue(int index)
    {
        if (page[PdfName.MediaBox] is not PdfArray mediaBox || mediaBox.Count < 4)
            return 0;

        return mediaBox[index] switch
        {
            PdfInteger i => i.Value,
            PdfReal r    => r.Value,
            _            => 0
        };
    }

    // Decode raw image bytes into a packed RGB (3 bytes/pixel) array.
    private static byte[] DecodeImageToRgb(ReadOnlyMemory<byte> decoded, int w, int h, string? cs, int bpc)
    {
        var pixelCount = w * h;

        // DeviceRGB — direct 3-channel, 8 bpc
        if (cs == "DeviceRGB" && bpc == 8 && decoded.Length == pixelCount * 3)
            return decoded.ToArray();

        // DeviceGray — single channel; replicate to R, G, B
        if ((cs == "DeviceGray" || cs == null) && bpc == 8 && decoded.Length == pixelCount)
        {
            var span = decoded.Span;
            var rgb  = new byte[pixelCount * 3];
            for (int i = 0, j = 0; i < pixelCount; i++, j += 3)
                rgb[j] = rgb[j + 1] = rgb[j + 2] = span[i];
            return rgb;
        }

        // DeviceCMYK — 4 channels, 8 bpc → convert to RGB
        if (cs == "DeviceCMYK" && bpc == 8 && decoded.Length == pixelCount * 4)
        {
            var span = decoded.Span;
            var rgb  = new byte[pixelCount * 3];
            for (int i = 0, j = 0; i < pixelCount; i++, j += 3)
            {
                var c = span[(i * 4)    ] / 255.0;
                var m = span[(i * 4) + 1] / 255.0;
                var y = span[(i * 4) + 2] / 255.0;
                var k = span[(i * 4) + 3] / 255.0;
                rgb[j]     = (byte)Math.Clamp(((1 - c) * (1 - k)) * 255, 0, 255);
                rgb[j + 1] = (byte)Math.Clamp(((1 - m) * (1 - k)) * 255, 0, 255);
                rgb[j + 2] = (byte)Math.Clamp(((1 - y) * (1 - k)) * 255, 0, 255);
            }
            return rgb;
        }

        // DeviceGray 1 bpc (bi-level / CCITTFax) — unpack bit rows to RGB.
        // PDF convention: sample 0 = minimum = white (paper), 1 = maximum = black (ink).
        if ((cs == "DeviceGray" || cs == null) && bpc == 1)
        {
            var span = decoded.Span;
            var rgb  = new byte[pixelCount * 3];
            var rowBytes = (w + 7) / 8;
            for (var row = 0; row < h; row++)
            for (var col = 0; col < w; col++)
            {
                var byteIdx = (row * rowBytes) + (col >> 3);
                if (byteIdx >= span.Length) break;
                var bit = (span[byteIdx] >> (7 - (col & 7))) & 1;
                var val = (byte)(bit == 0 ? 255 : 0); // 0=white paper, 1=black ink
                var j   = ((row * w) + col) * 3;
                rgb[j] = rgb[j + 1] = rgb[j + 2] = val;
            }
            return rgb;
        }

        // Unsupported: fall back to grey
        return BuildGrayPlaceholder(w, h);
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
