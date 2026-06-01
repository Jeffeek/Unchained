using System.Buffers.Binary;
using System.IO.Compression;

namespace Unchained.Pdf.Rendering.Rendering;

/// <summary>
/// Encodes a <see cref="RasterBuffer"/> to PNG bytes using only BCL APIs
/// (ZLibStream for DEFLATE, CRC32 computed from a look-up table).
/// </summary>
internal static class PdfPngEncoder
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] Crc32Table = BuildCrcTable();

    internal static byte[] Encode(RasterBuffer buffer)
    {
        using var ms = new MemoryStream(buffer.Width * buffer.Height * 4 + 256);
        ms.Write(PngSignature);
        WriteIhdr(ms, buffer.Width, buffer.Height);
        WriteIdat(ms, buffer);
        WriteChunk(ms, "IEND"u8, ReadOnlySpan<byte>.Empty);
        return ms.ToArray();
    }

    private static void WriteIhdr(Stream stream, int width, int height)
    {
        Span<byte> data = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(data[0..], width);
        BinaryPrimitives.WriteInt32BigEndian(data[4..], height);
        data[8] = 8;   // bit depth
        data[9] = 6;   // colour type: RGBA
        data[10] = 0;  // compression method
        data[11] = 0;  // filter method
        data[12] = 0;  // interlace: none
        WriteChunk(stream, "IHDR"u8, data);
    }

    private static void WriteIdat(Stream stream, RasterBuffer buffer)
    {
        var w = buffer.Width;
        var h = buffer.Height;
        var pixels = buffer.ToArgbBytes();

        // Build filtered scanline data: filter_byte(0=None) + R G B A per pixel
        var raw = new byte[h * (1 + w * 4)];
        for (var y = 0; y < h; y++)
        {
            var outOffset = y * (1 + w * 4);
            raw[outOffset] = 0; // filter type: None
            var srcOffset = y * w * 4;
            Buffer.BlockCopy(pixels, srcOffset, raw, outOffset + 1, w * 4);
        }

        // Compress with ZLib
        using var compressedMs = new MemoryStream();
        using (var zlib = new ZLibStream(compressedMs, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(raw);

        WriteChunk(stream, "IDAT"u8, compressedMs.ToArray());
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> lenBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lenBuf, (uint)data.Length);
        stream.Write(lenBuf);
        stream.Write(type);
        if (data.Length > 0) stream.Write(data);

        // CRC32 over type + data
        var crc = UpdateCrc(0xffffffff, type);
        crc = UpdateCrc(crc, data);
        crc ^= 0xffffffff;

        Span<byte> crcBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBuf, crc);
        stream.Write(crcBuf);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (var n = 0u; n < 256u; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
