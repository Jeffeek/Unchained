using System.Text;
using Unchained.Pdf.Content;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Engine.PageResources;

/// <summary>
///     Resolves the font-related resources of a page: the resource-name → base-font-name map,
///     embedded font program bytes, composite (Type0) font metadata, Type3 glyph procedures,
///     and ToUnicode CMaps. Extracted from <see cref="PdfPageAdapter" />.
/// </summary>
internal static class PageFontResolver
{
    private static void ForEachFontEntry(
        PdfDictionary page,
        PdfDocumentCore core,
        Action<string, PdfObject?, PdfDictionary?> action
    )
    {
        var resources = core.ResolveDict(page[PdfName.Resources]);
        var fontDict = core.ResolveDict(resources?[PdfName.Font]);
        if (fontDict is null) return;

        foreach (var (key, value) in fontDict.Entries)
            action(key, value, core.ResolveDict(value));
    }

    private static Dictionary<TKey, TValue> CollectFontEntries<TKey, TValue>(
        PdfDictionary page,
        PdfDocumentCore core,
        Func<string, PdfObject?, PdfDictionary?, (TKey Key, TValue Value, bool Include)> selector
    )
        where TKey : notnull
    {
        var result = new Dictionary<TKey, TValue>();
        ForEachFontEntry(
            page,
            core,
            (key, value, fontEntry) =>
            {
                var (k, v, include) = selector(key, value, fontEntry);
                if (include) result[k] = v;
            }
        );
        return result;
    }

    // Walks the page /Resources /Font dictionary and maps each resource name (e.g. "F1")
    // to the actual base font name (e.g. "Helvetica") for AFM width lookup.
    internal static Dictionary<string, string> ResolveFontNames(PdfDictionary page, PdfDocumentCore core) =>
        CollectFontEntries(
            page,
            core,
            static (key, _, fontEntry) =>
            {
                var baseFontName = fontEntry?.GetName(PdfName.BaseFont.Value);
                return (key, baseFontName!, baseFontName is not null);
            }
        );

    internal static IReadOnlyDictionary<string, byte[]?> GetEmbeddedFontBytes(PdfDictionary page, PdfDocumentCore core)
    {
        var result = new Dictionary<string, byte[]?>();
        ForEachFontEntry(
            page,
            core,
            (key, _, fontEntry) =>
            {
                if (fontEntry is null)
                {
                    result[key] = null;
                    return;
                }

                // For composite fonts (/Subtype /Type0) the embedded font program lives in
                // the FontDescriptor of the descendant CIDFont, not the top-level font dict
                // (§9.7.4). Follow /DescendantFonts to reach it.
                var descriptorHolder = fontEntry;
                if (fontEntry.IsSubtype("Type0"))
                {
                    var descendants = fontEntry[PdfName.DescendantFonts];
                    if (descendants is PdfIndirectReference dr)
                        descendants = core.ResolveIndirect(dr.ObjectNumber).Value;
                    var cidFont = descendants switch
                    {
                        PdfArray { Count: > 0 } a => core.ResolveDict(a[0]),
                        PdfDictionary d => d,
                        _ => null
                    };
                    if (cidFont is not null)
                        descriptorHolder = cidFont;
                }

                var descriptor = core.ResolveDict(descriptorHolder[PdfName.FontDescriptor]);
                if (descriptor is null)
                {
                    result[key] = null;
                    return;
                }

                // Try /FontFile2 (TrueType), /FontFile3 (OpenType/CFF), /FontFile (Type1) in order.
                var streamRef = descriptor[PdfName.FontFile2]
                                ?? descriptor[PdfName.FontFile3]
                                ?? descriptor[PdfName.FontFile];

                if (streamRef is null)
                {
                    result[key] = null;
                    return;
                }

                var fontStream = core.ResolveStream(streamRef);

                result[key] = fontStream is not null
                    ? StreamFilters.Decode(fontStream).ToArray()
                    : null;
            }
        );
        return result;
    }

    internal static IReadOnlyDictionary<string, CompositeFontInfo> GetCompositeFonts(PdfDictionary page, PdfDocumentCore core)
    {
        var result = new Dictionary<string, CompositeFontInfo>();
        ForEachFontEntry(
            page,
            core,
            (key, _, fontEntry) =>
            {
                if (fontEntry is null || fontEntry.GetName(PdfName.Subtype.Value) != "Type0")
                    return;

                // /Encoding may be a name (e.g. /Identity-H) or a CMap stream. We only
                // fast-path the Identity CMaps where each 2-byte code equals the CID.
                var encName = (fontEntry[PdfName.Encoding.Value] as PdfName)?.Value;
                var identityEncoding = encName is "Identity-H" or "Identity-V";

                var descendants = fontEntry[PdfName.DescendantFonts];
                if (descendants is PdfIndirectReference dr)
                    descendants = core.ResolveIndirect(dr.ObjectNumber).Value;
                var cidFont = descendants switch
                {
                    PdfArray { Count: > 0 } a => core.ResolveDict(a[0]),
                    PdfDictionary d => d,
                    _ => null
                };
                if (cidFont is null)
                    return;

                // /CIDToGIDMap: /Identity (or absent) => CID == GID; otherwise a stream of
                // 2-byte big-endian GIDs indexed by CID.
                var c2GObj = cidFont[PdfName.CIDToGIDMap.Value];
                if (c2GObj is PdfIndirectReference cr)
                    c2GObj = core.ResolveIndirect(cr.ObjectNumber).Value;
                var identityCidToGid = c2GObj is null || (c2GObj as PdfName)?.Value == "Identity";
                IReadOnlyDictionary<int, int>? cidToGid = null;
                if (!identityCidToGid && c2GObj is PdfStream c2GStream)
                {
                    var bytes = StreamFilters.Decode(c2GStream).Span;
                    var map = new Dictionary<int, int>();
                    for (var cid = 0; (cid * 2) + 1 < bytes.Length; cid++)
                    {
                        var gid = (bytes[cid * 2] << 8) | bytes[(cid * 2) + 1];
                        if (gid != 0) map[cid] = gid;
                    }

                    cidToGid = map;
                }

                var dwInt = cidFont.Get<PdfInteger>(PdfName.DW)?.Value;
                var dwReal = cidFont.Get<PdfReal>(PdfName.DW)?.Value;
                var dw = dwInt ?? dwReal ?? 1000.0;
                var widths = ParseCidWidths(core, cidFont["W"]);

                result[key] = new CompositeFontInfo(
                    identityEncoding,
                    identityCidToGid,
                    cidToGid,
                    dw,
                    widths
                );
            }
        );

        return result;
    }

    internal static IReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>> GetToUnicodeMaps(PdfDictionary page, PdfDocumentCore core)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<uint, string>>();
        ForEachFontEntry(
            page,
            core,
            (key, _, fontEntry) =>
            {
                if (fontEntry is null) return;

                var tuRef = fontEntry[PdfName.ToUnicode];
                var tuStream = core.ResolveStream(tuRef);
                if (tuStream is null) return;

                try
                {
                    var cmap = ParseToUnicodeCmap(StreamFilters.Decode(tuStream).Span);
                    if (cmap.Count > 0) result[key] = cmap;
                }
                catch
                {
                    /* malformed CMap — skip */
                }
            }
        );

        return result;
    }

    internal static IReadOnlyDictionary<string, Type3FontInfo> GetType3Fonts(PdfDictionary page, PdfDocumentCore core)
    {
        var result = new Dictionary<string, Type3FontInfo>();
        ForEachFontEntry(
            page,
            core,
            (resName, _, font) =>
            {
                if (font is null) return;
                if (font.GetName(PdfName.Subtype.Value) != "Type3") return;

                // /FontMatrix: glyph space → text space transform.
                var fm = font[PdfName.FontMatrix] is PdfArray { Count: >= 6 } fmArr
                    ? fmArr.Elements.Take(6)
                        .Select(static e =>
                            e switch
                            {
                                PdfReal rr => rr.Value,
                                PdfInteger ii => ii.Value,
                                _ => 0.0
                            }
                        )
                        .ToArray()
                    : [FontConstants.Type3DefaultMatrixScale, 0, 0, FontConstants.Type3DefaultMatrixScale, 0, 0];

                // /Encoding: maps char codes 0–255 to glyph names.
                var encoding = new string?[256];
                var encObj = font[PdfName.Encoding];
                if (encObj is PdfIndirectReference er)
                    encObj = core.ResolveIndirect(er.ObjectNumber).Value;

                switch (encObj)
                {
                    case PdfDictionary encDict:
                    {
                        // /Differences array: [firstCode /name1 /name2 …]
                        if (encDict[PdfName.Differences] is PdfArray diff)
                        {
                            var code = 0;
                            foreach (var elem in diff.Elements)
                            {
                                switch (elem)
                                {
                                    case PdfInteger ic:
                                        code = (int)ic.Value;
                                    break;
                                    case PdfName gn when code < 256:
                                        encoding[code] = gn.Value;
                                        code++;
                                    break;
                                }
                            }
                        }

                        break;
                    }
                    case PdfName encName:
                    {
                        // Standard encoding names — use a simple ASCII fallback.
                        if (encName.Value is "StandardEncoding" or "WinAnsiEncoding" or "MacRomanEncoding")
                        {
                            for (var c = 32; c < 127; c++)
                                encoding[c] = ((char)c).ToString();
                        }

                        break;
                    }
                }

                // /CharProcs: glyph name → stream of content operators.
                var charProcs = new Dictionary<string, IReadOnlyList<ContentOperator>>();
                var cpDict = core.ResolveDict(font[PdfName.CharProcs]);
                if (cpDict is not null)
                {
                    foreach (var (glyphName, streamObj) in cpDict.Entries)
                    {
                        var streamRef = streamObj is PdfIndirectReference sr
                            ? core.ResolveIndirect(sr.ObjectNumber).Value
                            : streamObj;
                        if (streamRef is not PdfStream glyphStream)
                            continue;

                        try
                        {
                            var decoded = StreamFilters.Decode(glyphStream);
                            var ops = ContentStreamParser.Parse(decoded);
                            charProcs[glyphName] = ops;
                        }
                        // ReSharper disable once EmptyGeneralCatchClause
                        catch { }
                    }
                }

                // /Widths and /FirstChar.
                var firstChar = (int)((font[PdfName.FirstChar] as PdfInteger)?.Value ?? 0);
                var widths = font[PdfName.Widths] is PdfArray widthsArr
                    ? widthsArr.Elements
                        .Select(static e => e switch
                            {
                                PdfReal wr => wr.Value,
                                PdfInteger wi => wi.Value,
                                _ => 0.0
                            }
                        )
                        .ToArray()
                    : [];

                result[resName] = new Type3FontInfo
                {
                    FontMatrix = fm,
                    Encoding = encoding,
                    CharProcs = charProcs,
                    Widths = widths,
                    FirstChar = firstChar
                };
            }
        );

        return result;
    }

    // Parses a CIDFont /W array (§9.7.4.3) into a CID->width map (glyph-space units).
    // Two forms: "c [w1 w2 ...]" (CIDs c, c+1, ...) and "cFirst cLast w" (range, same width).
    private static IReadOnlyDictionary<int, double> ParseCidWidths(PdfDocumentCore core, PdfObject? wObj)
    {
        var result = new Dictionary<int, double>();
        if (wObj is PdfIndirectReference r)
            wObj = core.ResolveIndirect(r.ObjectNumber).Value;
        if (wObj is not PdfArray arr)
            return result;

        var i = 0;
        while (i < arr.Count)
        {
            if (arr[i] is not (PdfInteger or PdfReal))
            {
                i++;
                continue;
            }

            var first = (int)arr[i].ReadIntOrReal();
            if (i + 1 >= arr.Count) break;

            if (arr[i + 1] is PdfArray widthList)
            {
                for (var k = 0; k < widthList.Count; k++)
                    result[first + k] = widthList[k].ReadIntOrReal();
                i += 2;
            }
            else if (i + 2 < arr.Count
                     && arr[i + 1] is PdfInteger or PdfReal
                     && arr[i + 2] is PdfInteger or PdfReal)
            {
                var last = (int)arr[i + 1].ReadIntOrReal();
                var w = arr[i + 2].ReadIntOrReal();
                for (var cid = first; cid <= last && cid - first < FontConstants.MaxCidRangeSize; cid++)
                    result[cid] = w;
                i += 3;
            }
            else
                i++;
        }

        return result;
    }

    // Parses a PDF ToUnicode CMap stream and returns a char-code → Unicode string mapping.
    // Handles beginbfchar (individual mappings) and beginbfrange (range mappings).
    private static IReadOnlyDictionary<uint, string> ParseToUnicodeCmap(ReadOnlySpan<byte> cmap)
    {
        var result = new Dictionary<uint, string>();
        var text = Encoding.Latin1.GetString(cmap);
        var lines = text.Split('\n');
        var mode = 0; // 0=none, 1=bfchar, 2=bfrange

        foreach (var line in lines.Select(static rawLine => rawLine.Trim()))
        {
            // The count is usually prefixed: "27 beginbfchar", so check EndsWith.
            if (line.EndsWith("beginbfchar", StringComparison.OrdinalIgnoreCase))
            {
                mode = 1;
                continue;
            }

            if (line == "endbfchar")
            {
                mode = 0;
                continue;
            }

            if (line.EndsWith("beginbfrange", StringComparison.OrdinalIgnoreCase))
            {
                mode = 2;
                continue;
            }

            if (line == "endbfrange")
            {
                mode = 0;
                continue;
            }

            if (mode == 0)
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            switch (mode)
            {
                case 1 when parts.Length >= 2:
                {
                    // <charCode> <unicodeValue>
                    var charCode = ParseHexToken(parts[0]);
                    var unicode = ParseHexToken(parts[1]);
                    if (charCode.Length == 0 || unicode.Length == 0)
                        continue;

                    var key = charCode.Aggregate<byte, uint>(0, static (current, b) => (current << 8) | b);

                    var uniStr = DecodeUtf16Be(unicode);
                    if (uniStr.Length > 0) result[key] = uniStr;
                    break;
                }
                case 2 when parts.Length >= 3:
                {
                    // <srcLo> <srcHi> <dstStart>   — maps a contiguous range
                    var lo = ParseHexToken(parts[0]);
                    var hi = ParseHexToken(parts[1]);
                    var dst = ParseHexToken(parts[2]);
                    if (lo.Length == 0 || hi.Length == 0 || dst.Length == 0) continue;

                    uint loKey = 0, hiKey = 0;
                    loKey = lo.Aggregate(loKey, static (current, b) => (current << 8) | b);
                    hiKey = hi.Aggregate(hiKey, static (current, b) => (current << 8) | b);

                    // dst is the starting Unicode code point (UTF-16BE big-endian)
                    var dstCp = dst.Aggregate<byte, uint>(0, static (current, b) => (current << 8) | b);

                    for (var key = loKey; key <= hiKey; key++)
                    {
                        var cp = dstCp + (key - loKey);
                        result[key] = char.ConvertFromUtf32((int)cp);
                    }

                    break;
                }
            }
        }

        return result;
    }

    // Parses a <hexhex> token and returns the decoded bytes. Returns empty on failure.
    private static byte[] ParseHexToken(string token)
    {
        token = token.Trim();
        if (!token.StartsWith('<') || !token.EndsWith('>'))
            return [];

        var hex = token[1..^1];
        if (hex.Length % 2 != 0) hex += "0";
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            try
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            catch
            {
                return [];
            }
        }

        return bytes;
    }

    private static string DecodeUtf16Be(IReadOnlyList<byte> bytes)
    {
        if (bytes.Count % 2 != 0 || bytes.Count == 0)
            return string.Empty;

        var chars = new char[bytes.Count / 2];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = (char)((bytes[i * 2] << 8) | bytes[(i * 2) + 1]);

        return new string(chars);
    }
}
