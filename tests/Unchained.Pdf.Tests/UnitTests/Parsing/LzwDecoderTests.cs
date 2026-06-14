using System.Text;
using Shouldly;
using Unchained.Drawing.Decoders;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Parsing;

public sealed class LzwDecoderTests
{
    private const int Clear = 256;
    private const int Eod = 257;

    /// <summary>
    ///     Builds a minimal valid LZW stream that encodes a Clear code followed by
    ///     literal byte codes followed by EOD.  Uses 9-bit codes (MSB-first).
    /// </summary>
    private static ReadOnlyMemory<byte> BuildLzwStream(params int[] codes)
    {
        // Pack variable-width codes MSB-first into a byte array.
        // All test streams use 9-bit codes only for simplicity.
        const int width = 9;
        var bits = new List<bool>();
        foreach (var code in codes)
        {
            for (var i = width - 1; i >= 0; i--)
                bits.Add(((code >> i) & 1) == 1);
        }

        // Pad to byte boundary.
        while (bits.Count % 8 != 0) bits.Add(false);

        var bytes = new byte[bits.Count / 8];
        for (var i = 0; i < bits.Count; i++)
        {
            if (bits[i])
                bytes[i / 8] |= (byte)(1 << (7 - (i % 8)));
        }

        return bytes;
    }

    [Fact]
    public async Task Decode_SingleLiteral_ReturnsThatByte()
    {
        // Clear, 'A'(65), EOD
        var data = BuildLzwStream(Clear, 65, Eod);
        var result = await Task.Run(() => LzwDecoder.Decode(data));
        result.Length.ShouldBe(1);
        result.Span[0].ShouldBe((byte)'A');
    }

    [Fact]
    public async Task Decode_MultipleLiterals_ReturnsAllBytes()
    {
        // Clear, 'H','e','l','l','o', EOD
        // ReSharper disable BadListLineBreaks
        var data = BuildLzwStream(
            Clear,
            72,
            101,
            108,
            108,
            111,
            Eod
        );
        // ReSharper restore BadListLineBreaks
        var result = await Task.Run(() => LzwDecoder.Decode(data));
        Encoding.ASCII.GetString(result.Span).ShouldBe("Hello");
    }

    [Fact]
    public async Task Decode_EmptyStreamAfterClearAndEod_ReturnsEmpty()
    {
        var data = BuildLzwStream(Clear, Eod);
        var result = await Task.Run(() => LzwDecoder.Decode(data));
        result.Length.ShouldBe(0);
    }

    [Fact]
    public async Task Decode_EarlyChangeZero_StillDecodes()
    {
        var data = BuildLzwStream(Clear, 65, Eod);
        var result = await Task.Run(() => LzwDecoder.Decode(data, 0));
        result.Span[0].ShouldBe((byte)'A');
    }

    [Fact]
    public async Task Decode_EarlyChangeOne_ExplicitParm_SameAsDefault()
    {
        var data = BuildLzwStream(Clear, 66, Eod);
        var result = await Task.Run(() => LzwDecoder.Decode(data));
        result.Span[0].ShouldBe((byte)'B');
    }

    [Fact]
    public async Task Decode_MultipleRepeatedSequences_BuildsTableEntries()
    {
        // Clear, 'A','B','A','B' — second 'A' triggers table entry 258='AB',
        // second 'B' may reference 258. Use raw literals to stay below table threshold.
        // ReSharper disable BadListLineBreaks
        var data = BuildLzwStream(
            Clear,
            65,
            66,
            65,
            66,
            65,
            Eod
        );
        // ReSharper restore BadListLineBreaks
        var result = await Task.Run(() => LzwDecoder.Decode(data));
        Encoding.ASCII.GetString(result.Span).ShouldBe("ABABA");
    }

    [Fact]
    public async Task Decode_NullDecodeParms_UsesDefaults()
    {
        var data = BuildLzwStream(Clear, 88, Eod);
        var result = await Task.Run(() => LzwDecoder.Decode(data));
        result.Span[0].ShouldBe((byte)'X');
    }

    [Fact]
    public async Task Decode_TruncatedStream_StopsGracefully()
    {
        // A stream that ends mid-code returns -1 from ReadBits → treated as EOD.
        var data = new ReadOnlyMemory<byte>([Clear >> 1]); // 1 byte: only 8 of 9 bits
        var result = await Task.Run(() => LzwDecoder.Decode(data));
        result.Length.ShouldBe(0);
    }

    [Fact]
    public async Task Decode_InvalidCodeAboveNextCode_ThrowsPdfException()
    {
        // After Clear, nextCode=258. Sending code 259 (not yet in table) is illegal.
        var data = BuildLzwStream(Clear, 259, Eod);
        await Should.ThrowAsync<InvalidDataException>(() =>
            Task.Run(() => LzwDecoder.Decode(data))
        );
    }
}
