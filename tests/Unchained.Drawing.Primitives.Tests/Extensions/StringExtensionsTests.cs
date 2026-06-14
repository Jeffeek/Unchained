using Shouldly;
using Unchained.Drawing.Extensions;
using Xunit;

namespace Unchained.Drawing.Primitives.Tests.Extensions;

/// <summary>Unit tests for the <see cref="StringExtensions" /> UTF-8 conversion helpers.</summary>
public sealed class StringExtensionsTests
{
    [Fact]
    public void ToUtf8Span_AsciiString_EncodesBytes() =>
        "ABC".ToUtf8Span().ToArray().ShouldBe([(byte)0x41, (byte)0x42, (byte)0x43]);

    [Fact]
    public void FromUtf8Span_DecodesBytes() =>
        new ReadOnlySpan<byte>([(byte)0x41, (byte)0x42, (byte)0x43]).FromUtf8Span().ShouldBe("ABC");

    [
        Theory,
        InlineData(""),
        InlineData("hello world"),
        InlineData("Unicode: café — ☃ — 日本語")
    ]
    public void RoundTrip_PreservesString(string source) =>
        source.ToUtf8Span().FromUtf8Span().ShouldBe(source);
}
