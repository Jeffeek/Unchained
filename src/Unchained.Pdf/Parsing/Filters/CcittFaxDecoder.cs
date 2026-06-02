using System.Diagnostics.CodeAnalysis;
using Unchained.Pdf.Core;

namespace Unchained.Pdf.Parsing.Filters;

/// <summary>
/// Decodes CCITT facsimile-compressed data (CCITTFaxDecode filter, ISO 32000-1 §7.4.6).
/// Supports Group 3 1D (K=0) and Group 4 (K=-1, T.6 pure 2D).
/// Output: packed-bit rows, MSB first, 1 bit per pixel.
/// </summary>
internal static class CcittFaxDecoder
{
    // ── T.4 / T.6 run-length Huffman tables ─────────────────────────────────
    // Each entry: (code_length, code_value_msb_aligned_to_13_bits, run_length)
    // Negative run_length = makeup code (add to accumulator, then read another code).
    //
    // Source: ITU-T T.4 Table 2 (white) and Table 3 (black), T.6 §2.
    //
    // Encoding: code stored as the top code_length bits of an int16,
    // i.e. code_value << (13 - code_length).
    // The 13-bit lookup table indexes the top 13 bits of the current bit stream.

    // White terminating codes — run lengths 0–63
    private static readonly (int L, ushort V, int Run)[] WhiteTermRaw =
    [
        (8, 0b00110101_00000, 0), (6, 0b000111_0000000, 1), (4, 0b0111_000000000, 2),
        (4, 0b1000_000000000, 3), (4, 0b1011_000000000, 4), (4, 0b1100_000000000, 5),
        (4, 0b1110_000000000, 6), (4, 0b1111_000000000, 7), (5, 0b10011_00000000, 8),
        (5, 0b10100_00000000, 9), (5, 0b00111_00000000, 10), (5, 0b01000_00000000, 11),
        (6, 0b001000_0000000, 12), (6, 0b000011_0000000, 13), (6, 0b110100_0000000, 14),
        (6, 0b110101_0000000, 15), (6, 0b101010_0000000, 16), (6, 0b101011_0000000, 17),
        (7, 0b0100111_000000, 18), (7, 0b0001100_000000, 19), (7, 0b0001000_000000, 20),
        (7, 0b0010111_000000, 21), (7, 0b0000011_000000, 22), (7, 0b0000100_000000, 23),
        (7, 0b0101000_000000, 24), (7, 0b0101011_000000, 25), (7, 0b0010011_000000, 26),
        (7, 0b0100100_000000, 27), (7, 0b0011000_000000, 28), (8, 0b00000010_00000, 29),
        (8, 0b00000011_00000, 30), (8, 0b00011010_00000, 31), (8, 0b00011011_00000, 32),
        (8, 0b00010010_00000, 33), (8, 0b00010011_00000, 34), (8, 0b00010100_00000, 35),
        (8, 0b00010101_00000, 36), (8, 0b00010110_00000, 37), (8, 0b00010111_00000, 38),
        (8, 0b00101000_00000, 39), (8, 0b00101001_00000, 40), (8, 0b00101010_00000, 41),
        (8, 0b00101011_00000, 42), (8, 0b00101100_00000, 43), (8, 0b00101101_00000, 44),
        (8, 0b00000100_00000, 45), (8, 0b00000101_00000, 46), (8, 0b00001010_00000, 47),
        (8, 0b00001011_00000, 48), (8, 0b01010010_00000, 49), (8, 0b01010011_00000, 50),
        (8, 0b01010100_00000, 51), (8, 0b01010101_00000, 52), (8, 0b00100100_00000, 53),
        (8, 0b00100101_00000, 54), (8, 0b01011000_00000, 55), (8, 0b01011001_00000, 56),
        (8, 0b01011010_00000, 57), (8, 0b01011011_00000, 58), (8, 0b01001010_00000, 59),
        (8, 0b01001011_00000, 60), (8, 0b00110010_00000, 61), (8, 0b00110011_00000, 62),
        (8, 0b00110100_00000, 63)
    ];

    // White makeup codes — run lengths 64–1728 (multiples of 64) + extended 1792–2560
    private static readonly (int L, ushort V, int Run)[] WhiteMakeupRaw =
    [
        (5, 0b11011_00000000, 64), (5, 0b10010_00000000, 128),
        (6, 0b010111_0000000, 192), (7, 0b0110111_000000, 256),
        (8, 0b00110110_00000, 320), (8, 0b00110111_00000, 384),
        (8, 0b01100100_00000, 448), (8, 0b01100101_00000, 512),
        (8, 0b01101000_00000, 576), (8, 0b01100111_00000, 640),
        (9, 0b011001100_0000, 704), (9, 0b011001101_0000, 768),
        (9, 0b011010010_0000, 832), (9, 0b011010011_0000, 896),
        (9, 0b011010100_0000, 960), (9, 0b011010101_0000, 1024),
        (9, 0b011010110_0000, 1088), (9, 0b011010111_0000, 1152),
        (9, 0b011011000_0000, 1216), (9, 0b011011001_0000, 1280),
        (9, 0b011011010_0000, 1344), (9, 0b011011011_0000, 1408),
        (9, 0b010011000_0000, 1472), (9, 0b010011001_0000, 1536),
        (9, 0b010011010_0000, 1600), (6, 0b011000_0000000, 1664),
        (9, 0b010011011_0000, 1728),
        // Extended makeup (shared with black)
        (11, 0b00000001000_00, 1792), (11, 0b00000001100_00, 1856),
        (11, 0b00000001101_00, 1920), (12, 0b000000010010_0, 1984),
        (12, 0b000000010011_0, 2048), (12, 0b000000010100_0, 2112),
        (12, 0b000000010101_0, 2176), (12, 0b000000010110_0, 2240),
        (12, 0b000000010111_0, 2304), (12, 0b000000011100_0, 2368),
        (12, 0b000000011101_0, 2432), (12, 0b000000011110_0, 2496),
        (12, 0b000000011111_0, 2560)
    ];

    // Black terminating codes — run lengths 0–63
    private static readonly (int L, ushort V, int Run)[] BlackTermRaw =
    [
        (10, 0b0000110111_000, 0), (3, 0b010_0000000000, 1), (2, 0b11_00000000000, 2),
        (2, 0b10_00000000000, 3), (3, 0b011_0000000000, 4), (4, 0b0011_000000000, 5),
        (4, 0b0010_000000000, 6), (5, 0b00011_00000000, 7), (6, 0b000101_0000000, 8),
        (6, 0b000100_0000000, 9), (7, 0b0000100_000000, 10), (7, 0b0000101_000000, 11),
        (7, 0b0000111_000000, 12), (8, 0b00000100_00000, 13), (8, 0b00000111_00000, 14),
        (9, 0b000011000_0000, 15), (10, 0b0000010111_000, 16), (10, 0b0000011000_000, 17),
        (10, 0b0000001000_000, 18), (11, 0b00001100111_00, 19), (11, 0b00001101000_00, 20),
        (11, 0b00001101100_00, 21), (11, 0b00000110111_00, 22), (11, 0b00000101000_00, 23),
        (11, 0b00000010111_00, 24), (11, 0b00000011000_00, 25), (12, 0b000011001010_0, 26),
        (12, 0b000011001011_0, 27), (12, 0b000011001100_0, 28), (12, 0b000011001101_0, 29),
        (12, 0b000001101000_0, 30), (12, 0b000001101001_0, 31), (12, 0b000001101010_0, 32),
        (12, 0b000001101011_0, 33), (12, 0b000011010010_0, 34), (12, 0b000011010011_0, 35),
        (12, 0b000011010100_0, 36), (12, 0b000011010101_0, 37), (12, 0b000011010110_0, 38),
        (12, 0b000011010111_0, 39), (12, 0b000001101100_0, 40), (12, 0b000001101101_0, 41),
        (12, 0b000011011010_0, 42), (12, 0b000011011011_0, 43), (12, 0b000001010100_0, 44),
        (12, 0b000001010101_0, 45), (12, 0b000001010110_0, 46), (12, 0b000001010111_0, 47),
        (12, 0b000001100100_0, 48), (12, 0b000001100101_0, 49), (12, 0b000001010010_0, 50),
        (12, 0b000001010011_0, 51), (12, 0b000000100100_0, 52), (12, 0b000000110111_0, 53),
        (12, 0b000000111000_0, 54), (12, 0b000000100111_0, 55), (12, 0b000000101000_0, 56),
        (12, 0b000001011000_0, 57), (12, 0b000001011001_0, 58), (12, 0b000000101011_0, 59),
        (12, 0b000000101100_0, 60), (12, 0b000001011010_0, 61), (12, 0b000001100110_0, 62),
        (12, 0b000001100111_0, 63)
    ];

    // Black makeup codes — run lengths 64–1728 + extended 1792–2560
    private static readonly (int L, ushort V, int Run)[] BlackMakeupRaw =
    [
        (10, 0b0000001111_000, 64), (12, 0b000011001000_0, 128),
        (12, 0b000011001001_0, 192), (12, 0b000001011011_0, 256),
        (12, 0b000000110011_0, 320), (12, 0b000000110100_0, 384),
        (12, 0b000000110101_0, 448), (13, 0b0000001101100, 512),
        (13, 0b0000001101101, 576), (13, 0b0000001001010, 640),
        (13, 0b0000001001011, 704), (13, 0b0000001001100, 768),
        (13, 0b0000001001101, 832), (13, 0b0000001110010, 896),
        (13, 0b0000001110011, 960), (13, 0b0000001110100, 1024),
        (13, 0b0000001110101, 1088), (13, 0b0000001110110, 1152),
        (13, 0b0000001110111, 1216), (13, 0b0000001010010, 1280),
        (13, 0b0000001010011, 1344), (13, 0b0000001010100, 1408),
        (13, 0b0000001010101, 1472), (13, 0b0000001011010, 1536),
        (13, 0b0000001011011, 1600), (13, 0b0000001100100, 1664),
        (13, 0b0000001100101, 1728),
        // Extended makeup (same codes as white)
        (11, 0b00000001000_00, 1792), (11, 0b00000001100_00, 1856),
        (11, 0b00000001101_00, 1920), (12, 0b000000010010_0, 1984),
        (12, 0b000000010011_0, 2048), (12, 0b000000010100_0, 2112),
        (12, 0b000000010101_0, 2176), (12, 0b000000010110_0, 2240),
        (12, 0b000000010111_0, 2304), (12, 0b000000011100_0, 2368),
        (12, 0b000000011101_0, 2432), (12, 0b000000011110_0, 2496),
        (12, 0b000000011111_0, 2560)
    ];

    // ── Lookup tables (built once, indexed by top 13 bits of bit stream) ────

    private const int LookupBits = 13;
    private const int LookupSize = 1 << LookupBits; // 8192

    // Lookup entry: run count (≥0 = terminating; <0 = makeup needing another code to follow).
    // code_length in high byte: (len << 16) | run  — stored as int32.
    // Invalid entry = 0 (len=0, run=0 is impossible for a valid code).
    private static readonly int[] WhiteLookup = BuildLookup(WhiteTermRaw, WhiteMakeupRaw);
    private static readonly int[] BlackLookup = BuildLookup(BlackTermRaw, BlackMakeupRaw);

    // Encode: (codeLen << 16) | (run & 0xFFFF)
    private static int[] BuildLookup(IEnumerable<(int L, ushort V, int Run)> term, IEnumerable<(int L, ushort V, int Run)> makeup)
    {
        var table = new int[LookupSize];
        // Fill terminating codes (run 0..63 → stored as-is, positive)
        foreach (var (l, v, run) in term)
            FillTable(table, l, v >> (LookupBits - l), run, l);
        // Fill makeup codes (stored as negative of run to distinguish from terminating)
        foreach (var (l, v, run) in makeup)
            FillTable(table, l, v >> (LookupBits - l), -run, l);
        return table;
    }

    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    private static void FillTable(IList<int> table, int codeLen, int baseIdx, int run, int len)
    {
        var extra = LookupBits - codeLen;
        var count = 1 << extra;
        var encoded = (len << 16) | (run & 0xFFFF);
        for (var i = 0; i < count; i++)
            table[baseIdx + i] = encoded;
    }

    // ── Decoder entry point ──────────────────────────────────────────────────

    /// <summary>
    /// Decodes CCITT-compressed data using the parameters in <paramref name="parms"/>.
    /// Output is a packed-bit bitmap: 1 bit per pixel, MSB = leftmost pixel, rows packed
    /// to byte boundaries. Respects <c>BlackIs1</c> to determine the background colour.
    /// </summary>
    internal static ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> data, PdfDictionary? parms)
    {
        var k = (int)(parms?.Get<PdfInteger>("K")?.Value ?? 0L);
        var columns = (int)(parms?.Get<PdfInteger>("Columns")?.Value ?? 1728L);
        var rows = (int)(parms?.Get<PdfInteger>("Rows")?.Value ?? 0L);
        var blackIs1 = parms?.Get<PdfBoolean>("BlackIs1")?.Value ?? false;
        var endOfBlock = parms?.Get<PdfBoolean>("EndOfBlock")?.Value ?? true;
        var encodedByteAlign = parms?.Get<PdfBoolean>("EncodedByteAlign")?.Value ?? false;

        return k switch
        {
            < 0 => DecodeGroup4(data, columns, rows, blackIs1, endOfBlock),
            0 => DecodeGroup3_1D(data,
                columns,
                rows,
                blackIs1,
                encodedByteAlign,
                endOfBlock),
            _ => throw new NotSupportedException("CCITTFaxDecode: K>0 (Group 3 mixed 2D) is not yet implemented.")
        };
    }

    // ── Group 4 (K = -1, T.6 pure 2D) ───────────────────────────────────────

    private static ReadOnlyMemory<byte> DecodeGroup4(
        ReadOnlyMemory<byte> data,
        int columns,
        int rows,
        bool blackIs1,
        bool endOfBlock
    )
    {
        var input = data.Span;
        var rowBytes = (columns + 7) >> 3;
        var output = new MemoryStream(rows > 0 ? rows * rowBytes : rowBytes * 64);

        var bitPos = 0;
        // Initial reference row: all white (bit=0 when BlackIs1=false, 1 otherwise)
        var refRow = new bool[columns + 1];
        var curRow = new bool[columns + 1];
        var whiteBit = !blackIs1; // true = white when !BlackIs1

        for (;;)
        {
            Array.Fill(curRow, whiteBit, 0, columns);

            var a0 = -1; // "before" the first pixel
            var a0Color = whiteBit; // color to the left of a0

            var complete = DecodeRow2D(
                input,
                ref bitPos,
                refRow,
                curRow,
                columns,
                whiteBit,
                endOfBlock,
                ref a0,
                ref a0Color
            );
            if (!complete)
                break; // EOFB reached

            WriteRow(output, curRow, columns, blackIs1);

            // Current row becomes new reference row
            Array.Copy(curRow, refRow, columns + 1);

            if (rows > 0 && output.Length >= (long)rows * rowBytes)
                break;
        }

        return output.ToArray();
    }

    // Returns false when EOFB (two consecutive EOL codes) is encountered and endOfBlock is true.
    private static bool DecodeRow2D(
        ReadOnlySpan<byte> input,
        ref int bitPos,
        IReadOnlyList<bool> refRow,
        IList<bool> curRow,
        int columns,
        bool whiteBit,
        bool endOfBlock,
        ref int a0,
        ref bool a0Color
    )
    {
        while (a0 < columns)
        {
            // Check for EOFB only when EndOfBlock is in effect (default true for Group 4).
            if (endOfBlock)
            {
                var saved = bitPos;
                if (TryReadEolOrEofb(input, ref bitPos))
                    return false;

                bitPos = saved;
            }

            // Read 2D mode code
            var mode = Read2dMode(input, ref bitPos);
            if (mode < 0)
                break; // end of data

            switch (mode)
            {
                case Mode2D.Pass:
                {
                    // Move a0 to b2 (second changing pixel in ref after a0)
                    var (_, b2) = FindB1B2(refRow, a0, a0Color, columns);
                    a0 = b2;

                    break;
                }
                case Mode2D.Horizontal:
                {
                    // Two 1D runs: a0a1 (current color) then a1a2 (opposite color)
                    var run1 = ReadRunLength(input, ref bitPos, a0Color == whiteBit);
                    if (run1 < 0)
                        return true;

                    var run2 = ReadRunLength(input, ref bitPos, a0Color != whiteBit);
                    if (run2 < 0)
                        return true;

                    FillRun(curRow, a0 + 1, run1, a0Color);
                    FillRun(curRow, a0 + 1 + run1, run2, !a0Color);
                    a0 += run1 + run2;
                    a0Color = a0 < columns ? curRow[a0] : whiteBit;

                    break;
                }
                default: // Vertical(offset) where mode = V0..V±3
                {
                    var offset = mode - Mode2D.V0;
                    var (b1, _) = FindB1B2(refRow, a0, a0Color, columns);
                    var a1 = b1 + offset;
                    a1 = Math.Clamp(a1, 0, columns);
                    FillRun(curRow, a0 + 1, a1 - a0 - 1, a0Color);
                    a0 = a1;
                    a0Color = a0 < columns ? !a0Color : whiteBit;

                    break;
                }
            }
        }

        return true;
    }

    // ── Group 3 1D (K = 0) ───────────────────────────────────────────────────

    private static ReadOnlyMemory<byte> DecodeGroup3_1D(
        ReadOnlyMemory<byte> data,
        int columns,
        int rows,
        bool blackIs1,
        bool encodedByteAlign,
        bool endOfBlock
    )
    {
        var input = data.Span;
        var rowBytes = (columns + 7) >> 3;
        var output = new MemoryStream(rows > 0 ? rows * rowBytes : rowBytes * 64);

        var bitPos = 0;
        var whiteBit = !blackIs1;

        for (var rowIdx = 0; rows == 0 || rowIdx < rows; rowIdx++)
        {
            // Skip EOL (12 bits: 0000 0000 0001)
            SkipEol(input, ref bitPos);
            if (encodedByteAlign)
                bitPos = (bitPos + 7) & ~7; // align to next byte boundary

            var row = new bool[columns + 1];
            Array.Fill(row, whiteBit, 0, columns);

            var pos = 0;
            var isWhite = true;

            while (pos < columns)
            {
                var run = ReadRunLength(input, ref bitPos, isWhite);
                if (run < 0)
                {
                    output.Write(Array.Empty<byte>());
                    break;
                }

                FillRun(row, pos, run, isWhite == whiteBit);

                pos += run;
                isWhite = !isWhite;
            }

            // Check for EOFB (two consecutive EOL codes indicate end of data)
            var saved = bitPos;
            SkipEol(input, ref bitPos);
            var saved2 = bitPos;
            SkipEol(input, ref bitPos);
            if (saved2 == bitPos) // no second EOL → not EOFB, rewind
                bitPos = saved;
            else if (endOfBlock)
                break; // EOFB found

            WriteRow(output, row, columns, blackIs1);
        }

        return output.ToArray();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static class Mode2D
    {
        public const int Pass = 4;       // distinct from V offsets (−3…+3) and the −1 sentinel
        public const int Horizontal = 5;
        public const int V0 = 0;
        // V(+1)=1, V(-1)=-1, V(+2)=2, V(-2)=-2, V(+3)=3, V(-3)=-3; −1 = end-of-data
    }

    // 2D mode codes (T.6 §4.2.1).
    private static int Read2dMode(ReadOnlySpan<byte> data, ref int bitPos)
    {
        while (true)
        {
            var b0 = PeekBit(data, bitPos);

            switch (b0)
            {
                case < 0:
                    return -1;
                case 1:
                    bitPos++;
                    return Mode2D.V0;
            }

            var b1 = PeekBit(data, bitPos + 1);
            if (b1 < 0)
                return -1;

            switch (b0)
            {
                case 0 when b1 == 1:
                {
                    var b2 = PeekBit(data, bitPos + 2);
                    if (b2 < 0) return -1;

                    bitPos += 3;

                    return b2 == 1 ? Mode2D.Horizontal : Mode2D.V0 - 1; // 001=H, 010=V(-1)
                }
                case 0 when b1 == 0:
                {
                    var b2 = PeekBit(data, bitPos + 2);
                    var b3 = PeekBit(data, bitPos + 3);
                    if (b2 < 0 || b3 < 0) return -1;

                    switch (b2)
                    {
                        case 1 when b3 == 1:
                            bitPos += 4;
                            return Mode2D.Pass;
                        case 1 when b3 == 0:
                            bitPos += 4;
                            return Mode2D.V0 + 2;
                        // b2==1, b3 ∈ {0,1}: both guarded cases above exhaust all possibilities.
                    }

                    // 00 0 0...
                    var b4 = PeekBit(data, bitPos + 4);
                    if (b4 < 0)
                        return -1;

                    // 000 0 x: 0000 1 1 = V(+3), 0000 1 0 = V(-3)
                    // Actually: 000011 = V(+3), 000010 = V(-3), 0001 = Pass (already handled above)
                    var b5 = PeekBit(data, bitPos + 5);
                    if (b3 == 0 && b4 == 1 && b5 >= 0)
                    {
                        bitPos += 6;
                        return b5 == 1 ? Mode2D.V0 + 3 : Mode2D.V0 - 3;
                    }

                    // Long zero run = EOL or EOFB padding — skip
                    bitPos++;

                    continue;
                }
            }

            bitPos++;

            return -1;
        }
    }

    // Finds (b1, b2): first and second changing pixels in refRow after a0 with colour opposite to a0Color.
    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    private static (int b1, int b2) FindB1B2(IReadOnlyList<bool> refRow, int a0, bool a0Color, int columns)
    {
        // b1 = first pixel in refRow after a0 with colour != a0Color
        var b1 = a0 + 1;
        while (b1 < columns && refRow[b1] == a0Color)
            b1++;
        // b2 = first pixel in refRow after b1 with colour == a0Color (same as a0)
        var b2 = b1 + 1;
        while (b2 < columns && refRow[b2] != a0Color)
            b2++;

        return (b1, b2);
    }

    // Reads a complete run length (makeup + terminating codes).
    // isWhiteRun=true → white lookup; false → black lookup.
    // Returns total run length, or -1 on end of data.
    private static int ReadRunLength(ReadOnlySpan<byte> data, ref int bitPos, bool isWhiteRun)
    {
        var lookup = isWhiteRun ? WhiteLookup : BlackLookup;
        var total = 0;

        while (true)
        {
            var bits = Peek13(data, bitPos);
            if (bits < 0)
                return total > 0 ? total : -1;

            var entry = lookup[bits];
            if (entry == 0)
                return total; // unknown code — treat as end of run

            var len = entry >> 16;
            var run = (short)(entry & 0xFFFF); // sign-extended: negative = makeup
            bitPos += len;

            if (run >= 0)
                return total + run; // terminating code

            total += -run; // makeup code: accumulate and read next
        }
    }

    // Reads up to 13 bits MSB first from position bitPos. Returns -1 at end of data.
    private static int Peek13(ReadOnlySpan<byte> data, int bitPos)
    {
        var result = 0;
        var avail = (data.Length * 8) - bitPos;
        var count = Math.Min(LookupBits, avail);
        if (count <= 0)
            return -1;

        for (var i = 0; i < count; i++)
        {
            var byteIdx = (bitPos + i) >> 3;
            result = (result << 1) | ((data[byteIdx] >> (7 - ((bitPos + i) & 7))) & 1);
        }

        // Pad with zeros if fewer than 13 bits available (valid for shorter codes)
        result <<= LookupBits - count;

        return result;
    }

    private static int PeekBit(ReadOnlySpan<byte> data, int bitPos)
    {
        var byteIdx = bitPos >> 3;
        return byteIdx >= data.Length ? -1 : (data[byteIdx] >> (7 - (bitPos & 7))) & 1;
    }

    // Checks for EOFB (two EOL codes = 24 zero bits + 1, twice) without consuming bits.
    private static bool TryReadEolOrEofb(ReadOnlySpan<byte> data, ref int bitPos)
    {
        // EOL = 12 bits: 0000 0000 0001
        var p = bitPos;
        for (var i = 0; i < 11; i++)
        {
            if (PeekBit(data, p++) != 0)
                return false;
        }

        if (PeekBit(data, p++) != 1)
            return false;

        // Second EOL?
        for (var i = 0; i < 11; i++)
        {
            if (PeekBit(data, p++) != 0)
                return false;
        }

        if (PeekBit(data, p) != 1)
            return false;

        bitPos = p + 1;

        return true;
    }

    private static void SkipEol(ReadOnlySpan<byte> data, ref int bitPos)
    {
        // Advance past 12-bit EOL (00000000001)
        for (var i = 0; i < 11; i++)
        {
            if (PeekBit(data, bitPos) == 0)
                bitPos++;
            else
                return;
        }

        if (PeekBit(data, bitPos) == 1)
            bitPos++;
    }

    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    private static void FillRun(IList<bool> row, int start, int length, bool color)
    {
        var end = Math.Min(start + length, row.Count - 1);
        for (var i = start; i < end; i++) row[i] = color;
    }

    [SuppressMessage("ReSharper", "BadListLineBreaks")]
    private static void WriteRow(Stream output, IList<bool> row, int columns, bool blackIs1)
    {
        var rowBytes = (columns + 7) >> 3;
        var buf = new byte[rowBytes];

        for (var i = 0; i < columns; i++)
        {
            // blackIs1=false: true=white=1, false=black=0 in output
            // blackIs1=true:  true=black=1, false=white=0 in output
            var isSet = blackIs1 ? !row[i] : row[i];
            if (isSet) buf[i >> 3] |= (byte)(0x80 >> (i & 7));
        }

        output.Write(buf);
    }
}
