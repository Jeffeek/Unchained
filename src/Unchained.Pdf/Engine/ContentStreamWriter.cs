using System.Buffers;
using System.Globalization;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Low-level PDF content stream builder. Writes operands and operator keywords
///     directly into an <see cref="ArrayBufferWriter{T}" /> without intermediate allocations.
/// </summary>
internal sealed class ContentStreamWriter(IBufferWriter<byte> buffer)
{
    // Writes "value " (float formatted with G6, followed by a space).
    internal void Float(float v)
    {
        WriteAscii(v.ToString("G6", CultureInfo.InvariantCulture));
        buffer.Write(" "u8);
    }

    // Writes "value " (integer, followed by a space).
    internal void Int(int v)
    {
        WriteAscii(v.ToString(CultureInfo.InvariantCulture));
        buffer.Write(" "u8);
    }

    // Writes "/name " including the leading slash.
    internal void Name(string name)
    {
        buffer.Write("/"u8);
        WriteAscii(name);
        buffer.Write(" "u8);
    }

    // Writes "(escaped text) " as a PDF literal string operand.
    // Non-Latin-1 characters (code point > 0xFF) are silently replaced with '?'.
    internal void LiteralString(string text)
    {
        buffer.Write("("u8);
        foreach (var b in text.Select(static ch => ch > '\xFF' ? (byte)'?' : (byte)ch))
        {
            switch (b)
            {
                case (byte)'(':
                case (byte)')':
                case (byte)'\\':
                {
                    buffer.Write("\\"u8);
                    break;
                }
            }

            buffer.GetSpan(1)[0] = b;
            buffer.Advance(1);
        }

        buffer.Write(") "u8);
    }

    // Writes "keyword\n" — the operator keyword followed by a newline.
    internal void Op(ReadOnlySpan<byte> keyword)
    {
        buffer.Write(keyword);
        buffer.Write("\n"u8);
    }

    // Writes "/tag <</MCID n>> BDC\n" — begins a marked-content sequence with an MCID property list.
    // Used for tagged PDF (ISO 32000-1 §14.6): every logical content unit is wrapped in BDC/EMC.
    // <paramref name="tag"/> must be a standard structure type name (e.g. "P", "H1", "Figure").
    // <paramref name="mcid"/> is the marked-content identifier; must be unique per page.
    internal void MarkedContentBegin(string tag, int mcid)
    {
        buffer.Write("/"u8);
        WriteAscii(tag);
        buffer.Write(" <<"u8);
        buffer.Write("/MCID "u8);
        WriteAscii(mcid.ToString(CultureInfo.InvariantCulture));
        buffer.Write(">> BDC\n"u8);
    }

    // Writes "EMC\n" — ends a marked-content sequence opened by MarkedContentBegin.
    internal void MarkedContentEnd() => buffer.Write("EMC\n"u8);

    private void WriteAscii(string s)
    {
        // ASCII range is identical to Latin-1 for printable chars; float/int strings
        // only contain ASCII digits, '.', '-', 'E', and 'G' format chars.
        var span = buffer.GetSpan(s.Length);
        for (var i = 0; i < s.Length; i++)
            span[i] = (byte)s[i];
        buffer.Advance(s.Length);
    }
}
