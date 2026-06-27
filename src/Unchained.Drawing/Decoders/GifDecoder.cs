using System.Buffers.Binary;
using System.Collections.Generic;

namespace Unchained.Drawing.Decoders;

/// <summary>
///     Minimal GIF decoder supporting single-frame, non-interlaced images
///     with a global colour table. Transparency is mapped to black.
/// </summary>
internal static class GifDecoder
{
    internal static bool IsGif(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 6
        && bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == '8'
        && bytes[4] == '9' && bytes[5] == 'a';

    internal static byte[]? TryDecodeToRgb(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (bytes.Length < 13) return null;
        if (!IsGif(bytes)) return null;

        width = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(6));
        height = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(8));
        if (width == 0 || height == 0) return null;

        var flags = bytes[10];
        var hasGct = (flags & 0x80) != 0;
        var gctSize = 1 << ((flags & 0x07) + 1);
        var gct = hasGct ? ReadPalette(bytes.Slice(13), gctSize) : null;
        var pos = hasGct ? 13 + gctSize * 3 : 13;

        while (pos < bytes.Length)
        {
            var b = bytes[pos];
            if (b == 0x2C)
            {
                var rgb = DecodeImage(bytes, ref pos, width, height, gct);
                return rgb;
            }
            if (b == 0x3B) return null;
            if (b == 0x21) pos = SkipExtension(bytes, pos);
            else return null;
        }
        return null;
    }

    private static byte[]? DecodeImage(
        ReadOnlySpan<byte> bytes,
        ref int pos,
        int width,
        int height,
        byte[]? palette)
    {
        pos++;
        if (pos + 10 > bytes.Length) return null;

        pos += 8;
        var imgFlags = bytes[pos++];
        var hasLct = (imgFlags & 0x80) != 0;
        var lctSize = hasLct ? 1 << ((imgFlags & 0x07) + 1) : 0;

        var pal = hasLct ? ReadPalette(bytes.Slice(pos), lctSize) : palette;
        if (pal == null || pal.Length == 0) return null;
        pos += lctSize * 3;

        var minCodeSize = bytes[pos++];
        var data = ReadSubBlocks(bytes, ref pos);
        if (data == null) return null;

        // GIF LZW: code size starts at min_code_size + 1 but floored at 9.
        // The LzwDecoder defaults to earlyChange=1 which shifts threshold,
        // but GIF requires the GIF-specific variant. Decode inline.
        var pixels = DecodeGifLzw(data, width, height);
        if (pixels.Length != width * height) return null;

        var rgb = new byte[width * height * 3];
        for (var i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            if (c < pal.Length / 3)
            {
                rgb[i * 3] = pal[c * 3];
                rgb[i * 3 + 1] = pal[c * 3 + 1];
                rgb[i * 3 + 2] = pal[c * 3 + 2];
            }
            else
            {
                rgb[i * 3] = 0;
                rgb[i * 3 + 1] = 0;
                rgb[i * 3 + 2] = 0;
            }
        }
        return rgb;
    }

    private static byte[] ReadPalette(ReadOnlySpan<byte> bytes, int count)
    {
        var size = count * 3;
        return bytes.Slice(0, size).ToArray();
    }

    private static byte[]? ReadSubBlocks(ReadOnlySpan<byte> bytes, ref int pos)
    {
        var outBytes = new List<byte>();
        while (pos < bytes.Length)
        {
            var blockSize = bytes[pos++];
            if (blockSize == 0) break;
            outBytes.AddRange(bytes.Slice(pos, blockSize));
            pos += blockSize;
        }
        return outBytes.ToArray();
    }

    private static int SkipExtension(ReadOnlySpan<byte> bytes, int pos)
    {
        pos++;
        if (pos >= bytes.Length) return -1;
        pos++;
        while (pos < bytes.Length)
        {
            var blockSize = bytes[pos++];
            if (blockSize == 0) break;
            pos += blockSize;
        }
        return pos;
    }

    // GIF LZW decoder — GIF uses a minimum code size of 9 bits.
    private static byte[] DecodeGifLzw(ReadOnlySpan<byte> data, int width, int height)
    {
        const int clearCode = 256;
        const int eoiCode = 257;
        const int minCodeSize = 9;

        var prefix = new int[4096];
        var suffix = new byte[4096];
        for (var i = 0; i < 256; i++)
        {
            prefix[i] = -1;
            suffix[i] = (byte)i;
        }

        var output = new byte[width * height];
        var outPos = 0;
        var bitPos = 0;
        var codeWidth = minCodeSize;
        var nextCode = clearCode + 1;
        var lastCode = -1;

        while (true)
        {
            var code = ReadBits(data, ref bitPos, codeWidth);
            if (code < 0 || code == eoiCode) break;

            if (code == clearCode)
            {
                nextCode = clearCode + 1;
                codeWidth = minCodeSize;
                lastCode = -1;
                continue;
            }

            if (code > nextCode) break;

            if (code == nextCode && lastCode >= 0)
            {
                var root = lastCode;
                while (root >= clearCode) root = prefix[root];
                prefix[lastCode] = lastCode;
                suffix[lastCode] = suffix[root];
            }

            var sp = 0;
            var stack = new byte[4096];
            var tmp = code;
            while (tmp >= clearCode)
            {
                stack[sp++] = suffix[tmp];
                tmp = prefix[tmp];
            }
            stack[sp++] = suffix[tmp];

            for (var i = sp - 1; i >= 0; i--)
                output[outPos++] = stack[i];

            if (lastCode >= 0 && nextCode < 4096)
            {
                if (code != nextCode)
                {
                    prefix[nextCode] = lastCode;
                    suffix[nextCode] = stack[sp - 1];
                }
                nextCode++;
                if (codeWidth < 12 && nextCode >= (1 << codeWidth))
                    codeWidth++;
            }
            lastCode = code;
        }
        return output;
    }

    private static int ReadBits(ReadOnlySpan<byte> data, ref int bitPos, int count)
    {
        var result = 0;
        for (var i = 0; i < count; i++)
        {
            var byteIdx = bitPos >> 3;
            if (byteIdx >= data.Length) return -1;
            result = (result << 1) | ((data[byteIdx] >> (7 - (bitPos & 7))) & 1);
            bitPos++;
        }
        return result;
    }
}
