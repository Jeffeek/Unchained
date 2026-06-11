using System.IO.Compression;

namespace Unchained.Drawing.Decoders;

/// <summary>
/// Decodes zlib/DEFLATE-compressed data (RFC 1950 zlib wrapper).
/// Used by PDF /FlateDecode (ISO 32000-1 §7.4.4) and PNG IDAT chunks.
/// </summary>
internal static class FlateDecoder
{
    public static ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> data)
    {
        using var input = new MemoryStream(data.ToArray());
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream(capacity: data.Length * 4);
        zlib.CopyTo(output);
        return output.ToArray();
    }
}
