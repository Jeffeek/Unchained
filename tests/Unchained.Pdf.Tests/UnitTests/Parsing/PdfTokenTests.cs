using System.Text;
using Shouldly;
using Unchained.Pdf.Parsing;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Parsing;

public sealed class PdfTokenTests
{
    private static PdfToken MakeToken(PdfTokenKind kind, string raw = "x", long offset = 0)
    {
        var bytes = Encoding.Latin1.GetBytes(raw);
        return new PdfToken(kind, bytes, offset);
    }

    [Fact]
    public async Task Is_MatchingKind_ReturnsTrue()
    {
        await Task.CompletedTask;
        var token = MakeToken(PdfTokenKind.Integer, "42");
        token.Is(PdfTokenKind.Integer).ShouldBeTrue();
    }

    [Fact]
    public async Task Is_DifferentKind_ReturnsFalse()
    {
        await Task.CompletedTask;
        var token = MakeToken(PdfTokenKind.Integer, "42");
        token.Is(PdfTokenKind.Real).ShouldBeFalse();
    }

    [
        Theory,
        InlineData(PdfTokenKind.Integer),
        InlineData(PdfTokenKind.Real),
        InlineData(PdfTokenKind.Name),
        InlineData(PdfTokenKind.LiteralString),
        InlineData(PdfTokenKind.HexString),
        InlineData(PdfTokenKind.BooleanTrue),
        InlineData(PdfTokenKind.BooleanFalse),
        InlineData(PdfTokenKind.Null),
        InlineData(PdfTokenKind.DictionaryBegin),
        InlineData(PdfTokenKind.DictionaryEnd),
        InlineData(PdfTokenKind.ArrayBegin),
        InlineData(PdfTokenKind.ArrayEnd),
        InlineData(PdfTokenKind.Stream),
        InlineData(PdfTokenKind.EndStream),
        InlineData(PdfTokenKind.Obj),
        InlineData(PdfTokenKind.EndObj),
        InlineData(PdfTokenKind.IndirectRef),
        InlineData(PdfTokenKind.Xref),
        InlineData(PdfTokenKind.Trailer),
        InlineData(PdfTokenKind.StartXref),
        InlineData(PdfTokenKind.Comment),
        InlineData(PdfTokenKind.EndOfFile)
    ]
    public async Task Is_EachKind_MatchesItself(PdfTokenKind kind)
    {
        await Task.CompletedTask;
        var token = MakeToken(kind);
        token.Is(kind).ShouldBeTrue();
    }

    [Fact]
    public async Task Kind_StoredCorrectly()
    {
        await Task.CompletedTask;
        var token = MakeToken(PdfTokenKind.Name, "/Type");
        token.Kind.ShouldBe(PdfTokenKind.Name);
    }

    [Fact]
    public async Task Offset_StoredCorrectly()
    {
        await Task.CompletedTask;
        var token = MakeToken(PdfTokenKind.Integer, "99", 0x200);
        token.Offset.ShouldBe(0x200L);
    }

    [Fact]
    public async Task Raw_ContainsOriginalBytes()
    {
        await Task.CompletedTask;
        var token = MakeToken(PdfTokenKind.Name, "/MyName");
        Encoding.Latin1.GetString(token.Raw.Span).ShouldBe("/MyName");
    }

    [Fact]
    public async Task ToString_ContainsKindOffsetAndRaw()
    {
        await Task.CompletedTask;
        var token = MakeToken(PdfTokenKind.Integer, "42", 0x10);
        var str = token.ToString();
        str.ShouldContain("Integer");
        str.ShouldContain("0x10");
        str.ShouldContain("42");
    }
}
