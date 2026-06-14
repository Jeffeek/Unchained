using System.Globalization;
using System.Text;
using Unchained.Pdf.Core;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Parsing;

/// <summary>
///     Parses a raw PDF byte buffer into a structured object graph
///     (ISO 32000-1 §7.3 object types + §7.5 file structure).
///     <para>
///         <b>Read strategy:</b>
///         <list type="number">
///             <item>Locate <c>startxref</c> near the end of the file (§7.5.5).</item>
///             <item>Parse the cross-reference table or stream at that offset (§7.5.4 / §7.5.8).</item>
///             <item>Read the trailer dictionary to locate the <c>/Root</c> catalog object.</item>
///             <item>Resolve individual objects lazily via their cross-reference byte offsets.</item>
///         </list>
///         Incremental updates (§7.5.6) are handled by chaining multiple xref sections
///         through the <c>/Prev</c> entry; the most-recent definition of any object wins.
///     </para>
/// </summary>
internal sealed class PdfParser(ReadOnlyMemory<byte> source)
{
    // ── Entry points ──────────────────────────────────────────────────────────

    /// <summary>
    ///     Performs the initial structural parse: locates and reads the cross-reference table
    ///     (including incremental-update chains) and returns the merged table together with
    ///     the most-recent trailer dictionary.
    ///     Individual objects are <b>not</b> loaded — they are resolved on demand by
    ///     <see cref="Unchained.Pdf.Document.PdfDocumentCore" /> via <see cref="ReadObject" />.
    /// </summary>
    /// <returns>
    ///     A tuple of the merged <see cref="CrossReferenceTable" /> and the trailer
    ///     <see cref="PdfDictionary" /> from the most-recent update section.
    /// </returns>
    /// <exception cref="PdfException">
    ///     Thrown when <c>startxref</c> cannot be found or the xref section is malformed.
    /// </exception>
    public (CrossReferenceTable Xref, PdfDictionary Trailer) ParseStructure()
    {
        var startXref = FindStartXref();
        return ParseXrefChain(startXref);
    }

    /// <summary>
    ///     Reads and fully parses the indirect object whose body begins at
    ///     <paramref name="byteOffset" /> in the source buffer.
    ///     Called on demand by <see cref="Unchained.Pdf.Document.PdfDocumentCore.ResolveIndirect" />
    ///     when an indirect reference is first dereferenced.
    /// </summary>
    /// <param name="byteOffset">
    ///     The absolute byte offset from the start of the source buffer, as stored in the
    ///     <see cref="CrossReferenceEntry.Offset" /> of an in-use cross-reference entry.
    /// </param>
    /// <returns>The parsed <see cref="PdfIndirectObject" />.</returns>
    /// <exception cref="PdfException">Thrown when the object header or body is malformed.</exception>
    public PdfIndirectObject ReadObject(long byteOffset)
    {
        var lexer = new Lexer(source, (int)byteOffset);

        // If the stored offset is stale (e.g., CRLF-converted file), scan forward for
        // the nearest "N G obj" header within 256 bytes.
        if (!lexer.Peek().Is(PdfTokenKind.Integer))
        {
            var adjusted = ScanForwardForObjectHeader(byteOffset);
            if (adjusted >= 0)
                lexer = new Lexer(source, adjusted);
        }

        var objNum = ExpectInteger(lexer);
        var generation = ExpectInteger(lexer);
        Expect(lexer, PdfTokenKind.Obj);

        var value = ReadValue(lexer);

        // A stream object follows with "stream" keyword — consume it if present.
        var next = lexer.Peek();
        if (!next.Is(PdfTokenKind.Stream) || value is not PdfDictionary dict)
            return new PdfIndirectObject((int)objNum, (int)generation, value);

        lexer.ReadNext(); // consume "stream"
        SkipNewline(lexer);

        // /Length may be stored as an indirect reference (/Length 3 0 R).
        // dict.Get<PdfInteger> returns null in that case, giving length=0 and an
        // empty stream. Fall back to scanning for "endstream" when this happens.
        var length = dict.Get<PdfInteger>(PdfName.Length)?.Value
                     ?? ScanForStreamLength(source.Span, lexer.Position);

        var data = source.Slice(lexer.Position, (int)length);
        value = new PdfStream(dict, data);

        return new PdfIndirectObject((int)objNum, (int)generation, value);
    }

    // ── Object value parser ───────────────────────────────────────────────────

    /// <summary>
    ///     Reads one complete PDF value from <paramref name="lexer" /> and returns it as
    ///     the appropriate <see cref="PdfObject" /> subtype. Handles all eight PDF primitive
    ///     types plus arrays, dictionaries, and indirect references.
    /// </summary>
    /// <exception cref="PdfException">Thrown when an unexpected token is encountered.</exception>
    internal PdfObject ReadValue(Lexer lexer)
    {
        var token = lexer.ReadNext();
        return token.Kind switch
        {
            PdfTokenKind.Integer => ReadIntegerOrRef(token, lexer),
            PdfTokenKind.Real => ParseReal(token),
            PdfTokenKind.BooleanTrue => PdfBoolean.True,
            PdfTokenKind.BooleanFalse => PdfBoolean.False,
            PdfTokenKind.Null => PdfNull.Instance,
            PdfTokenKind.Name => ParseName(token),
            PdfTokenKind.LiteralString => ParseLiteralString(token),
            PdfTokenKind.HexString => ParseHexString(token),
            PdfTokenKind.ArrayBegin => ReadArray(lexer),
            PdfTokenKind.DictionaryBegin => ReadDictionary(lexer),
            // ReSharper disable PatternIsRedundant
            PdfTokenKind.DictionaryEnd or
                PdfTokenKind.ArrayEnd or
                PdfTokenKind.Stream or
                PdfTokenKind.EndStream or
                PdfTokenKind.Obj or
                PdfTokenKind.EndObj or
                PdfTokenKind.IndirectRef or
                PdfTokenKind.Xref or
                PdfTokenKind.Trailer or
                PdfTokenKind.StartXref or
                PdfTokenKind.Comment or
                PdfTokenKind.EndOfFile or
                // ReSharper restore PatternIsRedundant
                _ => throw new PdfException($"Unexpected token {token.Kind} while reading value.", token.Offset)
        };
    }

    // Disambiguates between a bare integer and the start of an indirect reference "N G R".
    private static PdfObject ReadIntegerOrRef(PdfToken first, Lexer lexer)
    {
        var next = lexer.Peek();
        if (next.Kind != PdfTokenKind.Integer)
            return ParseInteger(first);

        var saved = lexer.Position;
        var second = lexer.ReadNext();
        var third = lexer.Peek();
        if (third.Kind == PdfTokenKind.IndirectRef)
        {
            lexer.ReadNext(); // consume R
            return new PdfIndirectReference(
                (int)ParseRawInteger(first.Raw.Span),
                (int)ParseRawInteger(second.Raw.Span)
            );
        }

        lexer.Seek(saved);
        return ParseInteger(first);
    }

    private PdfArray ReadArray(Lexer lexer)
    {
        var items = new List<PdfObject>();
        while (true)
        {
            var peek = lexer.Peek();
            if (peek.Is(PdfTokenKind.ArrayEnd))
            {
                lexer.ReadNext();
                break;
            }

            if (peek.Is(PdfTokenKind.EndOfFile))
                throw new PdfException("Unexpected end of file inside array.", peek.Offset);

            items.Add(ReadValue(lexer));
        }

        return new PdfArray(items);
    }

    private PdfDictionary ReadDictionary(Lexer lexer)
    {
        var entries = new Dictionary<string, PdfObject>();
        while (true)
        {
            var peek = lexer.Peek();
            if (peek.Is(PdfTokenKind.DictionaryEnd))
            {
                lexer.ReadNext();
                break;
            }

            if (peek.Is(PdfTokenKind.EndOfFile))
                throw new PdfException("Unexpected end of file inside dictionary.", peek.Offset);

            var keyToken = lexer.ReadNext();
            if (keyToken.Kind != PdfTokenKind.Name)
                throw new PdfException($"Expected name key in dictionary, got {keyToken.Kind}.", keyToken.Offset);

            var key = ParseRawName(keyToken.Raw.Span);
            var value = ReadValue(lexer);
            entries[key] = value;
        }

        return new PdfDictionary(entries);
    }

    // ── Stream length fallback ────────────────────────────────────────────────

    // When /Length is an indirect reference (e.g. /Length 3 0 R), dict.Get<PdfInteger>
    // returns null. This helper scans forward from the stream data start for the
    // "endstream" keyword (preceded by CR, LF, or CRLF) to determine the true length.
    private static long ScanForStreamLength(ReadOnlySpan<byte> source, int dataStart)
    {
        // Search for <LF>endstream or <CR><LF>endstream
        var endstream = "endstream"u8;
        for (var i = dataStart; i < source.Length - endstream.Length; i++)
        {
            if (source[i] != (byte)'e') continue;
            if (!source.Slice(i, endstream.Length).SequenceEqual(endstream)) continue;

            // Verify it's preceded by a newline
            var beforeLen = i - dataStart;
            return beforeLen switch
            {
                > 0 when source[i - 1] == '\n' => beforeLen - 1,
                > 1 when source[i - 2] == '\r' && source[i - 1] == '\n' => beforeLen - 2,
                > 0 when source[i - 1] == '\r' => beforeLen - 1,
                _ => beforeLen
            };
        }

        return 0;
    }

    // ── Cross-reference parsing ───────────────────────────────────────────────

    // Scans backwards from the end of the file (last 1 KB) for "startxref"
    // and returns the xref byte offset that follows it.
    private long FindStartXref()
    {
        var span = source.Span;
        var searchStart = Math.Max(0, span.Length - PdfConstants.XrefScanWindowBytes);
        for (var i = span.Length - 9; i >= searchStart; i--)
        {
            if (!span.Slice(i, 9).SequenceEqual("startxref"u8))
                continue;

            return ExpectInteger(new Lexer(source, i + 9));
        }

        throw new PdfException("Could not locate 'startxref' near end of file.");
    }

    // Follows the /Prev chain, merging all xref sections into a single table.
    // Earlier sections take priority: TryAdd ensures the first (most-recent) definition wins.
    private (CrossReferenceTable, PdfDictionary) ParseXrefChain(long xrefOffset)
    {
        var allEntries = new Dictionary<int, CrossReferenceEntry>();
        PdfDictionary? trailer = null;

        while (xrefOffset > 0)
        {
            var (entries, trailerDict, prev) = ParseSingleXref(xrefOffset);
            foreach (var (k, v) in entries)
                allEntries.TryAdd(k, v);

            trailer ??= trailerDict;
            xrefOffset = prev;
        }

        return (new CrossReferenceTable(allEntries, xrefOffset), trailer!);
    }

    private (Dictionary<int, CrossReferenceEntry> Entries, PdfDictionary Trailer, long Prev) ParseSingleXref(long offset)
    {
        var lexer = new Lexer(source, (int)offset);
        var peek = lexer.Peek();

        if (peek.Is(PdfTokenKind.Xref))
            return ParseTraditionalXref(lexer);

        if (peek.Is(PdfTokenKind.Integer))
            return ParseXrefStream(offset);

        // The stored offset is stale — common when CRLF-converted files shift every byte
        // position after generation. Scan forward up to 1 KB for the "xref" keyword.
        var adjusted = ScanForwardForXref(offset);
        if (adjusted >= 0)
        {
            var adjustedLexer = new Lexer(source, adjusted);
            return adjustedLexer.Peek().Is(PdfTokenKind.Xref)
                ? ParseTraditionalXref(adjustedLexer)
                : ParseXrefStream(adjusted);
        }

        throw new PdfException($"Cross-reference table not found at or near offset 0x{offset:X}.", offset);
    }

    // Scans forward from startOffset (up to 1 KB) for a "xref" keyword at a line boundary.
    private int ScanForwardForXref(long startOffset)
    {
        var span = source.Span;
        var limit = (int)Math.Min(startOffset + PdfConstants.XrefScanWindowBytes, span.Length - 4);

        for (var i = (int)startOffset; i < limit; i++)
        {
            if (span[i] != (byte)'x')
                continue;

            if (!span.Slice(i, 4).SequenceEqual("xref"u8))
                continue;

            if (i == 0 || span[i - 1] is (byte)'\r' or (byte)'\n')
                return i;
        }

        return -1;
    }

    // Scans forward from startOffset (up to 256 bytes) for an indirect-object header
    // ("N G obj") at a line boundary — used to recover from stale xref offsets.
    private int ScanForwardForObjectHeader(long startOffset)
    {
        var span = source.Span;
        var limit = (int)Math.Min(startOffset + 256, span.Length);
        for (var i = (int)startOffset; i < limit; i++)
        {
            if (span[i] < (byte)'0' || span[i] > (byte)'9')
                continue;

            if (i > 0 && span[i - 1] is not ((byte)'\r' or (byte)'\n'))
                continue;

            // Verify this looks like "N G obj": two integers separated by space, then " obj"
            var testLexer = new Lexer(source, i);
            if (!testLexer.Peek().Is(PdfTokenKind.Integer))
                continue;

            testLexer.ReadNext();
            if (!testLexer.Peek().Is(PdfTokenKind.Integer))
                continue;

            testLexer.ReadNext();
            if (testLexer.Peek().Is(PdfTokenKind.Obj))
                return i;
        }

        return -1;
    }

    // Parses a traditional "xref\n<subsection> ... trailer\n<dict>" block (§7.5.4).
    private (Dictionary<int, CrossReferenceEntry>, PdfDictionary, long) ParseTraditionalXref(Lexer lexer)
    {
        lexer.ReadNext(); // consume "xref"
        var entries = new Dictionary<int, CrossReferenceEntry>();

        while (lexer.Peek().Kind == PdfTokenKind.Integer)
        {
            var firstObj = (int)ExpectInteger(lexer);
            var count = (int)ExpectInteger(lexer);

            for (var i = 0; i < count; i++)
            {
                // ReSharper disable CommentTypo
                // Each entry is exactly 20 bytes: "nnnnnnnnnn ggggg f/n\r\n"
                // ReSharper restore CommentTypo
                var offsetTok = lexer.ReadNext();
                var genTok = lexer.ReadNext();
                var typeTok = lexer.ReadNext();

                var entryOffset = ParseRawInteger(offsetTok.Raw.Span);
                var generation = (int)ParseRawInteger(genTok.Raw.Span);
                var entryType = typeTok.Raw.Span[0] == (byte)'f'
                    ? CrossReferenceEntryType.Free
                    : CrossReferenceEntryType.InUse;

                entries[firstObj + i] = new CrossReferenceEntry(entryOffset, generation, entryType);
            }
        }

        Expect(lexer, PdfTokenKind.Trailer);
        Expect(lexer, PdfTokenKind.DictionaryBegin);
        var trailer = ReadDictionary(lexer);
        var prev = trailer.Get<PdfInteger>(PdfName.Prev)?.Value ?? 0;

        return (entries, trailer, prev);
    }

    // Cross-reference streams (§7.5.8) — PDF 1.5+ replaces the traditional text-based
    // xref table with a compressed binary stream. The stream dictionary doubles as the
    // trailer dictionary.
    private (Dictionary<int, CrossReferenceEntry>, PdfDictionary, long) ParseXrefStream(long offset)
    {
        var xrefObj = ReadObject(offset);
        if (xrefObj.Value is not PdfStream stream)
            throw new PdfException($"Expected a cross-reference stream at offset 0x{offset:X}, found {xrefObj.Value.GetType().Name}.", offset);

        var dict = stream.Dictionary;
        var decoded = StreamFilters.Decode(stream);

        // /W — field widths: [typeWidth, offsetWidth, genWidth]
        var w = dict.Get<PdfArray>("W") ?? throw new PdfException("Cross-reference stream missing required /W entry.");
        if (w.Count != 3)
            throw new PdfException($"Cross-reference stream /W must have 3 elements, got {w.Count}.");

        var w0 = (int)((w[0] as PdfInteger)?.Value ?? 0);
        var w1 = (int)((w[1] as PdfInteger)?.Value ?? throw new PdfException("Cross-reference stream /W[1] (offset field) must not be zero."));
        var w2 = (int)((w[2] as PdfInteger)?.Value ?? 0);
        var rowSize = w0 + w1 + w2;

        // /Index — subsection ranges [first0 count0 first1 count1 ...]
        // Defaults to [0, /Size] when absent.
        var size = (int)(dict.Get<PdfInteger>(PdfName.Size)?.Value ?? throw new PdfException("Cross-reference stream missing required /Size entry."));

        var indexArray = dict.Get<PdfArray>("Index");
        var subsections = new List<(int First, int Count)>();
        if (indexArray is not null)
        {
            for (var i = 0; i + 1 < indexArray.Count; i += 2)
                subsections.Add(((int)(indexArray[i] as PdfInteger)!.Value, (int)(indexArray[i + 1] as PdfInteger)!.Value));
        }
        else
            subsections.Add((0, size));

        // Parse binary rows
        var entries = new Dictionary<int, CrossReferenceEntry>();
        var span = decoded.Span;
        var pos = 0;

        foreach (var (first, count) in subsections)
        {
            for (var i = 0; i < count; i++)
            {
                if (pos + rowSize > span.Length)
                    throw new PdfException("Cross-reference stream data is shorter than declared entry count.");

                // Type field (default 1 when w0 == 0)
                var type = w0 == 0 ? 1 : (int)ReadBigEndian(span, pos, w0);
                var field1 = ReadBigEndian(span, pos + w0, w1);
                var gen = w2 == 0 ? 0 : (int)ReadBigEndian(span, pos + w0 + w1, w2);
                pos += rowSize;

                var entryType = type switch
                {
                    0 => CrossReferenceEntryType.Free,
                    1 => CrossReferenceEntryType.InUse,
                    2 => CrossReferenceEntryType.Compressed,
                    _ => CrossReferenceEntryType.InUse // unknown types treated as in-use per spec
                };

                entries[first + i] = new CrossReferenceEntry(field1, gen, entryType);
            }
        }

        var prev = dict.Get<PdfInteger>(PdfName.Prev)?.Value ?? 0;
        return (entries, dict, prev);
    }

    // Reads a big-endian unsigned integer of <paramref name="count"/> bytes from <paramref name="span"/>.
    private static long ReadBigEndian(ReadOnlySpan<byte> span, int offset, int count)
    {
        long value = 0;
        for (var i = 0; i < count; i++)
            value = (value << 8) | span[offset + i];
        return value;
    }

    // ── Token utilities ───────────────────────────────────────────────────────

    private static long ExpectInteger(Lexer lexer)
    {
        var t = lexer.ReadNext();
        return t.Kind != PdfTokenKind.Integer
            ? throw new PdfException($"Expected integer, got {t.Kind}.", t.Offset)
            : ParseRawInteger(t.Raw.Span);
    }

    private static void Expect(Lexer lexer, PdfTokenKind kind)
    {
        var t = lexer.ReadNext();
        if (t.Kind != kind)
            throw new PdfException($"Expected {kind}, got {t.Kind}.", t.Offset);
    }

    // §7.3.8.1 — "stream" keyword must be followed by a single CR, LF, or CR+LF.
    private static void SkipNewline(Lexer lexer) => lexer.SkipLineEnding();

    // ── Raw value parsers (no string allocation) ──────────────────────────────

    private static PdfInteger ParseInteger(PdfToken token) =>
        new(ParseRawInteger(token.Raw.Span));

    private static PdfReal ParseReal(PdfToken token) =>
        new(ParseRawReal(token.Raw.Span));

    private static PdfName ParseName(PdfToken token) =>
        PdfName.Get(ParseRawName(token.Raw.Span));

    // Strips the surrounding parentheses from a literal string token.
    // NOTE: PDF escape sequences (\n, \r, \ddd etc. per §7.3.4.2) are NOT decoded here.
    // The raw bytes are preserved so that PdfWriter can round-trip them faithfully.
    // Callers that need the decoded bytes (e.g. text rendering) call GetBinaryBytes().
    private static PdfString ParseLiteralString(PdfToken token)
    {
        var inner = token.Raw.Slice(1, token.Raw.Length - 2);
        return new PdfString(inner);
    }

    // Strips the surrounding angle brackets from a hex string token.
    // The raw hex digit characters are preserved in Bytes so that PdfWriter can
    // round-trip them faithfully. Call PdfString.DecodeHexBytes() when the actual
    // binary content is needed (e.g. for text char-code decoding in the renderer).
    private static PdfString ParseHexString(PdfToken token)
    {
        var inner = token.Raw.Slice(1, token.Raw.Length - 2);
        return new PdfString(inner, true);
    }

    private static long ParseRawInteger(ReadOnlySpan<byte> span)
    {
        var negative = span[0] == (byte)'-';
        var start = (negative || span[0] == (byte)'+') ? 1 : 0;
        long value = 0;
        for (var i = start; i < span.Length; i++)
            value = (value * 10) + (span[i] - '0');

        return negative ? -value : value;
    }

    private static double ParseRawReal(ReadOnlySpan<byte> span) =>
        double.Parse(
            Encoding.Latin1.GetString(span),
            CultureInfo.InvariantCulture
        );

    // §7.3.5 — decodes a name token: strips the leading '/' and expands '#xx' escapes.
    private static string ParseRawName(ReadOnlySpan<byte> span)
    {
        var start = span[0] == (byte)'/' ? 1 : 0;
        if (!span[start..].Contains((byte)'#'))
            return Encoding.Latin1.GetString(span[start..]);

        var sb = new StringBuilder(span.Length);
        for (var i = start; i < span.Length; i++)
        {
            if (span[i] == (byte)'#' && i + 2 < span.Length)
            {
                var hi = HexNibble(span[i + 1]);
                var lo = HexNibble(span[i + 2]);
                sb.Append((char)((hi << 4) | lo));
                i += 2;
            }
            else
                sb.Append((char)span[i]);
        }

        return sb.ToString();
    }

    private static int HexNibble(byte b) => b switch
    {
        >= (byte)'0' and <= (byte)'9' => b - '0',
        >= (byte)'a' and <= (byte)'f' => b - 'a' + 10,
        >= (byte)'A' and <= (byte)'F' => b - 'A' + 10,
        _ => throw new PdfException($"Invalid hex digit 0x{b:X2}")
    };
}
