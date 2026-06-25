using System.Text;
using Shouldly;
using Unchained.Drawing.Decoders;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Parsing;

public sealed class CcittFaxDecoderTests
{
    // A 16×16 Group-4 image produced by ImageMagick (the imagemagick-ccitt.pdf fixture).
    // Verified bit-exact against libtiff's Group-4 decoder. With BlackIs1=false the
    // decoded samples are 1=white, 0=black; the figure is a sparse white pattern on black.
    private static readonly byte[] EncodedG4 =
        Convert.FromHexString("26a0bfcc39147c47231ffff1cc3918febb58fc004004");

    // A 16×16 Group-3 2D (T.4, K>0) image of the same figure, produced by libtiff with
    // T4Options bit-0 (2D) set and Photometric=0 (white runs first, matching the PDF/G4
    // convention). Each line is framed by an EOL + a 1D/2D tag bit. Verified bit-exact
    // against libtiff's own decoder. BlackIs1=false → 1=white.
    private static readonly byte[] EncodedG32D =
        Convert.FromHexString(
            "001d4002800ea00118466a003a80047d4007ea6002e003f530010c0070bd5800bb800ec5b0010c0075000a"
        );

    [Fact]
    public void DecodeGroup4_ProducesExpectedBitmap()
    {
        var decoded = CcittFaxDecoder.Decode(EncodedG4, -1, 16, 16).ToArray();

        // Expected per-row bits, 1 = white (BlackIs1=false). Bit-exact reference output.
        string[] expected =
        [
            "0000000000000000", "0000000000000000", "0000000000000000",
            "0001000000001000", "0000000000000000", "0000000100000000",
            "0000000100000000", "0000000100000000", "0000000100000000",
            "0000000000000000", "0001000000010000", "0001000000110000",
            "0000111111100000", "0000000000000000", "0000000000000000",
            "0000000000000000"
        ];

        const int rowBytes = 2;
        decoded.Length.ShouldBe(rowBytes * 16);
        var sb = new StringBuilder(16);
        for (var row = 0; row < 16; row++)
        {
            sb.Clear();
            for (var col = 0; col < 16; col++)
            {
                var bit = (decoded[(row * rowBytes) + (col >> 3)] >> (7 - (col & 7))) & 1;
                sb.Append(bit);
            }

            sb.ToString().ShouldBe(expected[row], $"row {row} mismatch");
        }
    }

    [Fact]
    public void DecodeGroup4_BlackIs1_InvertsOutput()
    {
        var normal = CcittFaxDecoder.Decode(EncodedG4, -1, 16, 16).ToArray();

        var inverted = CcittFaxDecoder.Decode(EncodedG4, -1, 16, 16, true).ToArray();

        inverted.Length.ShouldBe(normal.Length);
        for (var i = 0; i < normal.Length; i++)
            inverted[i].ShouldBe((byte)~normal[i]);
    }

    [Fact]
    public void DecodeGroup3_2D_ProducesExpectedBitmap()
    {
        var decoded = CcittFaxDecoder.Decode(EncodedG32D, 4, 16, 16).ToArray();

        // Same figure as the Group-4 fixture. 1 = white (BlackIs1=false).
        string[] expected =
        [
            "1111111111111111", "1111111111111111", "1111111111111111",
            "1110111111110111", "1111111111111111", "1111111011111111",
            "1111111011111111", "1111111011111111", "1111111011111111",
            "1111111111111111", "1110111111101111", "1110111111100111",
            "1111000000001111", "1111111111111111", "1111111111111111",
            "1111111111111111"
        ];

        const int rowBytes = 2;
        decoded.Length.ShouldBe(rowBytes * 16);
        var sb = new StringBuilder(16);
        for (var row = 0; row < 16; row++)
        {
            sb.Clear();
            for (var col = 0; col < 16; col++)
            {
                var bit = (decoded[(row * rowBytes) + (col >> 3)] >> (7 - (col & 7))) & 1;
                sb.Append(bit);
            }

            sb.ToString().ShouldBe(expected[row], $"row {row} mismatch");
        }
    }

    [Fact]
    public void DecodeGroup3_2D_BlackIs1_InvertsOutput()
    {
        var normal = CcittFaxDecoder.Decode(EncodedG32D, 4, 16, 16).ToArray();
        var inverted = CcittFaxDecoder.Decode(EncodedG32D, 4, 16, 16, true).ToArray();

        inverted.Length.ShouldBe(normal.Length);
        for (var i = 0; i < normal.Length; i++)
            inverted[i].ShouldBe((byte)~normal[i]);
    }
}
