using System.IO;

namespace Unchained.Drawing;

/// <summary>
/// Decodes ASCII85-encoded data (base-85 encoding).
/// Used by PDF /ASCII85Decode (ISO 32000-1 §7.4.3) and PostScript.
/// </summary>
internal static class Ascii85Decoder
{
    public static ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> data)
    {
        var span = data.Span;
        var output = new List<byte>(data.Length);
        var group = new byte[5];
        var groupLen = 0;

        for (var i = 0; i < span.Length; i++)
        {
            var b = span[i];

            // End-of-data marker '~>'
            if (b == (byte)'~')
            {
                if (i + 1 < span.Length && span[i + 1] == (byte)'>')
                    break;

                throw new InvalidDataException("ASCII85Decode: unexpected '~' not followed by '>'.");
            }

            if (IsWhitespace(b)) continue;

            switch (b)
            {
                case (byte)'z' when groupLen != 0:
                    throw new InvalidDataException("ASCII85Decode: 'z' inside a group.");
                case (byte)'z':
                    output.AddRange([0, 0, 0, 0]);
                    continue;
                case < (byte)'!' or > (byte)'u':
                    throw new InvalidDataException($"ASCII85Decode: character 0x{b:X2} is out of range.");
            }

            group[groupLen++] = b;

            if (groupLen != 5)
                continue;

            DecodeGroup(group, 5, output);
            groupLen = 0;
        }

        if (groupLen <= 0)
            return output.ToArray();

        for (var j = groupLen; j < 5; j++)
            group[j] = (byte)'u';
        DecodeGroup(group, groupLen, output);

        return output.ToArray();
    }

    private static void DecodeGroup(IReadOnlyList<byte> group, int count, ICollection<byte> output)
    {
        var value =
            ((uint)(group[0] - '!') * 52200625u) +
            ((uint)(group[1] - '!') * 614125u) +
            ((uint)(group[2] - '!') * 7225u) +
            ((uint)(group[3] - '!') * 85u) +
            (uint)(group[4] - '!');

        var emit = count - 1;
        for (var shift = 24; shift >= 24 - ((emit - 1) * 8); shift -= 8)
            output.Add((byte)(value >> shift));
    }

    private static bool IsWhitespace(byte b) =>
        b is 0x00 or 0x09 or 0x0A or 0x0C or 0x0D or 0x20;
}
