using System.Text;
using Shouldly;
using Unchained.Drawing.Decoders;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Parsing;

public sealed class Ascii85DecoderTests
{
    [Fact]
    public void Decode_HelloWorld_ReturnsCorrectBytes()
    {
        // "Man" in ASCII85 is "9jqo~>"
        var encoded = "9jqo~>"u8.ToArray();
        var result = Ascii85Decoder.Decode(encoded);
        result.Span[0].ShouldBe((byte)'M');
        result.Span[1].ShouldBe((byte)'a');
        result.Span[2].ShouldBe((byte)'n');
    }

    [Fact]
    public void Decode_ZShorthand_ProducesFourZeroBytes()
    {
        var result = Ascii85Decoder.Decode("z~>"u8.ToArray());
        result.Length.ShouldBe(4);
        result.Span.ToArray().ShouldAllBe(static b => b == 0);
    }

    [Fact]
    public void Decode_EmptyInput_ReturnsEmpty() => Ascii85Decoder.Decode("~>"u8.ToArray()).Length.ShouldBe(0);

    [Fact]
    public async Task Decode_MultipleFullGroups_CorrectOutput()
    {
        // "Man " → base85 group "9jqo", " Lig" → group "F*2M7"
        // Use known encoding: "Man " is encoded as "9jqo" (4 chars → 3 bytes "Man")
        // Actually test two 'z' groups = 8 zero bytes
        var result = await Task.Run(static () => Ascii85Decoder.Decode("zz~>"u8.ToArray()));
        result.Length.ShouldBe(8);
        result.Span.ToArray().ShouldAllBe(static b => b == 0);
    }

    [Fact]
    public async Task Decode_PartialGroup_TwoChars_OneByte()
    {
        // Partial group with 2 chars encodes 1 byte.
        // "!!" in base85 = value 0, emit 1 byte = 0x00
        var result = await Task.Run(static () => Ascii85Decoder.Decode("!!~>"u8.ToArray()));
        result.Length.ShouldBe(1);
        result.Span[0].ShouldBe((byte)0x00);
    }

    [Fact]
    public async Task Decode_PartialGroup_ThreeChars_TwoBytes()
    {
        // Partial group with 3 chars encodes 2 bytes.
        var result = await Task.Run(static () => Ascii85Decoder.Decode("!!!~>"u8.ToArray()));
        result.Length.ShouldBe(2);
    }

    [Fact]
    public async Task Decode_PartialGroup_FourChars_ThreeBytes()
    {
        // Partial group with 4 chars encodes 3 bytes.
        var result = await Task.Run(static () => Ascii85Decoder.Decode("!!!!~>"u8.ToArray()));
        result.Length.ShouldBe(3);
    }

    [Fact]
    public async Task Decode_WhitespaceWithinEncoded_Ignored()
    {
        // "9jqo~>" with internal spaces/newlines still decodes "Man"
        var result = await Task.Run(static () => Ascii85Decoder.Decode("9j qo\n~>"u8.ToArray()));
        result.Length.ShouldBe(3);
        result.Span[0].ShouldBe((byte)'M');
    }

    [Fact]
    public async Task Decode_ZInsideGroup_ThrowsPdfException() =>
        // 'z' appearing when groupLen != 0 is illegal
        await Should.ThrowAsync<InvalidDataException>(static () =>
            Task.Run(static () => Ascii85Decoder.Decode("!z~>"u8.ToArray())));

    [Fact]
    public async Task Decode_TildeNotFollowedByGreaterThan_ThrowsPdfException() =>
        await Should.ThrowAsync<InvalidDataException>(static () =>
            Task.Run(static () => Ascii85Decoder.Decode("~~"u8.ToArray())));

    [Fact]
    public async Task Decode_OutOfRangeCharacter_ThrowsPdfException() =>
        // 'v' (0x76=118) is above 'u' (0x75=117)
        await Should.ThrowAsync<InvalidDataException>(static () =>
            Task.Run(static () => Ascii85Decoder.Decode("vvvvv~>"u8.ToArray())));

    [Fact]
    public async Task Decode_KnownVector_ManEncoding()
    {
        // "9jqo~>" → "Man"
        var result = await Task.Run(static () => Ascii85Decoder.Decode("9jqo~>"u8.ToArray()));
        Encoding.ASCII.GetString(result.Span).ShouldBe("Man");
    }
}
