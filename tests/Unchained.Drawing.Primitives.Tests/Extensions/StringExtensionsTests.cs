using Shouldly;
using Unchained.Drawing.Primitives.Extensions;
using Xunit;

namespace Unchained.Drawing.Primitives.Tests.Extensions;

/// <summary>Unit tests for the <see cref="StringExtensions" /> UTF-8 conversion helpers.</summary>
public sealed class StringExtensionsTests
{
    [Fact]
    public void ToUtf8Span_AsciiString_EncodesBytes() =>
        "ABC".ToUtf8Span().ToArray().ShouldBe("ABC"u8.ToArray());

    [Fact]
    public void FromUtf8Span_DecodesBytes() =>
        new ReadOnlySpan<byte>("ABC"u8.ToArray()).FromUtf8Span().ShouldBe("ABC");

    [
        Theory,
        InlineData(""),
        InlineData("hello world"),
        InlineData("Unicode: café — ☃ — 日本語")
    ]
    public void RoundTrip_PreservesString(string source) =>
        source.ToUtf8Span().FromUtf8Span().ShouldBe(source);

    [Fact]
    public void RoundTrip_SingleNullByte() =>
        "\0".ToUtf8Span().FromUtf8Span().ShouldBe("\0");

    [Fact]
    public void RoundTrip_TwoByteUTF8Sequence() =>
        "".ToUtf8Span().FromUtf8Span().ShouldBe("");

    [Fact]
    public void RoundTrip_SurrogatePair() =>
        "😀".ToUtf8Span().FromUtf8Span().ShouldBe("😀");
}
