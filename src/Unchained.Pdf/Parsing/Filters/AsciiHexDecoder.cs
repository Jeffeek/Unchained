using Unchained.Pdf.Core;

namespace Unchained.Pdf.Parsing.Filters;

/// <summary>
/// Decodes PDF /ASCIIHexDecode streams (ISO 32000-1 §7.4.2).
/// Input is a sequence of hex digit pairs ('0'–'9', 'A'–'F', 'a'–'f'),
/// optionally separated by whitespace, terminated by '>'.
/// </summary>
internal static class AsciiHexDecoder
{
    public static ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> data)
    {
        var span = data.Span;
        var output = new List<byte>(data.Length / 2);
        var nibble = -1; // -1 = waiting for first nibble of a pair

        foreach (var b in span)
        {
            if (b == (byte)'>') break; // end-of-data marker

            int value;
            if (b is >= (byte)'0' and <= (byte)'9') value = b - '0';
            else if (b is >= (byte)'A' and <= (byte)'F') value = b - 'A' + 10;
            else if (b is >= (byte)'a' and <= (byte)'f') value = b - 'a' + 10;
            else if (IsWhitespace(b)) continue;
            else throw new PdfException($"ASCIIHexDecode: unexpected byte 0x{b:X2}.");

            if (nibble < 0)
            {
                nibble = value;
            }
            else
            {
                output.Add((byte)((nibble << 4) | value));
                nibble = -1;
            }
        }

        // A trailing single nibble is padded with 0 on the right (spec §7.4.2)
        if (nibble >= 0)
            output.Add((byte)(nibble << 4));

        return output.ToArray();
    }

    private static bool IsWhitespace(byte b) =>
        b is 0x00 or 0x09 or 0x0A or 0x0C or 0x0D or 0x20;
}
