using Unchained.Drawing.Primitives.Extensions;
using Unchained.Pdf.Core;

namespace Unchained.Pdf.Parsing;

/// <summary>
///     Tokenizes a PDF byte stream into a sequence of <see cref="PdfToken" /> values
///     (ISO 32000-1 §7.2 — Lexical Conventions).
///     <para>
///         The lexer works directly on a <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;byte&gt;</see>
///         to avoid encoding overhead and allocates no heap objects for fixed-size tokens.
///         String and stream data are returned as zero-copy slices into the source buffer.
///     </para>
///     <para>
///         The lexer is forward-only by default, but the cursor can be repositioned with
///         <see cref="Seek" /> to support the backwards-reading strategy required by the
///         PDF cross-reference table (<c>startxref</c> at end of file → xref offset → objects).
///     </para>
/// </summary>
internal sealed class Lexer(ReadOnlyMemory<byte> source, int startPosition = 0)
{
    /// <summary>The current byte offset within the source buffer.</summary>
    public int Position { get; private set; } = startPosition;

    /// <summary>
    ///     <see langword="true" /> when <see cref="Position" /> has reached or passed
    ///     the end of the source buffer.
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    public bool AtEnd => Position >= source.Length;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    ///     Reads and returns the next token, advancing <see cref="Position" /> past it.
    ///     Leading whitespace and comments are skipped automatically.
    ///     Returns a token of kind <see cref="PdfTokenKind.EndOfFile" /> when the
    ///     source is exhausted.
    /// </summary>
    public PdfToken ReadNext()
    {
        SkipWhitespaceAndComments();
        if (AtEnd)
            return new PdfToken(PdfTokenKind.EndOfFile, ReadOnlyMemory<byte>.Empty, Position);

        var b = Current();
        return b switch
        {
            (byte)'/' => ReadName(),
            (byte)'(' => ReadLiteralString(),
            (byte)'<' => PeekByte(1) == (byte)'<' ? ReadKeywordToken(PdfTokenKind.DictionaryBegin, 2) : ReadHexString(),
            (byte)'>' => PeekByte(1) == (byte)'>'
                ? ReadKeywordToken(PdfTokenKind.DictionaryEnd, 2)
                : throw new PdfException("Unexpected '>'", Position),
            (byte)'[' => ReadKeywordToken(PdfTokenKind.ArrayBegin, 1),
            (byte)']' => ReadKeywordToken(PdfTokenKind.ArrayEnd, 1),
            _ when IsDigit(b) || b == (byte)'-' || b == (byte)'+' || b == (byte)'.' => ReadNumber(),
            _ => ReadKeyword()
        };
    }

    /// <summary>
    ///     Returns the next token without advancing <see cref="Position" />.
    ///     Useful for one-token lookahead in the parser.
    /// </summary>
    public PdfToken Peek()
    {
        var saved = Position;
        var token = ReadNext();
        Position = saved;
        return token;
    }

    /// <summary>
    ///     Repositions the cursor to an absolute byte <paramref name="offset" /> within the source buffer.
    ///     Used by <see cref="PdfParser" /> to jump to cross-reference offsets and to backtrack
    ///     during indirect-reference disambiguation.
    /// </summary>
    public void Seek(int offset) => Position = offset;

    /// <summary>
    ///     Skips a CR, LF, or CR+LF sequence at the current position without consuming a full token.
    ///     Required after the <c>stream</c> keyword (ISO 32000-1 §7.3.8.1) to position the cursor
    ///     at the first byte of stream data.
    /// </summary>
    public void SkipLineEnding()
    {
        if (!AtEnd && source.Span[Position] == (byte)'\r') Position++;
        if (!AtEnd && source.Span[Position] == (byte)'\n') Position++;
    }

    // ── Private readers ───────────────────────────────────────────────────────

    private void SkipWhitespaceAndComments()
    {
        while (!AtEnd)
        {
            var b = Current();
            if (b.IsWhitespace())
                Advance();
            else if (b == (byte)'%')
            {
                // Comment — skip to end of line
                while (!AtEnd && Current() != (byte)'\r' && Current() != (byte)'\n')
                    Advance();
            }
            else
                break;
        }
    }

    private PdfToken ReadName()
    {
        var start = Position;
        Advance(); // consume '/'
        while (!AtEnd && !IsDelimiter(Current()) && !Current().IsWhitespace())
            Advance();
        return new PdfToken(PdfTokenKind.Name, Slice(start, Position), start);
    }

    private PdfToken ReadLiteralString()
    {
        var start = Position;
        Advance(); // consume '('
        var depth = 1;
        while (!AtEnd && depth > 0)
        {
            var b = Current();
            switch (b)
            {
                case (byte)'\\':
                    Advance(); // escaped — skip next byte
                break;
                case (byte)'(':
                    depth++;
                break;
                case (byte)')':
                    depth--;
                break;
            }

            Advance();
        }

        return new PdfToken(PdfTokenKind.LiteralString, Slice(start, Position), start);
    }

    private PdfToken ReadHexString()
    {
        var start = Position;
        Advance(); // consume '<'
        while (!AtEnd && Current() != (byte)'>')
            Advance();

        if (!AtEnd) Advance(); // consume '>'
        return new PdfToken(PdfTokenKind.HexString, Slice(start, Position), start);
    }

    private PdfToken ReadNumber()
    {
        var start = Position;
        var isReal = false;
        if (Current() is (byte)'-' or (byte)'+') Advance();
        while (!AtEnd && (IsDigit(Current()) || Current() == (byte)'.'))
        {
            if (Current() == (byte)'.') isReal = true;
            Advance();
        }

        return new PdfToken(
            isReal ? PdfTokenKind.Real : PdfTokenKind.Integer,
            Slice(start, Position),
            start
        );
    }

    private PdfToken ReadKeyword()
    {
        var start = Position;
        while (!AtEnd && !Current().IsWhitespace() && !IsDelimiter(Current()))
            Advance();

        var raw = Slice(start, Position);
        var kind = MatchKeyword(raw.Span);
        return new PdfToken(kind, raw, start);
    }

    private PdfToken ReadKeywordToken(PdfTokenKind kind, int length)
    {
        var start = Position;
        Position += length;
        return new PdfToken(kind, Slice(start, Position), start);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private byte Current() => source.Span[Position];

    private byte PeekByte(int offset) =>
        Position + offset < source.Length ? source.Span[Position + offset] : (byte)0;

    private void Advance() => Position++;

    private ReadOnlyMemory<byte> Slice(int start, int end) =>
        source.Slice(start, end - start);

    // §7.2.2 — whitespace characters: NUL, TAB, LF, FF, CR, SPACE

    private static bool IsDigit(byte b) => b is >= (byte)'0' and <= (byte)'9';

    // §7.2.2 Table 1 — delimiter characters
    private static bool IsDelimiter(byte b) =>
        b is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>'
            or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}'
            or (byte)'/' or (byte)'%';

    private static PdfTokenKind MatchKeyword(ReadOnlySpan<byte> raw)
    {
        // Compare against known keywords — no string allocation.
        if (raw.SequenceEqual("true"u8)) return PdfTokenKind.BooleanTrue;
        if (raw.SequenceEqual("false"u8)) return PdfTokenKind.BooleanFalse;
        if (raw.SequenceEqual("null"u8)) return PdfTokenKind.Null;
        if (raw.SequenceEqual("obj"u8)) return PdfTokenKind.Obj;
        if (raw.SequenceEqual("endobj"u8)) return PdfTokenKind.EndObj;
        if (raw.SequenceEqual("stream"u8)) return PdfTokenKind.Stream;
        if (raw.SequenceEqual("endstream"u8)) return PdfTokenKind.EndStream;
        if (raw.SequenceEqual("R"u8)) return PdfTokenKind.IndirectRef;
        if (raw.SequenceEqual("xref"u8)) return PdfTokenKind.Xref;
        if (raw.SequenceEqual("trailer"u8)) return PdfTokenKind.Trailer;
        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (raw.SequenceEqual("startxref"u8)) return PdfTokenKind.StartXref;

        // Unknown keyword — treat as a bare name-like token; parser will reject if invalid.
        return PdfTokenKind.Name;
    }
}
