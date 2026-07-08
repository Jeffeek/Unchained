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
    public void Is_MatchingKind_ReturnsTrue()
    {
        var token = MakeToken(PdfTokenKind.Integer, "42");
        token.Is(PdfTokenKind.Integer).ShouldBeTrue();
    }

    [Fact]
    public void Is_DifferentKind_ReturnsFalse()
    {
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
    public void Is_MatchingKind_ReturnsTrue_PdfTokenKind(PdfTokenKind kind)
    {
        var token = MakeToken(kind);
        token.Is(kind).ShouldBeTrue();
    }

    [Fact]
    public void Kind_StoredCorrectly()
    {
        var token = MakeToken(PdfTokenKind.Name, "/Type");
        token.Kind.ShouldBe(PdfTokenKind.Name);
    }

    [Fact]
    public void Offset_StoredCorrectly()
    {
        var token = MakeToken(PdfTokenKind.Integer, "99", 0x200);
        token.Offset.ShouldBe(0x200L);
    }

    [Fact]
    public void Raw_ContainsOriginalBytes()
    {
        var token = MakeToken(PdfTokenKind.Name, "/MyName");
        Encoding.Latin1.GetString(token.Raw.Span).ShouldBe("/MyName");
    }

    [Fact]
    public void ToString_FormatsKindOffsetAndRaw()
    {
        var token = MakeToken(PdfTokenKind.Integer, "42", 0x10);
        var str = token.ToString();
        str.ShouldBe("Integer @ 0x10 \"42\"");
    }
}
