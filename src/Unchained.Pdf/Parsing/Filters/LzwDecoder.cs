using Unchained.Pdf.Core;

namespace Unchained.Pdf.Parsing.Filters;

/// <summary>
/// Decodes LZW-compressed data (LZWDecode filter, ISO 32000-1 §7.4.4).
/// Implements TIFF-compatible LZW: MSB-first variable-length codes,
/// initial code width 9 bits, Clear=256, EOD=257.
/// </summary>
internal static class LzwDecoder
{
    private const int ClearCode = 256;
    private const int EodCode = 257;
    private const int FirstDynamic = 258;
    private const int MaxTableSize = 4096;

    /// <summary>
    /// Decodes <paramref name="data"/> using LZW decompression.
    /// </summary>
    /// <param name="data">The LZW-compressed data.</param>
    /// <param name="parms">Optional /DecodeParms dictionary; reads <c>EarlyChange</c> (default 1).</param>
    internal static ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> data, PdfDictionary? parms)
    {
        // EarlyChange=1 (TIFF default): increase code width one entry earlier than strictly needed.
        var earlyChange = (int)(parms?.Get<PdfInteger>("EarlyChange")?.Value ?? 1L);

        var input = data.Span;
        var output = new MemoryStream(Math.Max(64, data.Length * 3));

        // String table — each entry is (prefix code | -1 if literal, suffix byte).
        // Entries 0–255: single literal bytes. Entries 258+: dynamically built.
        var prefix = new int[MaxTableSize];
        var suffix = new byte[MaxTableSize];
        for (var i = 0; i < 256; i++)
        {
            prefix[i] = -1;
            suffix[i] = (byte)i;
        }

        int nextCode = FirstDynamic, codeWidth = 9, bitPos = 0, lastCode = -1;
        var stack = new byte[MaxTableSize]; // decode buffer (reversed string)

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
                throw new PdfException($"LZWDecode: invalid code {code} (nextCode={nextCode}).");

            // Special case: code == nextCode, entry not yet in table.
            // Entry = prevEntry + firstChar(prevEntry).  Build it now so we can decode.
            if (code == nextCode && lastCode >= 0)
            {
                // Find first char of lastCode's decoded string (follow chain to root).
                var root = lastCode;
                while (root >= FirstDynamic) root = prefix[root];
                prefix[nextCode] = lastCode;
                suffix[nextCode] = suffix[root]; // first char of lastCode's string
            }

            // Decode: follow prefix chain into stack (reversed), then write forward.
            var sp = 0;
            var tmp = code;
            while (tmp >= FirstDynamic)
            {
                stack[sp++] = suffix[tmp];
                tmp = prefix[tmp];
            }

            stack[sp++] = suffix[tmp]; // literal byte at chain root

            // stack[sp-1] = first char of decoded string; write reversed.
            for (var i = sp - 1; i >= 0; i--)
                output.WriteByte(stack[i]);

            // Add new table entry using (lastCode, firstChar(currentDecoded)).
            if (lastCode >= 0 && nextCode < MaxTableSize)
            {
                if (code != nextCode) // normal case; special case already filled above
                {
                    prefix[nextCode] = lastCode;
                    suffix[nextCode] = stack[sp - 1]; // stack[sp-1] = first char
                }

                nextCode++;

                // Increase code width at the EarlyChange-determined threshold.
                if (codeWidth < 12)
                {
                    // EarlyChange=1: switch at 2^w-1; EarlyChange=0: switch at 2^w.
                    var threshold = (1 << codeWidth) - (earlyChange != 0 ? 1 : 0);
                    if (nextCode >= threshold)
                        codeWidth++;
                }
            }

            lastCode = code;
        }

        return output.ToArray();
    }

    // Reads `count` bits from `data` at `bitPos`, MSB first. Returns -1 at end of data.
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
