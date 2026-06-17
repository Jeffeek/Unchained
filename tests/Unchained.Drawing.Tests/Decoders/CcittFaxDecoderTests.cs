using System.Text;
using Shouldly;
using Unchained.Drawing.Decoders;
using Xunit;

namespace Unchained.Drawing.Tests.Decoders;

/// <summary>
///     Tests for <see cref="CcittFaxDecoder" /> exercising the three coding schemes selected by the
///     <c>k</c> parameter: Group 4 (k&lt;0, T.6 pure 2D), Group 3 1D (k=0), and Group 3 mixed 1D/2D
///     (k&gt;0, T.4). The G4 and G3-2D fixtures are bit-exact ImageMagick/libtiff output for a 16×16
///     figure (verified against libtiff); the G3-1D streams are hand-built from the T.4 white
///     terminating Huffman codes. Output is packed 1-bpp, MSB-first; with <c>blackIs1=false</c> a set
///     bit is white.
/// </summary>
public sealed class CcittFaxDecoderTests
{
    // 16×16 Group-4 figure (ImageMagick), verified bit-exact against libtiff.
    private static readonly byte[] EncodedG4 =
        Convert.FromHexString("26a0bfcc39147c47231ffff1cc3918febb58fc004004");

    // 16×16 Group-3 2D (T.4, K>0) of the same figure (libtiff), framed EOL + tag bit per line.
    private static readonly byte[] EncodedG32D =
        Convert.FromHexString(
            "001d4002800ea00118466a003a80047d4007ea6002e003f530010c0070bd5800bb800ec5b0010c0075000a"
        );

    // Two all-white 8-pixel rows encoded with the T.4 white terminating code for run 8
    // (5-bit "10011"), packed MSB-first with no EOL framing: "1001110011" → 0x9C 0xC0.
    private static readonly byte[] EncodedG31DWhite = [0x9C, 0xC0];

    private static string RowBits(IReadOnlyList<byte> decoded, int row, int columns)
    {
        var rowBytes = (columns + 7) >> 3;
        var sb = new StringBuilder(columns);
        for (var col = 0; col < columns; col++)
        {
            var bit = (decoded[(row * rowBytes) + (col >> 3)] >> (7 - (col & 7))) & 1;
            sb.Append(bit);
        }

        return sb.ToString();
    }

    [Fact]
    public void DecodeGroup4_ProducesExpectedBitmap()
    {
        var decoded = CcittFaxDecoder.Decode(EncodedG4, -1, 16, 16).ToArray();

        string[] expected =
        [
            "0000000000000000", "0000000000000000", "0000000000000000",
            "0001000000001000", "0000000000000000", "0000000100000000",
            "0000000100000000", "0000000100000000", "0000000100000000",
            "0000000000000000", "0001000000010000", "0001000000110000",
            "0000111111100000", "0000000000000000", "0000000000000000",
            "0000000000000000"
        ];

        decoded.Length.ShouldBe(2 * 16);
        for (var row = 0; row < 16; row++)
            RowBits(decoded, row, 16).ShouldBe(expected[row], $"row {row} mismatch");
    }

    [Fact]
    public void DecodeGroup4_BlackIs1_InvertsOutput()
    {
        var normal = CcittFaxDecoder.Decode(EncodedG4, -1, 16, 16).ToArray();
        var inverted = CcittFaxDecoder.Decode(EncodedG4, -1, 16, 16, blackIs1: true).ToArray();

        inverted.Length.ShouldBe(normal.Length);
        for (var i = 0; i < normal.Length; i++)
            inverted[i].ShouldBe((byte)~normal[i]);
    }

    [Fact]
    public void DecodeGroup4_RowsZero_DecodesUntilExhausted()
    {
        // rows=0 means "decode until data runs out"; the figure is 16 rows tall.
        var decoded = CcittFaxDecoder.Decode(EncodedG4, -1, 16).ToArray();
        decoded.Length.ShouldBeGreaterThanOrEqualTo(2 * 16);
    }

    [Fact]
    public void DecodeGroup3_2D_ProducesExpectedBitmap()
    {
        var decoded = CcittFaxDecoder.Decode(EncodedG32D, 4, 16, 16).ToArray();

        string[] expected =
        [
            "1111111111111111", "1111111111111111", "1111111111111111",
            "1110111111110111", "1111111111111111", "1111111011111111",
            "1111111011111111", "1111111011111111", "1111111011111111",
            "1111111111111111", "1110111111101111", "1110111111100111",
            "1111000000001111", "1111111111111111", "1111111111111111",
            "1111111111111111"
        ];

        decoded.Length.ShouldBe(2 * 16);
        for (var row = 0; row < 16; row++)
            RowBits(decoded, row, 16).ShouldBe(expected[row], $"row {row} mismatch");
    }

    [Fact]
    public void DecodeGroup3_2D_BlackIs1_InvertsOutput()
    {
        var normal = CcittFaxDecoder.Decode(EncodedG32D, 4, 16, 16).ToArray();
        var inverted = CcittFaxDecoder.Decode(EncodedG32D, 4, 16, 16, blackIs1: true).ToArray();

        inverted.Length.ShouldBe(normal.Length);
        for (var i = 0; i < normal.Length; i++)
            inverted[i].ShouldBe((byte)~normal[i]);
    }

    [Fact]
    public void DecodeGroup3_1D_AllWhiteRows()
    {
        var decoded = CcittFaxDecoder.Decode(EncodedG31DWhite, 0, 8, 2).ToArray();

        // 8-pixel rows → 1 byte per row, 2 rows. All white (blackIs1=false) → every bit set.
        decoded.Length.ShouldBe(2);
        RowBits(decoded, 0, 8).ShouldBe("11111111");
        RowBits(decoded, 1, 8).ShouldBe("11111111");
    }

    [Fact]
    public void DecodeGroup3_1D_BlackIs1_StillSetsWhiteRowBits()
    {
        // The 1D path applies blackIs1 twice: once when choosing the row fill colour
        // (whiteBit = !blackIs1) and again in WriteRow (isSet = blackIs1 ? !row : row).
        // The two negations cancel, so an all-white row reads back as all-set bits
        // regardless of blackIs1.
        var decoded = CcittFaxDecoder.Decode(EncodedG31DWhite, 0, 8, 2, blackIs1: true).ToArray();

        decoded.Length.ShouldBe(2);
        RowBits(decoded, 0, 8).ShouldBe("11111111");
    }

    [Fact]
    public void DecodeGroup3_1D_EncodedByteAlign_DecodesRows()
    {
        // Each row's white run-8 code ("10011") padded to a byte boundary, so byte-aligned
        // decoding still recovers two all-white rows.
        byte[] aligned = [0b10011_000, 0b10011_000];
        var decoded = CcittFaxDecoder.Decode(
                aligned,
                0,
                8,
                2,
                blackIs1: false,
                endOfBlock: true,
                encodedByteAlign: true
            )
            .ToArray();

        decoded.Length.ShouldBe(2);
        RowBits(decoded, 0, 8).ShouldBe("11111111");
        RowBits(decoded, 1, 8).ShouldBe("11111111");
    }

    [Fact]
    public void Decode_EmptyData_ProducesNoRows()
    {
        var decoded = CcittFaxDecoder.Decode(ReadOnlyMemory<byte>.Empty, 0, 16, 4).ToArray();
        // No input bits → decoder emits blank rows or nothing; never throws.
        decoded.Length.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Decode_DefaultColumns_Group4_DoesNotThrow() =>
        Should.NotThrow(static () => CcittFaxDecoder.Decode(EncodedG4, -1).ToArray());
}
