using System.IO.Compression;

namespace Unchained.Pdf.Parsing.Filters;

/// <summary>
/// Decodes PDF /FlateDecode streams (ISO 32000-1 §7.4.4).
/// PDF uses the zlib wrapper format (RFC 1950), not raw DEFLATE (RFC 1951).
/// <see cref="ZLibStream"/> (.NET 6+) handles the zlib header and Adler-32
/// checksum correctly. Do NOT use <see cref="DeflateStream"/> here.
/// </summary>
internal static class FlateDecoder
{
    /// <summary>Decompresses a FlateDecode-compressed byte span.</summary>
    /// <exception cref="Unchained.Pdf.Core.PdfException">
    /// Thrown when the zlib data is truncated or corrupt.
    /// </exception>
    public static ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> data)
    {
        try
        {
            using var input = new MemoryStream(data.ToArray());
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream(capacity: data.Length * 4);
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch (InvalidDataException ex)
        {
            throw new Core.PdfException("FlateDecode: corrupt or truncated zlib stream.", ex);
        }
    }
}
