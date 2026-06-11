using System.IO;

namespace Unchained.Drawing.Decoders;

/// <summary>
/// Decodes LZW-compressed data.
/// Used by PDF /LZWDecode (ISO 32000-1 §7.4.4) and TIFF LZW compression.
/// Implements TIFF-compatible LZW: MSB-first variable-length codes,
/// initial code width 9 bits, Clear=256, EOD=257.
/// </summary>
internal static class LzwDecoder
{
    private const int ClearCode = 256;
    private const int EodCode = 257;
    private const int FirstDynamic = 258;
    private const int MaxTableSize = 4096;

    /// <param name="data">The LZW-compressed bytes.</param>
    /// <param name="earlyChange">
    /// 1 (default, TIFF/PDF) = increase code width one entry earlier.
    /// 0 = increase at the exact power-of-2 boundary.
    /// </param>
    internal static ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> data, int earlyChange = 1)
    {
        var input = data.Span;
        var output = new MemoryStream(Math.Max(64, data.Length * 3));

        var prefix = new int[MaxTableSize];
        var suffix = new byte[MaxTableSize];
        for (var i = 0; i < 256; i++)
        {
            prefix[i] = -1;
            suffix[i] = (byte)i;
        }

        int nextCode = FirstDynamic, codeWidth = 9, bitPos = 0, lastCode = -1;
        var stack = new byte[MaxTableSize];

        while (true)
        {
            var code = ReadBits(input, ref bitPos, codeWidth);
            if (code is < 0 or EodCode)
                break;

            if (code == ClearCode)
            {
                nextCode = FirstDynamic;
                codeWidth = 9;
                lastCode = -1;
                continue;
            }

            if (code > nextCode)
                throw new InvalidDataException($"LZWDecode: invalid code {code} (nextCode={nextCode}).");

            if (code == nextCode && lastCode >= 0)
            {
                var root = lastCode;
                while (root >= FirstDynamic) root = prefix[root];
                prefix[nextCode] = lastCode;
                suffix[nextCode] = suffix[root];
            }

            var sp = 0;
            var tmp = code;
            while (tmp >= FirstDynamic)
            {
                stack[sp++] = suffix[tmp];
                tmp = prefix[tmp];
            }

            stack[sp++] = suffix[tmp];

            for (var i = sp - 1; i >= 0; i--)
                output.WriteByte(stack[i]);

            if (lastCode >= 0 && nextCode < MaxTableSize)
            {
                if (code != nextCode)
                {
                    prefix[nextCode] = lastCode;
                    suffix[nextCode] = stack[sp - 1];
                }

                nextCode++;

                if (codeWidth < 12)
                {
                    var threshold = (1 << codeWidth) - (earlyChange != 0 ? 1 : 0);
                    if (nextCode >= threshold)
                        codeWidth++;
                }
            }

            lastCode = code;
        }

        return output.ToArray();
    }

    private static int ReadBits(ReadOnlySpan<byte> data, ref int bitPos, int count)
    {
        var result = 0;
        for (var i = 0; i < count; i++)
        {
            var byteIdx = bitPos >> 3;
            if (byteIdx >= data.Length)
                return -1;

            result = (result << 1) | ((data[byteIdx] >> (7 - (bitPos & 7))) & 1);
            bitPos++;
        }

        return result;
    }
}
