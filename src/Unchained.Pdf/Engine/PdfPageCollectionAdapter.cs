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
/// Dimensions are read from <c>/CropBox</c> when present, falling back to
/// <c>/MediaBox</c> (ISO 32000-1 §14.11.2). Content operators are parsed on
/// demand from the page's <c>/Contents</c> stream(s).
/// </summary>
internal sealed class PdfPageAdapter(PdfDictionary page, int pageNumber, PdfDocumentCore core) : IPdfPage
{
    /// <inheritdoc />
    public int PageNumber { get; } = pageNumber;

    /// <inheritdoc />
    public double CropOriginX => GetArrayBoxValue("CropBox", 0) ?? GetArrayBoxValue("MediaBox", 0) ?? 0;

    /// <inheritdoc />
    public double CropOriginY => GetArrayBoxValue("CropBox", 1) ?? GetArrayBoxValue("MediaBox", 1) ?? 0;

    /// <inheritdoc />
    // CropBox (if present) defines the visible area; fall back to MediaBox.
    // [llx lly urx ury]; width = |urx - llx|, height = |ury - lly|.
    // Rotation swaps the meaning: for Rotate 90/270 the logical width = ury-lly.
    public double Width
    {
        get
        {
            var rotate = Rotate;
            return rotate is 90 or 270
                ? Math.Abs(GetBoxValue(3) - GetBoxValue(1))
                : Math.Abs(GetBoxValue(2) - GetBoxValue(0));
        }
    }

    /// <inheritdoc />
    public double Height
    {
        get
        {
            var rotate = Rotate;
            return rotate is 90 or 270
                ? Math.Abs(GetBoxValue(2) - GetBoxValue(0))
                : Math.Abs(GetBoxValue(3) - GetBoxValue(1));
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

            // For composite fonts (/Subtype /Type0) the embedded font program lives in
            // the FontDescriptor of the descendant CIDFont, not the top-level font dict
            // (§9.7.4). Follow /DescendantFonts to reach it.
            var descriptorHolder = fontEntry;
            if (fontEntry.GetName(PdfName.Subtype.Value) == "Type0")
            {
                var descendants = fontEntry[PdfName.Get("DescendantFonts")];
                if (descendants is PdfIndirectReference dr)
                    descendants = core.ResolveIndirect(dr.ObjectNumber).Value;
                var cidFont = descendants switch
                {
                    PdfArray { Count: > 0 } a => ResolveDict(a[0]),
                    PdfDictionary d => d,
                    _ => null
                };
                if (cidFont is not null)
                    descriptorHolder = cidFont;
            }

            var descriptor = ResolveDict(descriptorHolder[PdfName.Get("FontDescriptor")]);
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
    public IReadOnlyDictionary<string, Models.CompositeFontInfo> GetCompositeFonts()
    {
        var result = new Dictionary<string, Models.CompositeFontInfo>();
        var resources = ResolveDict(page[PdfName.Resources]);
        var fontDict = ResolveDict(resources?[PdfName.Font]);
        if (fontDict is null)
            return result;

        foreach (var (key, value) in fontDict.Entries)
        {
            var fontEntry = ResolveDict(value);
            if (fontEntry is null || fontEntry.GetName(PdfName.Subtype.Value) != "Type0")
                continue;

            // /Encoding may be a name (e.g. /Identity-H) or a CMap stream. We only
            // fast-path the Identity CMaps where each 2-byte code equals the CID.
            var encName = (fontEntry["Encoding"] as PdfName)?.Value;
            var identityEncoding = encName is "Identity-H" or "Identity-V";

            var descendants = fontEntry[PdfName.Get("DescendantFonts")];
            if (descendants is PdfIndirectReference dr)
                descendants = core.ResolveIndirect(dr.ObjectNumber).Value;
            var cidFont = descendants switch
            {
                PdfArray { Count: > 0 } a => ResolveDict(a[0]),
                PdfDictionary d => d,
                _ => null
            };
            if (cidFont is null)
                continue;

            // /CIDToGIDMap: /Identity (or absent) => CID == GID; otherwise a stream of
            // 2-byte big-endian GIDs indexed by CID.
            var c2gObj = cidFont["CIDToGIDMap"];
            if (c2gObj is PdfIndirectReference cr)
                c2gObj = core.ResolveIndirect(cr.ObjectNumber).Value;
            var identityCidToGid = c2gObj is null || (c2gObj as PdfName)?.Value == "Identity";
            IReadOnlyDictionary<int, int>? cidToGid = null;
            if (!identityCidToGid && c2gObj is PdfStream c2gStream)
            {
                var bytes = StreamFilters.Decode(c2gStream).Span;
                var map = new Dictionary<int, int>();
                for (var cid = 0; (cid * 2) + 1 < bytes.Length; cid++)
                {
                    var gid = (bytes[cid * 2] << 8) | bytes[(cid * 2) + 1];
                    if (gid != 0) map[cid] = gid;
                }
                cidToGid = map;
            }

            var dwInt = cidFont.Get<PdfInteger>(PdfName.Get("DW"))?.Value;
            var dwReal = cidFont.Get<PdfReal>(PdfName.Get("DW"))?.Value;
            var dw = dwInt ?? (dwReal is { } dr2 ? dr2 : 1000.0);
            var widths = ParseCidWidths(cidFont["W"]);

            result[key] = new Models.CompositeFontInfo(
                identityEncoding, identityCidToGid, cidToGid, dw, widths);
        }

        return result;
    }

    // Parses a CIDFont /W array (§9.7.4.3) into a CID->width map (glyph-space units).
    // Two forms: "c [w1 w2 ...]" (CIDs c, c+1, ...) and "cFirst cLast w" (range, same width).
    private IReadOnlyDictionary<int, double> ParseCidWidths(PdfObject? wObj)
    {
        var result = new Dictionary<int, double>();
        if (wObj is PdfIndirectReference r)
            wObj = core.ResolveIndirect(r.ObjectNumber).Value;
        if (wObj is not PdfArray arr)
            return result;

        var i = 0;
        while (i < arr.Count)
        {
            if (arr[i] is not (PdfInteger or PdfReal)) { i++; continue; }
            var first = (int)ReadCoordinate(arr[i]);
            if (i + 1 >= arr.Count) break;

            if (arr[i + 1] is PdfArray widthList)
            {
                for (var k = 0; k < widthList.Count; k++)
                    result[first + k] = ReadCoordinate(widthList[k]);
                i += 2;
            }
            else if (i + 2 < arr.Count
                     && arr[i + 1] is (PdfInteger or PdfReal)
                     && arr[i + 2] is (PdfInteger or PdfReal))
            {
                var last = (int)ReadCoordinate(arr[i + 1]);
                var w = ReadCoordinate(arr[i + 2]);
                for (var cid = first; cid <= last && cid - first < 65536; cid++)
                    result[cid] = w;
                i += 3;
            }
            else
            {
                i++;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>> GetToUnicodeMaps()
    {
        var result = new Dictionary<string, IReadOnlyDictionary<uint, string>>();
        var resources = ResolveDict(page[PdfName.Resources]);
        var fontDict = ResolveDict(resources?[PdfName.Font]);
        if (fontDict is null) return result;

        foreach (var (key, value) in fontDict.Entries)
        {
            var fontEntry = ResolveDict(value);
            if (fontEntry is null) continue;

            var tuRef = fontEntry[PdfName.Get("ToUnicode")];
            var tuStream = tuRef is PdfIndirectReference r
                ? core.ResolveIndirect(r.ObjectNumber).Value as PdfStream
                : tuRef as PdfStream;
            if (tuStream is null) continue;

            try
            {
                var cmap = ParseToUnicodeCmap(StreamFilters.Decode(tuStream).Span);
                if (cmap.Count > 0) result[key] = cmap;
            }
            catch { /* malformed CMap — skip */ }
        }

        return result;
    }

    // Parses a PDF ToUnicode CMap stream and returns a char-code → Unicode string mapping.
    // Handles beginbfchar (individual mappings) and beginbfrange (range mappings).
    private static IReadOnlyDictionary<uint, string> ParseToUnicodeCmap(ReadOnlySpan<byte> cmap)
    {
        var result = new Dictionary<uint, string>();
        var text = System.Text.Encoding.Latin1.GetString(cmap);
        var lines = text.Split('\n');
        var mode  = 0; // 0=none, 1=bfchar, 2=bfrange

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            // The count is usually prefixed: "27 beginbfchar", so check EndsWith.
            if (line.EndsWith("beginbfchar"))  { mode = 1; continue; }
            if (line == "endbfchar")           { mode = 0; continue; }
            if (line.EndsWith("beginbfrange")) { mode = 2; continue; }
            if (line == "endbfrange")          { mode = 0; continue; }
            if (mode == 0) continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (mode == 1 && parts.Length >= 2)
            {
                // <charCode> <unicodeValue>
                var charCode = ParseHexToken(parts[0]);
                var unicode  = ParseHexToken(parts[1]);
                if (charCode.Length == 0 || unicode.Length == 0) continue;
                uint key = 0;
                foreach (var b in charCode) key = (key << 8) | b;
                var uniStr = DecodeUtf16Be(unicode);
                if (uniStr.Length > 0) result[key] = uniStr;
            }
            else if (mode == 2 && parts.Length >= 3)
            {
                // <srcLo> <srcHi> <dstStart>   — maps a contiguous range
                var lo  = ParseHexToken(parts[0]);
                var hi  = ParseHexToken(parts[1]);
                var dst = ParseHexToken(parts[2]);
                if (lo.Length == 0 || hi.Length == 0 || dst.Length == 0) continue;

                uint loKey = 0, hiKey = 0;
                foreach (var b in lo)  loKey  = (loKey  << 8) | b;
                foreach (var b in hi)  hiKey  = (hiKey  << 8) | b;

                // dst is the starting Unicode code point (UTF-16BE big-endian)
                uint dstCp = 0;
                foreach (var b in dst) dstCp = (dstCp << 8) | b;

                for (var key = loKey; key <= hiKey; key++)
                {
                    var cp = dstCp + (key - loKey);
                    result[key] = char.ConvertFromUtf32((int)cp);
                }
            }
        }

        return result;
    }

    // Parses a <hexhex> token and returns the decoded bytes. Returns empty on failure.
    private static byte[] ParseHexToken(string token)
    {
        token = token.Trim();
        if (!token.StartsWith('<') || !token.EndsWith('>')) return [];
        var hex = token[1..^1];
        if (hex.Length % 2 != 0) hex = hex + "0";
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            try { bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16); }
            catch { return []; }
        }
        return bytes;
    }

    private static string DecodeUtf16Be(byte[] bytes)
    {
        if (bytes.Length % 2 != 0 || bytes.Length == 0) return string.Empty;
        var chars = new char[bytes.Length / 2];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = (char)((bytes[i * 2] << 8) | bytes[i * 2 + 1]);
        return new string(chars);
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

            var cs     = ReadColorSpace(stream.Dictionary);
            var bpc    = (int)(stream.Dictionary.Get<PdfInteger>(PdfName.Get("BitsPerComponent"))?.Value ?? 8);
            var decode = ReadDecodeArray(stream.Dictionary);
            var indexed = ReadIndexedPalette(stream.Dictionary);

            byte[] rgb;
            try
            {
                var decoded = StreamFilters.Decode(stream);
                rgb = indexed is not null
                    ? DecodeIndexedToRgb(decoded, w, h, bpc, indexed)
                    : DecodeImageToRgb(decoded, w, h, cs, bpc, decode);
            }
            catch (Exception ex) when (ex is NotImplementedException or NotSupportedException or InvalidOperationException)
            {
                // Unsupported filter or malformed data — use gray placeholder.
                rgb = BuildGrayPlaceholder(w, h);
            }

            var alpha = ReadSoftMask(stream.Dictionary, w, h);
            result[key] = new ImageXObject(w, h, rgb, alpha);
        }

        return result;
    }

    // Decodes an image's /SMask soft mask into a per-pixel alpha channel (W×H bytes,
    // 0 = transparent, 255 = opaque), resampled to the base image's dimensions. The SMask
    // is a DeviceGray image whose samples are the alpha values (§11.6.5.2). Returns null
    // when there is no soft mask or it cannot be decoded.
    private byte[]? ReadSoftMask(PdfDictionary imageDict, int baseW, int baseH)
    {
        var smRef = imageDict[PdfName.Get("SMask")];
        if (smRef is PdfIndirectReference r)
            smRef = core.ResolveIndirect(r.ObjectNumber).Value;
        if (smRef is not PdfStream smStream)
            return null;

        try
        {
            var smw = (int)(smStream.Dictionary.Get<PdfInteger>(PdfName.Get("Width"))?.Value ?? 0);
            var smh = (int)(smStream.Dictionary.Get<PdfInteger>(PdfName.Get("Height"))?.Value ?? 0);
            if (smw <= 0 || smh <= 0)
                return null;

            var smBpc = (int)(smStream.Dictionary.Get<PdfInteger>(PdfName.Get("BitsPerComponent"))?.Value ?? 8);
            var smDecode = ReadDecodeArray(smStream.Dictionary);
            // Decode as DeviceGray then take one channel per pixel as the alpha value.
            var smRgb = DecodeImageToRgb(StreamFilters.Decode(smStream), smw, smh, "DeviceGray", smBpc, smDecode);

            var alpha = new byte[baseW * baseH];
            for (var y = 0; y < baseH; y++)
            for (var x = 0; x < baseW; x++)
            {
                // Nearest-neighbour resample the mask to the base image grid.
                var sx = smw == baseW ? x : x * smw / baseW;
                var sy = smh == baseH ? y : y * smh / baseH;
                alpha[(y * baseW) + x] = smRgb[(((sy * smw) + sx) * 3)];
            }
            return alpha;
        }
        catch (Exception ex) when (ex is NotImplementedException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
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

    // ── Box helpers ───────────────────────────────────────────────────────────

    // Returns the raw value at the given index from /CropBox if present,
    // otherwise from /MediaBox. Both follow [llx lly urx ury] order.
    private double GetBoxValue(int index) =>
        GetArrayBoxValue("CropBox", index) ??
        GetArrayBoxValue("MediaBox", index) ??
        0;

    private double? GetArrayBoxValue(string name, int index)
    {
        // /MediaBox and /CropBox are inheritable page attributes (§7.7.3.4): a page leaf
        // may omit them and inherit from an ancestor /Pages node. Walk up /Parent until
        // the entry is found. Also resolve indirect references on both the array and its
        // elements.
        var key = PdfName.Get(name);
        PdfObject? current = page;
        var depth = 0;
        while (current is not null && depth++ < 64)
        {
            var dict = current switch
            {
                PdfDictionary d => d,
                PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfDictionary,
                _ => null
            };
            if (dict is null) break;

            var boxObj = dict[key];
            if (boxObj is PdfIndirectReference br)
                boxObj = core.ResolveIndirect(br.ObjectNumber).Value;

            if (boxObj is PdfArray box && box.Count >= 4)
            {
                var elem = box[index];
                if (elem is PdfIndirectReference er)
                    elem = core.ResolveIndirect(er.ObjectNumber).Value;
                return elem switch
                {
                    PdfInteger i => i.Value,
                    PdfReal r2   => r2.Value,
                    _            => null
                };
            }

            current = dict[PdfName.Get("Parent")];
        }

        return null;
    }

    // Kept for compatibility — still used by the inline-image coordinate path.
    private double GetMediaBoxValue(int index) => GetArrayBoxValue("MediaBox", index) ?? 0;

    // Resolves the /ColorSpace entry to a canonical name string.
    // Handles both direct names (/DeviceRGB) and arrays ([/ICCBased <stream>],
    // [/Indexed /DeviceRGB …]) by reading the base-space name or channel count.
    private string? ReadColorSpace(PdfDictionary dict)
    {
        var csObj = dict["ColorSpace"];
        if (csObj is PdfName name)
            return name.Value;

        if (csObj is PdfIndirectReference r)
            csObj = core.ResolveIndirect(r.ObjectNumber).Value;

        if (csObj is PdfArray arr && arr.Count >= 1)
        {
            var kind = (arr[0] as PdfName)?.Value;
            if (kind == "ICCBased" && arr.Count >= 2)
            {
                // The ICC stream's /N entry gives the number of color channels.
                var iccStream = arr[1] is PdfIndirectReference iccRef
                    ? core.ResolveIndirect(iccRef.ObjectNumber).Value as PdfStream
                    : arr[1] as PdfStream;
                var n = (int)(iccStream?.Dictionary.Get<PdfInteger>(PdfName.Get("N"))?.Value ?? 0);
                return n switch { 1 => "DeviceGray", 3 => "DeviceRGB", 4 => "DeviceCMYK", _ => null };
            }
            if (kind == "Indexed" && arr.Count >= 2)
                return (arr[1] as PdfName)?.Value; // return base space
        }

        return null;
    }

    // Reads the /Decode array as a float[]. Returns null when absent (= identity).
    private static float[]? ReadDecodeArray(PdfDictionary dict)
    {
        if (dict["Decode"] is not PdfArray da || da.Count == 0)
            return null;
        var values = new float[da.Count];
        for (var i = 0; i < da.Count; i++)
            values[i] = ReadFloat(da[i]);
        return values;
    }

    // An [/Indexed base hival lookup] colour space (§8.6.6.3). BaseChannels is the
    // number of colour components in the base space (1=Gray, 3=RGB, 4=CMYK); Lookup
    // holds (hival+1)*BaseChannels palette bytes, one base-space colour per index.
    private sealed record IndexedPalette(string Base, int BaseChannels, int HiVal, byte[] Lookup);

    // Detects an [/Indexed base hival lookup] /ColorSpace and parses its palette.
    // Returns null when the colour space is not Indexed.
    private IndexedPalette? ReadIndexedPalette(PdfDictionary dict)
    {
        var csObj = dict["ColorSpace"];
        if (csObj is PdfIndirectReference r)
            csObj = core.ResolveIndirect(r.ObjectNumber).Value;

        if (csObj is not PdfArray arr || arr.Count < 4)
            return null;
        if ((arr[0] as PdfName)?.Value != "Indexed")
            return null;

        // Base colour space: a name, or a nested array (e.g. [/ICCBased ...]).
        var baseName = ResolveBaseSpaceName(arr[1]);
        if (baseName is null)
            return null;
        var baseChannels = baseName switch
        {
            "DeviceGray" => 1, "DeviceRGB" => 3, "DeviceCMYK" => 4, _ => 0
        };
        if (baseChannels == 0)
            return null;

        var hival = (int)((arr[2] as PdfInteger)?.Value ?? 0);
        if (hival < 0)
            return null;

        // Lookup table: a byte string or a stream of (hival+1)*baseChannels bytes.
        var lookupObj = arr[3];
        if (lookupObj is PdfIndirectReference lr)
            lookupObj = core.ResolveIndirect(lr.ObjectNumber).Value;

        byte[] lookup = lookupObj switch
        {
            PdfString s => s.GetBinaryBytes().ToArray(),
            PdfStream st => StreamFilters.Decode(st).ToArray(),
            _ => []
        };
        return lookup.Length == 0 ? null : new IndexedPalette(baseName, baseChannels, hival, lookup);
    }

    // Resolves the base colour space of an Indexed space to a Device* name.
    private string? ResolveBaseSpaceName(PdfObject? obj)
    {
        if (obj is PdfIndirectReference r)
            obj = core.ResolveIndirect(r.ObjectNumber).Value;

        if (obj is PdfName n)
            return n.Value;

        if (obj is PdfArray arr && arr.Count >= 1)
        {
            var kind = (arr[0] as PdfName)?.Value;
            if (kind == "ICCBased" && arr.Count >= 2)
            {
                var iccStream = arr[1] is PdfIndirectReference iccRef
                    ? core.ResolveIndirect(iccRef.ObjectNumber).Value as PdfStream
                    : arr[1] as PdfStream;
                var nn = (int)(iccStream?.Dictionary.Get<PdfInteger>(PdfName.Get("N"))?.Value ?? 0);
                return nn switch { 1 => "DeviceGray", 3 => "DeviceRGB", 4 => "DeviceCMYK", _ => null };
            }
            if (kind is "CalRGB" or "Lab") return "DeviceRGB";
            if (kind == "CalGray") return "DeviceGray";
        }

        return null;
    }

    // Expands an Indexed (paletted) image to packed RGB. Each sample is a palette
    // index (bpc bits) that is looked up in the base-space palette and converted to RGB.
    private static byte[] DecodeIndexedToRgb(ReadOnlyMemory<byte> decoded, int w, int h, int bpc, IndexedPalette pal)
    {
        var pixelCount = w * h;
        var rgb = new byte[pixelCount * 3];
        var data = decoded.Span;
        var lut = pal.Lookup;
        var bc = pal.BaseChannels;
        var rowBytes = (w * bpc + 7) / 8;

        for (var row = 0; row < h; row++)
        for (var col = 0; col < w; col++)
        {
            var index = ReadSample(data, row, col, bpc, rowBytes);
            if (index > pal.HiVal) index = pal.HiVal;

            var off = index * bc;
            byte rr, gg, bb;
            if (off + bc > lut.Length)
            {
                rr = gg = bb = 0;
            }
            else if (bc == 1)
            {
                rr = gg = bb = lut[off];
            }
            else if (bc == 3)
            {
                rr = lut[off]; gg = lut[off + 1]; bb = lut[off + 2];
            }
            else // CMYK base
            {
                var c = lut[off] / 255.0; var m = lut[off + 1] / 255.0;
                var y = lut[off + 2] / 255.0; var k = lut[off + 3] / 255.0;
                rr = (byte)Math.Clamp((1 - c) * (1 - k) * 255, 0, 255);
                gg = (byte)Math.Clamp((1 - m) * (1 - k) * 255, 0, 255);
                bb = (byte)Math.Clamp((1 - y) * (1 - k) * 255, 0, 255);
            }

            var j = ((row * w) + col) * 3;
            rgb[j] = rr; rgb[j + 1] = gg; rgb[j + 2] = bb;
        }

        return rgb;
    }

    // Reads a single bpc-bit sample (1/2/4/8) at the given row/column from packed image data.
    private static int ReadSample(ReadOnlySpan<byte> data, int row, int col, int bpc, int rowBytes)
    {
        switch (bpc)
        {
            case 8:
            {
                var idx = (row * rowBytes) + col;
                return idx < data.Length ? data[idx] : 0;
            }
            case 1 or 2 or 4:
            {
                var bitPos = col * bpc;
                var byteIdx = (row * rowBytes) + (bitPos >> 3);
                if (byteIdx >= data.Length) return 0;
                var shift = 8 - bpc - (bitPos & 7);
                var mask = (1 << bpc) - 1;
                return (data[byteIdx] >> shift) & mask;
            }
            default:
                return 0;
        }
    }

    // Applies a /Decode array to a single 8-bit sample for one channel.
    // decode[2*ch] = Dmin, decode[2*ch+1] = Dmax per PDF spec §8.9.5.3.
    private static byte ApplyDecode(byte sample, float[]? decode, int channel)
    {
        if (decode is null) return sample;
        var idx = channel * 2;
        if (idx + 1 >= decode.Length) return sample;
        var dmin = decode[idx];
        var dmax = decode[idx + 1];
        if (Math.Abs(dmax - dmin - 1f) < 1e-4f && Math.Abs(dmin) < 1e-4f) return sample; // identity
        var component = dmin + (sample / 255f) * (dmax - dmin);
        return (byte)Math.Clamp(component * 255f, 0, 255);
    }

    // Decode raw image bytes into a packed RGB (3 bytes/pixel) array.
    private static byte[] DecodeImageToRgb(ReadOnlyMemory<byte> decoded, int w, int h, string? cs, int bpc, float[]? decode)
    {
        var pixelCount = w * h;

        // When cs is null, or the declared colorspace doesn't match the actual decoded
        // channel count (e.g. a CMYK ICC profile but the JPEG decoder collapsed to 1 channel),
        // fall back to the data-length heuristic so we get the best possible rendering
        // rather than a grey placeholder.
        var expectedChannels = cs switch
        {
            "DeviceCMYK" => 4, "DeviceRGB" => 3, "DeviceGray" => 1, _ => 0
        };
        if (bpc == 8 && expectedChannels > 0 && decoded.Length != pixelCount * expectedChannels)
            cs = null; // declared cs doesn't match data; re-infer below

        cs ??= decoded.Length switch
        {
            var n when n == pixelCount * 4 => "DeviceCMYK",
            var n when n == pixelCount * 3 => "DeviceRGB",
            _                              => "DeviceGray"
        };

        // DeviceRGB — direct 3-channel, 8 bpc
        if (cs == "DeviceRGB" && bpc == 8 && decoded.Length == pixelCount * 3)
        {
            if (decode is null) return decoded.ToArray();
            var span = decoded.Span;
            var rgb  = new byte[pixelCount * 3];
            for (int i = 0, j = 0; i < pixelCount; i++, j += 3)
            {
                rgb[j]     = ApplyDecode(span[j],     decode, 0);
                rgb[j + 1] = ApplyDecode(span[j + 1], decode, 1);
                rgb[j + 2] = ApplyDecode(span[j + 2], decode, 2);
            }
            return rgb;
        }

        // DeviceGray — single channel; replicate to R, G, B
        if (cs == "DeviceGray" && bpc == 8 && decoded.Length == pixelCount)
        {
            var span = decoded.Span;
            var rgb  = new byte[pixelCount * 3];
            for (int i = 0, j = 0; i < pixelCount; i++, j += 3)
            {
                var v = ApplyDecode(span[i], decode, 0);
                rgb[j] = rgb[j + 1] = rgb[j + 2] = v;
            }
            return rgb;
        }

        // DeviceCMYK — 4 channels, 8 bpc → convert to RGB
        if (cs == "DeviceCMYK" && bpc == 8 && decoded.Length == pixelCount * 4)
        {
            var span = decoded.Span;
            var rgb  = new byte[pixelCount * 3];
            for (int i = 0, j = 0; i < pixelCount; i++, j += 3)
            {
                var c = ApplyDecode(span[i * 4    ], decode, 0) / 255.0;
                var m = ApplyDecode(span[i * 4 + 1], decode, 1) / 255.0;
                var y = ApplyDecode(span[i * 4 + 2], decode, 2) / 255.0;
                var k = ApplyDecode(span[i * 4 + 3], decode, 3) / 255.0;
                rgb[j]     = (byte)Math.Clamp(((1 - c) * (1 - k)) * 255, 0, 255);
                rgb[j + 1] = (byte)Math.Clamp(((1 - m) * (1 - k)) * 255, 0, 255);
                rgb[j + 2] = (byte)Math.Clamp(((1 - y) * (1 - k)) * 255, 0, 255);
            }
            return rgb;
        }

        // DeviceGray 1 bpc (bi-level / CCITTFax) — unpack bit rows to RGB.
        // DeviceGray with the default Decode [0 1]: sample 0 → 0.0 (black), sample 1 → 1.0
        // (white). The CCITTFaxDecode filter already applies its /BlackIs1 flag when
        // producing these samples, so here we only honour the image's own /Decode array.
        if (cs == "DeviceGray" && bpc == 1)
        {
            // /Decode default for 1bpc is [0.0 1.0]; [1.0 0.0] inverts black/white.
            var invertBits = decode is { Length: >= 2 } && decode[0] > decode[1];
            var span = decoded.Span;
            var rgb  = new byte[pixelCount * 3];
            var rowBytes = (w + 7) / 8;
            for (var row = 0; row < h; row++)
            for (var col = 0; col < w; col++)
            {
                var byteIdx = (row * rowBytes) + (col >> 3);
                if (byteIdx >= span.Length) break;
                var bit = (span[byteIdx] >> (7 - (col & 7))) & 1;
                if (invertBits) bit = 1 - bit;
                var val = (byte)(bit == 0 ? 0 : 255); // 0=black, 1=white (DeviceGray)
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
