using Unchained.Pdf.Core;

namespace Unchained.Pdf.Parsing.Filters;

/// <summary>
/// Decodes PDF /RunLengthDecode streams (ISO 32000-1 §7.4.5).
/// Each run starts with a length byte:
/// <list type="bullet">
///   <item>0–127 — copy the next (length + 1) bytes verbatim.</item>
///   <item>129–255 — repeat the next byte (257 − length) times.</item>
///   <item>128 — end-of-data (EOD) marker; stop decoding.</item>
/// </list>
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

            if (length == 128) break; // EOD

            if (length < 128)
            {
                // Literal run: copy (length + 1) bytes verbatim
                var count = length + 1;
                if (i + count > span.Length)
                    throw new PdfException("RunLengthDecode: literal run extends past end of data.");

                for (var j = 0; j < count; j++)
                    output.Add(span[i + j]);

                i += count;
            }
            else
            {
                // Repeat run: output (257 - length) copies of the next byte
                if (i >= span.Length)
                    throw new PdfException("RunLengthDecode: repeat run has no data byte.");

                var count = 257 - length;
                var value = span[i++];

                for (var j = 0; j < count; j++)
                    output.Add(value);
            }
        }

        return output.ToArray();
    }
}
