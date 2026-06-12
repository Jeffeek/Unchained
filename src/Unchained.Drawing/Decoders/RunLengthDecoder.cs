namespace Unchained.Drawing.Decoders;

/// <summary>
/// Decodes PackBits / PDF run-length encoded data.
/// Used by PDF /RunLengthDecode (ISO 32000-1 §7.4.5) and Apple PackBits (BMP, TIFF, PICT).
/// Length byte semantics: 0–127 = copy next (length+1) bytes verbatim;
/// 129–255 = repeat next byte (257−length) times; 128 = end-of-data.
/// </summary>
internal static class RunLengthDecoder
{
    public static ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> data)
    {
        var span = data.Span;
        var output = new List<byte>(data.Length * 2);
        var i = 0;

        while (i < span.Length)
        {
            var length = span[i++];

            if (length == 128) break;

            if (length < 128)
            {
                var count = length + 1;
                if (i + count > span.Length)
                    throw new InvalidDataException("RunLengthDecode: literal run extends past end of data.");

                for (var j = 0; j < count; j++)
                    output.Add(span[i + j]);

                i += count;
            }
            else
            {
                if (i >= span.Length)
                    throw new InvalidDataException("RunLengthDecode: repeat run has no data byte.");

                var count = 257 - length;
                var value = span[i++];

                for (var j = 0; j < count; j++)
                    output.Add(value);
            }
        }

        return output.ToArray();
    }
}
