using System.Text;
using Unchained.Drawing.Extensions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Content;

/// <summary>
///     Parses a decoded PDF content stream into a flat sequence of
///     <see cref="ContentOperator" /> records (ISO 32000-1 §7.8.2).
///     <para>
///         Inline images (<c>BI</c>…<c>ID</c>…<c>EI</c>) are decoded at parse time and
///         emitted as a <c>BI</c> operator whose first operand is a <see cref="PdfInlineImage" />.
///     </para>
/// </summary>
internal static class ContentStreamParser
{
    /// <summary>
    ///     Parses <paramref name="data" /> and returns the ordered list of content operators.
    /// </summary>
    /// <param name="data">Decoded (decompressed) content stream bytes.</param>
    public static IReadOnlyList<ContentOperator> Parse(ReadOnlyMemory<byte> data)
    {
        var lexer = new Lexer(data);
        var parser = new PdfParser(data);

        var operands = new List<PdfObject>();
        var operators = new List<ContentOperator>();

        while (!lexer.AtEnd)
        {
            var peek = lexer.Peek();

            switch (peek.Kind)
            {
                case PdfTokenKind.EndOfFile:
                    return operators;

                // ── Operand types ──────────────────────────────────────────
                case PdfTokenKind.Integer:
                case PdfTokenKind.Real:
                case PdfTokenKind.LiteralString:
                case PdfTokenKind.HexString:
                case PdfTokenKind.BooleanTrue:
                case PdfTokenKind.BooleanFalse:
                case PdfTokenKind.Null:
                case PdfTokenKind.ArrayBegin:
                case PdfTokenKind.DictionaryBegin:
                {
                    operands.Add(parser.ReadValue(lexer));
                    break;
                }

                case PdfTokenKind.Name:
                {
                    var raw = peek.Raw.Span;
                    if (raw.Length > 0 && raw[0] == (byte)'/')
                        operands.Add(parser.ReadValue(lexer));
                    else
                    {
                        lexer.ReadNext();
                        var opName = Encoding.Latin1.GetString(raw);

                        if (opName == "ID")
                        {
                            // The operands accumulated since `BI` are the image dict key-value
                            // pairs (§8.9.7).  Decode the image and emit a BI operator.
                            var inlineImage = DecodeInlineImage(operands, lexer, data);
                            operands.Clear();
                            if (inlineImage is not null)
                                operators.Add(new ContentOperator("BI", [inlineImage]));
                            break;
                        }

                        operators.Add(new ContentOperator(opName, operands.ToArray()));
                        operands.Clear();
                    }

                    break;
                }

                case PdfTokenKind.DictionaryEnd:
                case PdfTokenKind.ArrayEnd:
                case PdfTokenKind.Stream:
                case PdfTokenKind.EndStream:
                case PdfTokenKind.Obj:
                case PdfTokenKind.EndObj:
                case PdfTokenKind.IndirectRef:
                case PdfTokenKind.Xref:
                case PdfTokenKind.Trailer:
                case PdfTokenKind.StartXref:
                case PdfTokenKind.Comment:
                default:
                {
                    lexer.ReadNext();
                    break;
                }
            }
        }

        return operators;
    }

    // ── Inline image decoding ─────────────────────────────────────────────────

    // Decodes a BI…ID…EI block. The `operands` list contains the alternating
    // name/value pairs from the image parameter dictionary.
    // Returns a PdfInlineImage (RGB, 3 bytes/pixel) or null on failure.
    private static PdfInlineImage? DecodeInlineImage(
        List<PdfObject> operands,
        Lexer lexer,
        ReadOnlyMemory<byte> source
    )
    {
        try
        {
            // ── Parse image parameters ────────────────────────────────────
            // Operands arrive as flat alternating pairs: /Key Value /Key Value …
            // Inline image dict uses abbreviated names (§8.9.7, Table 92).
            var dict = new Dictionary<string, PdfObject>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i + 1 < operands.Count; i += 2)
            {
                if (operands[i] is PdfName key)
                    dict[key.Value] = operands[i + 1];
            }

            var w = GetInt(dict, "W", "Width", 0);
            var h = GetInt(dict, "H", "Height", 0);
            var bpc = GetInt(dict, "BPC", "BitsPerComponent", 8);
            var cs = GetName(dict, "CS", "ColorSpace");
            var filters = GetFilters(dict);

            if (w <= 0 || h <= 0) return null;

            // For unfiltered inline images the data length is deterministic, so read
            // exactly that many bytes. Scanning for "EI" alone is unsafe: raw binary image
            // data can contain a whitespace+E+I byte sequence by coincidence, truncating
            // the image. With a filter the encoded length is unknown, so fall back to the
            // EI scan (encoded data normally ends with an unambiguous marker before EI).
            var components = ComponentsForColorSpace(cs);
            var expectedLen = filters.Count == 0 && components > 0
                ? (long)w * h * components * bpc / 8
                : 0;

            var rawBytes = ExtractInlineImageBytes(lexer, source, expectedLen);
            if (rawBytes.Length == 0) return null;

            // ── Apply filters in sequence ─────────────────────────────────
            var decoded = ApplyInlineFilters(filters, rawBytes);

            // ── Convert to RGB ────────────────────────────────────────────
            var rgb = ConvertToRgb(decoded, w, h, cs, bpc);
            if (rgb is null) return null;
            // Inline images fill the unit square [0,0]→[1,1] in the current CTM,
            // exactly like Do XObjects. The cm matrix placed before BI maps the
            // unit square to the desired position and size; pixel dimensions here
            // would cause wrong placement when a cm transform is present.
            return new PdfInlineImage(w, h, rgb, 1, 1);
        }
        catch
        {
            // Advance past EI so the stream remains in sync even on error.
            SkipToEi(lexer, source);
            return null;
        }
    }

    private static int GetInt(
        Dictionary<string, PdfObject> dict,
        string abbr,
        string full,
        int fallback
    )
    {
        var obj = dict.GetValueOrDefault(abbr) ?? dict.GetValueOrDefault(full);
        return obj switch
        {
            PdfInteger n => (int)n.Value,
            PdfReal r => (int)r.Value,
            _ => fallback
        };
    }

    private static string? GetName(
        Dictionary<string, PdfObject> dict,
        string abbr,
        string full
    )
    {
        var obj = dict.GetValueOrDefault(abbr) ?? dict.GetValueOrDefault(full);
        return obj is PdfName n ? n.Value : null;
    }

    // Maps an inline-image colour space (abbreviated or full name) to its component count.
    // Returns 0 for unknown/indexed spaces where the unfiltered length can't be derived.
    private static int ComponentsForColorSpace(string? cs) => cs switch
    {
        "G" or "DeviceGray" or "CalGray" => 1,
        "RGB" or "DeviceRGB" or "CalRGB" => 3,
        "CMYK" or "DeviceCMYK" => 4,
        _ => 0
    };

    // Read raw bytes for an inline image. When expectedLen > 0 (unfiltered data of known
    // size) read exactly that many bytes, then skip the trailing whitespace and EI. This
    // avoids truncating binary data that coincidentally contains a whitespace+E+I sequence.
    // Otherwise (filtered data of unknown length) scan for a whitespace-delimited EI.
    private static byte[] ExtractInlineImageBytes(Lexer lexer, ReadOnlyMemory<byte> source, long expectedLen = 0)
    {
        var span = source.Span;
        var pos = lexer.Position;

        // Skip exactly one byte of whitespace that immediately follows the ID keyword.
        if (pos < span.Length && span[pos].IsWhitespace()) pos++;

        var dataStart = pos;

        if (expectedLen > 0 && dataStart + expectedLen <= span.Length)
        {
            var end = dataStart + (int)expectedLen;
            var data = span[dataStart..end].ToArray();
            // Advance past optional whitespace + EI so the stream stays in sync.
            var p = end;
            while (p < span.Length && span[p].IsWhitespace()) p++;
            if (p + 1 < span.Length && span[p] == (byte)'E' && span[p + 1] == (byte)'I')
                p += 2;
            lexer.Seek(p);
            return data;
        }

        while (pos < span.Length - 2)
        {
            if (span[pos].IsWhitespace() &&
                span[pos + 1] == (byte)'E' &&
                span[pos + 2] == (byte)'I')
            {
                var data = span[dataStart..pos].ToArray();
                lexer.Seek(pos + 3); // advance past EI
                return data;
            }

            pos++;
        }

        lexer.Seek(span.Length);
        return [];
    }

    private static void SkipToEi(Lexer lexer, ReadOnlyMemory<byte> source)
    {
        var span = source.Span;
        var pos = lexer.Position;
        while (pos < span.Length - 2)
        {
            if (span[pos].IsWhitespace() &&
                span[pos + 1] == (byte)'E' &&
                span[pos + 2] == (byte)'I')
            {
                lexer.Seek(pos + 3);
                return;
            }

            pos++;
        }

        lexer.Seek(span.Length);
    }

    // Apply the inline image filter.  Abbreviated filter names per Table 92.
    // Reads the inline-image /F (Filter) entry, which may be a single name or an array of
    // names applied in sequence (§8.9.7). Returns an empty list when no filter is present.
    private static List<string> GetFilters(Dictionary<string, PdfObject> dict)
    {
        var obj = dict.GetValueOrDefault("F") ?? dict.GetValueOrDefault("Filter");
        var result = new List<string>();
        switch (obj)
        {
            case PdfName n:
                result.Add(n.Value);
            break;
            case PdfArray arr:
                foreach (var e in arr.Elements)
                    if (e is PdfName en)
                        result.Add(en.Value);
            break;
        }

        return result;
    }

    // Applies a sequence of inline-image filters in order.
    private static ReadOnlyMemory<byte> ApplyInlineFilters(List<string> filters, byte[] raw)
    {
        ReadOnlyMemory<byte> data = raw;
        foreach (var f in filters)
            data = ApplyInlineFilter(f, data.ToArray());
        return data;
    }

    private static ReadOnlyMemory<byte> ApplyInlineFilter(string? filterName, byte[] raw)
    {
        var expanded = filterName switch
        {
            "AHx" or "ASCIIHexDecode" => "ASCIIHexDecode",
            "A85" or "ASCII85Decode" => "ASCII85Decode",
            "Fl" or "FlateDecode" => "FlateDecode",
            "LZW" or "LZWDecode" => "LZWDecode",
            "RL" or "RunLengthDecode" => "RunLengthDecode",
            "CCF" or "CCITTFaxDecode" => "CCITTFaxDecode",
            "DCT" or "DCTDecode" => "DCTDecode",
            null or "" => null,
            _ => filterName
        };

        if (expanded is null) return raw;

        // Build a minimal PdfStream wrapping the raw bytes so we can reuse StreamFilters.
        try
        {
            var dict = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["Filter"] = PdfName.Get(expanded)
            });
            var stream = new PdfStream(dict, raw);
            return StreamFilters.Decode(stream);
        }
        catch
        {
            return raw; // return unfiltered on failure
        }
    }

    // Convert decoded bytes to packed RGB (3 bytes/pixel).
    private static byte[]? ConvertToRgb(
        ReadOnlyMemory<byte> data,
        int w,
        int h,
        string? cs,
        int bpc
    )
    {
        var pixelCount = w * h;

        if (cs is null or "DeviceRGB" or "RGB" && bpc == 8 && data.Length == pixelCount * 3)
            return data.ToArray();

        if (cs is null or "DeviceGray" or "G" && bpc == 8 && data.Length == pixelCount)
        {
            var src = data.Span;
            var rgb = new byte[pixelCount * 3];
            for (int i = 0, j = 0; i < pixelCount; i++, j += 3)
                rgb[j] = rgb[j + 1] = rgb[j + 2] = src[i];
            return rgb;
        }

        if (cs is "DeviceCMYK" or "CMYK" && bpc == 8 && data.Length == pixelCount * 4)
        {
            var src = data.Span;
            var rgb = new byte[pixelCount * 3];
            for (int i = 0, j = 0; i < pixelCount; i++, j += 3)
            {
                var c = src[i * 4] / 255.0;
                var m = src[(i * 4) + 1] / 255.0;
                var y = src[(i * 4) + 2] / 255.0;
                var k = src[(i * 4) + 3] / 255.0;
                rgb[j] = (byte)Math.Clamp((1 - c) * (1 - k) * 255, 0, 255);
                rgb[j + 1] = (byte)Math.Clamp((1 - m) * (1 - k) * 255, 0, 255);
                rgb[j + 2] = (byte)Math.Clamp((1 - y) * (1 - k) * 255, 0, 255);
            }

            return rgb;
        }

        // DeviceGray 1 bpc — bit-packed rows.
        // PDF §8.9.5.1: for 1-bpc images the sample value 0 = white (minimum),
        // 1 = black (maximum), i.e. 0 = paper, 1 = ink (CCITT fax convention).
        if (cs is null or "DeviceGray" or "G" && bpc == 1)
        {
            var src = data.Span;
            var rgb = new byte[pixelCount * 3];
            var rowBytes = (w + 7) / 8;
            for (var row = 0; row < h; row++)
            for (var col = 0; col < w; col++)
            {
                var byteIdx = (row * rowBytes) + (col >> 3);
                if (byteIdx >= src.Length) break;
                var bit = (src[byteIdx] >> (7 - (col & 7))) & 1;
                // bit=0 → white (paper), bit=1 → black (ink)
                var v = (byte)(bit == 0 ? 255 : 0);
                var j = ((row * w) + col) * 3;
                rgb[j] = rgb[j + 1] = rgb[j + 2] = v;
            }

            return rgb;
        }

        return null; // unsupported format
    }
}
