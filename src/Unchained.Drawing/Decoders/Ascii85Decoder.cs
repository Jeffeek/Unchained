using Unchained.Drawing.Extensions;

namespace Unchained.Drawing.Decoders;

/// <summary>
///     Decodes ASCII85-encoded data (base-85 encoding).
///     Used by PDF /ASCII85Decode (ISO 32000-1 §7.4.3) and PostScript.
/// </summary>
internal static class Ascii85Decoder
{
    private const char ExclamationMarkChar = '!';
    private const char ZChar = 'z';

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
                case (byte)ZChar when groupLen != 0:
                    throw new InvalidDataException($"ASCII85Decode: '{ZChar}' inside a group.");
                case (byte)ZChar:
                    output.AddRange([0, 0, 0, 0]);
                    continue;
                case < (byte)ExclamationMarkChar or > (byte)'u':
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
        const int msbShift = 24;
        const int shiftStep = 8;

        var value =
            ((uint)(group[0] - ExclamationMarkChar) * 52200625u) +
            ((uint)(group[1] - ExclamationMarkChar) * 614125u) +
            ((uint)(group[2] - ExclamationMarkChar) * 7225u) +
            ((uint)(group[3] - ExclamationMarkChar) * 85u) +
            (uint)(group[4] - ExclamationMarkChar);

        var emit = count - 1;
        for (var shift = msbShift; shift >= msbShift - ((emit - 1) * shiftStep); shift -= shiftStep)
            output.Add((byte)(value >> shift));
    }
}
