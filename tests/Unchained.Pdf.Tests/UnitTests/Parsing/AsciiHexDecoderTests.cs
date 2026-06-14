using System.Text;
using Shouldly;
using Unchained.Drawing.Decoders;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Parsing;

public sealed class AsciiHexDecoderTests
{
    [
        Theory,
        InlineData("48656C6C6F>", "Hello"),
        InlineData("68 65 6C 6C 6F>", "hello"),
        InlineData(">", "")
    ]
    public void Decode_ValidInput_ReturnsCorrectBytes(string input, string expected)
    {
        var result = AsciiHexDecoder.Decode(Encoding.ASCII.GetBytes(input));
        Encoding.ASCII.GetString(result.Span).ShouldBe(expected);
    }

    [Fact]
    public void Decode_OddNibble_PadsWithZeroOnRight()
    {
        // "4" → 0x40 (padded to "40")
        var result = AsciiHexDecoder.Decode("4>"u8.ToArray());
        result.Span[0].ShouldBe((byte)0x40);
    }

    [Fact]
    public void Decode_LowercaseHex_Works()
    {
        var result = AsciiHexDecoder.Decode("ff>"u8.ToArray());
        result.Span[0].ShouldBe((byte)0xFF);
    }

    [Fact]
    public async Task Decode_MixedCaseHex_ReturnsCorrectBytes()
    {
        // "aB" → 0xAB
        var result = await Task.Run(static () => AsciiHexDecoder.Decode("aB>"u8.ToArray()));
        result.Span[0].ShouldBe((byte)0xAB);
    }

    [Fact]
    public async Task Decode_WhitespaceWithinPairs_Ignored()
    {
        // "4 8 >" → 0x48 = 'H'
        var result = await Task.Run(static () => AsciiHexDecoder.Decode("4 8 >"u8.ToArray()));
        result.Span[0].ShouldBe((byte)0x48);
    }

    [Fact]
    public async Task Decode_TabAndNewlineWhitespace_Ignored()
    {
        // "4\t8\n>" → 0x48
        var input = "4\t8\n>"u8.ToArray();
        var result = await Task.Run(() => AsciiHexDecoder.Decode(input));
        result.Span[0].ShouldBe((byte)0x48);
    }

    [Fact]
    public async Task Decode_AllZeros_ReturnsZeroBytes()
    {
        var result = await Task.Run(static () => AsciiHexDecoder.Decode("0000>"u8.ToArray()));
        result.Length.ShouldBe(2);
        result.Span[0].ShouldBe((byte)0x00);
        result.Span[1].ShouldBe((byte)0x00);
    }

    [Fact]
    public async Task Decode_OddLength_LastNibblePaddedWithZero()
    {
        // "F" → 0xF0
        var result = await Task.Run(static () => AsciiHexDecoder.Decode("F>"u8.ToArray()));
        result.Length.ShouldBe(1);
        result.Span[0].ShouldBe((byte)0xF0);
    }

    [Fact]
    public async Task Decode_InvalidCharacter_ThrowsPdfException() =>
        // 'G' is not valid hex
        await Should.ThrowAsync<InvalidDataException>(static () => Task.Run(static () => AsciiHexDecoder.Decode("GG>"u8.ToArray())));

    [Fact]
    public async Task Decode_MaxByteValue_FF_Works()
    {
        var result = await Task.Run(static () => AsciiHexDecoder.Decode("FF>"u8.ToArray()));
        result.Span[0].ShouldBe((byte)0xFF);
    }

    [Fact]
    public async Task Decode_MultipleBytes_CorrectCount()
    {
        // "48656C6C6F" = "Hello"
        var result = await Task.Run(static () => AsciiHexDecoder.Decode("48656C6C6F>"u8.ToArray()));
        result.Length.ShouldBe(5);
        Encoding.ASCII.GetString(result.Span).ShouldBe("Hello");
    }
}
