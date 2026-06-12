using Unchained.Drawing.Extensions;

namespace Unchained.Drawing.Decoders;

/// <summary>
/// Decodes ASCII hex-encoded data.
/// Used by PDF /ASCIIHexDecode (ISO 32000-1 §7.4.2).
/// </summary>
internal static class AsciiHexDecoder
{
    public static ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> data)
    {
        var span = data.Span;
        var output = new List<byte>(data.Length / 2);
        var nibble = -1;

        foreach (var b in span)
        {
            if (b == (byte)'>') break;

            int value;
            switch (b)
            {
                case >= (byte)'0' and <= (byte)'9':
                {
                    value = b - '0';
                    break;
                }
                case >= (byte)'A' and <= (byte)'F':
                {
                    value = b - 'A' + 10;
                    break;
                }
                case >= (byte)'a' and <= (byte)'f':
                {
                    value = b - 'a' + 10;
                    break;
                }
                default:
                {
                    if (b.IsWhitespace())
                        continue;

                    throw new InvalidDataException($"ASCIIHexDecode: unexpected byte 0x{b:X2}.");
                }
            }

            if (nibble < 0)
                nibble = value;
            else
            {
                output.Add((byte)((nibble << 4) | value));
                nibble = -1;
            }
        }

        if (nibble >= 0)
            output.Add((byte)(nibble << 4));

        return output.ToArray();
    }
}
