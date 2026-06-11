using Unchained.Drawing.Extensions;

namespace Unchained.Drawing.Decoders;

/// <summary>
/// Decodes ASCII85-encoded data (base-85 encoding).
/// Used by PDF /ASCII85Decode (ISO 32000-1 §7.4.3) and PostScript.
/// </summary>
internal static class Ascii85Decoder
{
    const char exclamationMarkChar = '!';
    const char zChar = 'z';

    public static ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> data)
    {
        const int groupLength = 5;

        var span = data.Span;
        var output = new List<byte>(data.Length);
        var group = new byte[groupLength];
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

            if (b.IsWhitespace()) continue;

            switch (b)
            {
                case (byte)zChar when groupLen != 0:
                    throw new InvalidDataException($"ASCII85Decode: '{zChar}' inside a group.");
                case (byte)zChar:
                    output.AddRange([0, 0, 0, 0]);
                    continue;
                case < (byte)exclamationMarkChar or > (byte)'u':
                    throw new InvalidDataException($"ASCII85Decode: character 0x{b:X2} is out of range.");
            }

            group[groupLen++] = b;

            if (groupLen != groupLength)
                continue;

            DecodeGroup(group, groupLength, output);
            groupLen = 0;
        }

        if (groupLen <= 0)
            return output.ToArray();

        for (var j = groupLen; j < groupLength; j++)
            group[j] = (byte)'u';
        DecodeGroup(group, groupLen, output);

        return output.ToArray();
    }

    private static void DecodeGroup(IReadOnlyList<byte> group, int count, ICollection<byte> output)
    {
        const int shifteen = 24;
        const int shiftLength = 8;

        var value =
            ((uint)(group[0] - exclamationMarkChar) * 52200625u) +
            ((uint)(group[1] - exclamationMarkChar) * 614125u) +
            ((uint)(group[2] - exclamationMarkChar) * 7225u) +
            ((uint)(group[3] - exclamationMarkChar) * 85u) +
            (uint)(group[4] - exclamationMarkChar);

        var emit = count - 1;
        for (var shift = shifteen; shift >= shifteen - ((emit - 1) * shiftLength); shift -= shiftLength)
            output.Add((byte)(value >> shift));
    }
}
