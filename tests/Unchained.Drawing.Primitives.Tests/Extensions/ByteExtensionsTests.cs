using Shouldly;
using Unchained.Drawing.Extensions;
using Xunit;

namespace Unchained.Drawing.Primitives.Tests.Extensions;

/// <summary>Unit tests for the <see cref="ByteExtensions" /> byte helper methods.</summary>
public sealed class ByteExtensionsTests
{
    [
        Theory,
        InlineData((byte)0x00),
        InlineData((byte)0x09),
        InlineData((byte)0x0A),
        InlineData((byte)0x0C),
        InlineData((byte)0x0D),
        InlineData((byte)0x20)
    ]
    public void IsWhitespace_WhitespaceBytes_True(byte b) =>
        b.IsWhitespace().ShouldBeTrue();

    [
        Theory,
        InlineData((byte)'A'),
        InlineData((byte)'0'),
        InlineData((byte)0x0B),
        InlineData((byte)0x7F)
    ]
    public void IsWhitespace_NonWhitespaceBytes_False(byte b) =>
        b.IsWhitespace().ShouldBeFalse();

    [Fact]
    public void BitMsbFirst_ReadsBitsMostSignificantFirst()
    {
        const byte value = 0b1010_0000;
        value.BitMsbFirst(0).ShouldBe(1); // MSB
        value.BitMsbFirst(1).ShouldBe(0);
        value.BitMsbFirst(2).ShouldBe(1);
        value.BitMsbFirst(3).ShouldBe(0);
        value.BitMsbFirst(7).ShouldBe(0); // LSB
    }

    [Fact]
    public void BitMsbFirst_LeastSignificantBit()
    {
        const byte value = 0b0000_0001;
        value.BitMsbFirst(7).ShouldBe(1);
        value.BitMsbFirst(0).ShouldBe(0);
    }

    [
        Theory,
        InlineData((byte)0x00, "00"),
        InlineData((byte)0x0F, "0F"),
        InlineData((byte)0xFF, "FF"),
        InlineData((byte)0xA3, "A3")
    ]
    public void ToHex2_FormatsTwoDigitUppercase(byte b, string expected) =>
        b.ToHex2().ShouldBe(expected);
}
