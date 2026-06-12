using System.Buffers.Binary;

namespace Unchained.Drawing.Encoders;

/// <summary>
/// Encodes a <see cref="RasterBuffer"/> to an uncompressed 24-bit BMP using only BCL APIs.
/// The buffer stores pixels as RGBA (4 bytes per pixel); BMP stores them as BGR rows written
/// bottom-up with each row padded to a 4-byte boundary.
/// </summary>
internal static class BmpEncoder
{
    private const int FileHeaderSize = 14;
    private const int InfoHeaderSize = 40;

    internal static byte[] Encode(RasterBuffer buffer)
    {
        var width = buffer.Width;
        var height = buffer.Height;
        var source = buffer.ToArgbBytes();

        var rowStride = (width * 3 + 3) & ~3;
        var pixelDataSize = rowStride * height;
        var fileSize = FileHeaderSize + InfoHeaderSize + pixelDataSize;

        var output = new byte[fileSize];

        // ── BITMAPFILEHEADER ──
        output[0] = (byte)'B';
        output[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(2), fileSize);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(10), FileHeaderSize + InfoHeaderSize);

        // ── BITMAPINFOHEADER ──
        var info = output.AsSpan(FileHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(info, InfoHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(info[4..], width);
        BinaryPrimitives.WriteInt32LittleEndian(info[8..], height);
        BinaryPrimitives.WriteInt16LittleEndian(info[12..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(info[14..], 24);
        BinaryPrimitives.WriteInt32LittleEndian(info[20..], pixelDataSize);

        // ── Pixel data (bottom-up, BGR) ──
        var dataOffset = FileHeaderSize + InfoHeaderSize;
        for (var y = 0; y < height; y++)
        {
            var srcRow = (height - 1 - y) * width * 4;
            var destRow = dataOffset + y * rowStride;
            for (var x = 0; x < width; x++)
            {
                var src = srcRow + x * 4;
                var dest = destRow + x * 3;
                output[dest]     = source[src + 2]; // B
                output[dest + 1] = source[src + 1]; // G
                output[dest + 2] = source[src];     // R
            }
        }

        return output;
    }
}
