using System.Text;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Content;

/// <summary>
/// Parses a decoded PDF content stream into a flat sequence of
/// <see cref="ContentOperator"/> records (ISO 32000-1 §7.8.2).
/// <para>
/// Inline images (<c>BI</c>…<c>ID</c>…<c>EI</c>) are decoded at parse time and
/// emitted as a <c>BI</c> operator whose first operand is a <see cref="PdfInlineImage"/>.
/// </para>
/// </summary>
internal static class ContentStreamParser
{
    /// <summary>
    /// Parses <paramref name="data"/> and returns the ordered list of content operators.
    /// </summary>
    /// <param name="data">Decoded (decompressed) content stream bytes.</param>
    public static IReadOnlyList<ContentOperator> Parse(ReadOnlyMemory<byte> data)
    {
        var lexer = new Lexer(data);
        var parser = new PdfParser(data);

        var operands  = new List<PdfObject>();
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
                    {
                        operands.Add(parser.ReadValue(lexer));
                    }
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
        ReadOnlyMemory<byte> source)
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

            var w   = GetInt(dict, "W",   "Width",            0);
            var h   = GetInt(dict, "H",   "Height",           0);
            var bpc = GetInt(dict, "BPC", "BitsPerComponent", 8);
            var cs  = GetName(dict, "CS", "ColorSpace");
            var filterName = GetName(dict, "F",  "Filter");
            var filterName2 = GetName(dict, "DP", "DecodeParms");

            if (w <= 0 || h <= 0) return null;

            // ── Extract raw image bytes ───────────────────────────────────
            var rawBytes = ExtractInlineImageBytes(lexer, source);
            if (rawBytes.Length == 0) return null;

            // ── Apply filter ──────────────────────────────────────────────
            var decoded = ApplyInlineFilter(filterName, rawBytes);

            // ── Convert to RGB ────────────────────────────────────────────
            var rgb = ConvertToRgb(decoded, w, h, cs, bpc);
            if (rgb is null) return null;
            // Pass pixel W/H as user-space dimensions so the renderer can place the
            // image correctly even when no cm transformation precedes it.
            return new PdfInlineImage(w, h, rgb, userWidth: w, userHeight: h);
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
        string abbr, string full, int fallback)
    {
        var obj = dict.GetValueOrDefault(abbr) ?? dict.GetValueOrDefault(full);
        return obj switch
        {
            PdfInteger n => (int)n.Value,
            PdfReal r    => (int)r.Value,
            _            => fallback
        };
    }

    private static string? GetName(
        Dictionary<string, PdfObject> dict,
        string abbr, string full)
    {
        var obj = dict.GetValueOrDefault(abbr) ?? dict.GetValueOrDefault(full);
        return obj is PdfName n ? n.Value : null;
    }

    // Read raw bytes from the current lexer position until EI (preceded by whitespace).
    private static byte[] ExtractInlineImageBytes(Lexer lexer, ReadOnlyMemory<byte> source)
    {
        var span  = source.Span;
        var start = lexer.Position;
        var pos   = start;

        // Skip exactly one byte of whitespace that immediately follows the ID keyword.
        if (pos < span.Length && IsWhitespace(span[pos])) pos++;

        var dataStart = pos;

        while (pos < span.Length - 2)
        {
            if (IsWhitespace(span[pos]) &&
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
        var pos  = lexer.Position;
        while (pos < span.Length - 2)
        {
            if (IsWhitespace(span[pos]) &&
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
    private static ReadOnlyMemory<byte> ApplyInlineFilter(string? filterName, byte[] raw)
    {
        var expanded = filterName switch
        {
            "AHx" or "ASCIIHexDecode" => "ASCIIHexDecode",
            "A85" or "ASCII85Decode"  => "ASCII85Decode",
            "Fl"  or "FlateDecode"    => "FlateDecode",
            "LZW" or "LZWDecode"      => "LZWDecode",
            "RL"  or "RunLengthDecode"=> "RunLengthDecode",
            "CCF" or "CCITTFaxDecode" => "CCITTFaxDecode",
            "DCT" or "DCTDecode"      => "DCTDecode",
            null or ""                => null,
            _                         => filterName
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
    private static byte[]? ConvertToRgb(ReadOnlyMemory<byte> data, int w, int h, string? cs, int bpc)
    {
        var pixelCount = w * h;

        if ((cs is null or "DeviceRGB" or "RGB") && bpc == 8 && data.Length == pixelCount * 3)
            return data.ToArray();

        if ((cs is null or "DeviceGray" or "G") && bpc == 8 && data.Length == pixelCount)
        {
            var src = data.Span;
            var rgb = new byte[pixelCount * 3];
            for (int i = 0, j = 0; i < pixelCount; i++, j += 3)
                rgb[j] = rgb[j + 1] = rgb[j + 2] = src[i];
            return rgb;
        }

        if ((cs is "DeviceCMYK" or "CMYK") && bpc == 8 && data.Length == pixelCount * 4)
        {
            var src = data.Span;
            var rgb = new byte[pixelCount * 3];
            for (int i = 0, j = 0; i < pixelCount; i++, j += 3)
            {
                var c = src[(i * 4)    ] / 255.0;
                var m = src[(i * 4) + 1] / 255.0;
                var y = src[(i * 4) + 2] / 255.0;
                var k = src[(i * 4) + 3] / 255.0;
                rgb[j]     = (byte)Math.Clamp(((1 - c) * (1 - k)) * 255, 0, 255);
                rgb[j + 1] = (byte)Math.Clamp(((1 - m) * (1 - k)) * 255, 0, 255);
                rgb[j + 2] = (byte)Math.Clamp(((1 - y) * (1 - k)) * 255, 0, 255);
            }
            return rgb;
        }

        // DeviceGray 1 bpc — bit-packed rows.
        // PDF §8.9.5.1: for 1-bpc images the sample value 0 = white (minimum),
        // 1 = black (maximum), i.e. 0 = paper, 1 = ink (CCITT fax convention).
        if ((cs is null or "DeviceGray" or "G") && bpc == 1)
        {
            var src      = data.Span;
            var rgb      = new byte[pixelCount * 3];
            var rowBytes = (w + 7) / 8;
            for (var row = 0; row < h; row++)
            for (var col = 0; col < w; col++)
            {
                var byteIdx = (row * rowBytes) + (col >> 3);
                if (byteIdx >= src.Length) break;
                var bit = (src[byteIdx] >> (7 - (col & 7))) & 1;
                // bit=0 → white (paper), bit=1 → black (ink)
                var v   = (byte)(bit == 0 ? 255 : 0);
                var j   = ((row * w) + col) * 3;
                rgb[j] = rgb[j + 1] = rgb[j + 2] = v;
            }
            return rgb;
        }

        return null; // unsupported format
    }

    private static bool IsWhitespace(byte b) =>
        b is 0x00 or 0x09 or 0x0A or 0x0C or 0x0D or 0x20;
}
