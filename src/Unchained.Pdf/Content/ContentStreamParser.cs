using System.Text;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing;

namespace Unchained.Pdf.Content;

/// <summary>
/// Parses a decoded PDF content stream into a flat sequence of
/// <see cref="ContentOperator"/> records (ISO 32000-1 §7.8.2).
/// <para>
/// A content stream is a sequence of operand values followed by an operator keyword.
/// Operands accumulate in a stack until an operator is encountered, at which point
/// the operator claims them all and the stack is cleared.
/// </para>
/// <para>
/// Inline images (<c>BI</c>…<c>EI</c>) are detected and skipped; their binary
/// payload cannot be safely tokenized with the general lexer.
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
        // PdfParser.ReadValue handles nested arrays/dictionaries in operands.
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
                    operands.Add(parser.ReadValue(lexer));
                    break;

                case PdfTokenKind.Name:
                {
                    var raw = peek.Raw.Span;
                    if (raw.Length > 0 && raw[0] == (byte)'/')
                    {
                        // Starts with '/' → PDF name operand (e.g. /Helvetica)
                        operands.Add(parser.ReadValue(lexer));
                    }
                    else
                    {
                        // No leading '/' → operator keyword
                        lexer.ReadNext();
                        var opName = Encoding.Latin1.GetString(raw);

                        // Inline images require special handling — skip binary payload.
                        if (opName == "ID")
                        {
                            SkipInlineImageData(lexer, data);
                            operands.Clear();
                            break;
                        }

                        operators.Add(new ContentOperator(opName, operands.ToArray()));
                        operands.Clear();
                    }
                    break;
                }

                default:
                    lexer.ReadNext(); // skip unexpected tokens gracefully
                    break;
            }
        }

        return operators;
    }

    // ── Inline image handling ─────────────────────────────────────────────────

    // After consuming the `ID` keyword, raw binary image data follows until `EI`,
    // which must be preceded by a whitespace character (§7.4.9).
    // We scan the raw bytes rather than the tokenizer to safely cross binary content.
    private static void SkipInlineImageData(Lexer lexer, ReadOnlyMemory<byte> source)
    {
        var span = source.Span;
        var pos = lexer.Position;

        while (pos < span.Length - 2)
        {
            // EI must be preceded by whitespace (NUL, TAB, LF, FF, CR, or SPACE).
            if (IsWhitespace(span[pos])
                && span[pos + 1] == (byte)'E'
                && span[pos + 2] == (byte)'I')
            {
                lexer.Seek(pos + 3); // skip past EI
                return;
            }
            pos++;
        }

        lexer.Seek(span.Length); // EI not found — advance to end
    }

    private static bool IsWhitespace(byte b) =>
        b is 0x00 or 0x09 or 0x0A or 0x0C or 0x0D or 0x20;
}
