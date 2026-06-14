using System.Text;

namespace Unchained.Pdf.Parsing;

/// <summary>
///     Classifies a single lexical token in a PDF byte stream (ISO 32000-1 §7.2).
/// </summary>
public enum PdfTokenKind
{
    // ── Primitives ────────────────────────────────────────────────────────────

    /// <summary>A signed decimal integer literal, e.g. <c>123</c> or <c>-7</c>.</summary>
    Integer,

    /// <summary>A real number literal, e.g. <c>3.14</c> or <c>-.002</c>.</summary>
    Real,

    /// <summary>A literal string delimited by parentheses, e.g. <c>(Hello)</c>.</summary>
    LiteralString,

    /// <summary>A hex-encoded string delimited by angle brackets, e.g. <c>&lt;4E6F&gt;</c>.</summary>
    HexString,

    /// <summary>A name token starting with <c>/</c>, e.g. <c>/Type</c>.</summary>
    Name,

    /// <summary>The keyword <c>true</c>.</summary>
    BooleanTrue,

    /// <summary>The keyword <c>false</c>.</summary>
    BooleanFalse,

    /// <summary>The keyword <c>null</c>.</summary>
    Null,

    // ── Containers ────────────────────────────────────────────────────────────

    /// <summary>The <c>&lt;&lt;</c> token that opens a dictionary.</summary>
    DictionaryBegin,

    /// <summary>The <c>&gt;&gt;</c> token that closes a dictionary.</summary>
    DictionaryEnd,

    /// <summary>The <c>[</c> token that opens an array.</summary>
    ArrayBegin,

    /// <summary>The <c>]</c> token that closes an array.</summary>
    ArrayEnd,

    // ── Streams ───────────────────────────────────────────────────────────────

    /// <summary>The <c>stream</c> keyword that begins a stream body.</summary>
    Stream,

    /// <summary>The <c>endstream</c> keyword that terminates a stream body.</summary>
    EndStream,

    // ── Indirect objects ──────────────────────────────────────────────────────

    /// <summary>The <c>obj</c> keyword that opens an indirect object definition.</summary>
    Obj,

    /// <summary>The <c>endobj</c> keyword that closes an indirect object definition.</summary>
    EndObj,

    /// <summary>
    ///     The <c>R</c> keyword that completes an indirect reference: <c>N G R</c>.
    ///     Emitted only after two consecutive <see cref="Integer" /> tokens.
    /// </summary>
    IndirectRef,

    // ── File structure keywords ───────────────────────────────────────────────

    /// <summary>The <c>xref</c> keyword that begins a cross-reference table section.</summary>
    Xref,

    /// <summary>The <c>trailer</c> keyword that precedes the trailer dictionary.</summary>
    Trailer,

    /// <summary>The <c>startxref</c> keyword followed by the xref byte offset.</summary>
    StartXref,

    // ── Meta ──────────────────────────────────────────────────────────────────

    /// <summary>A comment beginning with <c>%</c> and running to the end of the line.</summary>
    Comment,

    /// <summary>Signals that the end of the source buffer has been reached.</summary>
    EndOfFile
}

/// <summary>
///     A single lexical token produced by <see cref="Lexer" />.
///     Stored as a <see langword="struct" /> to avoid per-token heap allocation in the hot path.
///     <see cref="Raw" /> is a zero-copy slice into the original source buffer.
/// </summary>
internal readonly struct PdfToken(PdfTokenKind kind, ReadOnlyMemory<byte> raw, long offset)
{
    /// <summary>The kind of this token.</summary>
    public PdfTokenKind Kind { get; } = kind;

    /// <summary>
    ///     The raw bytes of this token as a slice into the source buffer.
    ///     No allocation; the memory is valid as long as the source buffer is alive.
    /// </summary>
    public ReadOnlyMemory<byte> Raw { get; } = raw;

    /// <summary>
    ///     The absolute byte offset from the start of the source buffer where this token begins.
    ///     Used to attach location information to <see cref="Unchained.Pdf.Core.PdfException" />.
    /// </summary>
    public long Offset { get; } = offset;

    /// <summary>Returns <see langword="true" /> if this token's kind matches <paramref name="kind" />.</summary>
    public bool Is(PdfTokenKind kind) => Kind == kind;

    /// <summary>
    ///     Returns a diagnostic string in the form <c>Kind @ 0xOFFSET "raw-text"</c>.
    /// </summary>
    public override string ToString() =>
        $"{Kind} @ 0x{Offset:X} \"{Encoding.Latin1.GetString(Raw.Span)}\"";
}
