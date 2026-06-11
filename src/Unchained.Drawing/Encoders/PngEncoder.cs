using System.Buffers.Binary;
using System.IO.Compression;
using Unchained.Drawing.Constants;
using Unchained.Drawing.Extensions;

namespace Unchained.Drawing.Encoders;

/// <summary>
/// Encodes a <see cref="RasterBuffer"/> to PNG bytes using only BCL APIs
/// (ZLibStream for DEFLATE, CRC32 computed from a look-up table).
/// No external image library is required.
/// </summary>
internal static class PngEncoder
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    internal static byte[] Encode(RasterBuffer buffer)
    {
        using var ms = new MemoryStream((buffer.Width * buffer.Height * 4) + 256);
        ms.Write(PngSignature);
        WriteIHDR(ms, buffer.Width, buffer.Height);
        WriteIDAT(ms, buffer);
        WriteChunk(ms, PngConstants.IEND.ToUtf8Span(), ReadOnlySpan<byte>.Empty);

        return ms.ToArray();
    }

    // ReSharper disable once InconsistentNaming
    private static void WriteIHDR(Stream stream, int width, int height)
    {
        Span<byte> data = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(data[..], width);
        BinaryPrimitives.WriteInt32BigEndian(data[4..], height);
        // ReSharper disable once GrammarMistakeInComment
        data[8] = 8;   // bit depth
        data[9] = 6;   // colour type: RGBA
        data[10] = 0;  // compression method
        data[11] = 0;  // filter method
        data[12] = 0;  // interlace: none
        WriteChunk(stream, PngConstants.IHDR.ToUtf8Span(), data);
    }

    // ReSharper disable once InconsistentNaming
    private static void WriteIDAT(Stream stream, RasterBuffer buffer)
    {
        var w = buffer.Width;
        var h = buffer.Height;
        var pixels = buffer.ToArgbBytes();

        // Filtered scanline data: filter_byte(0=None) + R G B A per pixel
        var raw = new byte[h * (1 + (w * 4))];
        for (var y = 0; y < h; y++)
        {
            var outOffset = y * (1 + (w * 4));
            raw[outOffset] = 0; // filter type: None
            var srcOffset = y * w * 4;
            Buffer.BlockCopy(pixels, srcOffset, raw, outOffset + 1, w * 4);
        }

        using var compressedMs = new MemoryStream();
        using (var zlib = new ZLibStream(compressedMs, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(raw);

        WriteChunk(stream, PngConstants.IDAT.ToUtf8Span(), compressedMs.ToArray());
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> lenBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lenBuf, (uint)data.Length);
        stream.Write(lenBuf);
        stream.Write(type);
        if (data.Length > 0) stream.Write(data);

        var crc = UpdateCrc(PngConstants.Crc32Init, type);
        crc = UpdateCrc(crc, data);
        crc ^= PngConstants.Crc32Init;

        Span<byte> crcBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBuf, crc);
        stream.Write(crcBuf);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
            crc = PngConstants.CtcTable[(crc ^ b) & JpegMarkers.MarkerPrefix] ^ (crc >> 8);

        return crc;
    }
}
