using System.Text;
using Shouldly;
using Unchained.Pdf.Parsing;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Parsing;

public sealed class LexerTests
{
    private static Lexer Lex(string input) =>
        new(Encoding.Latin1.GetBytes(input));

    private static string Raw(PdfToken token) =>
        Encoding.Latin1.GetString(token.Raw.Span);

    // ── Primitives ────────────────────────────────────────────────────────────

    [
        Theory,
        InlineData("42", "42"),
        InlineData("-7", "-7"),
        InlineData("0", "0"),
        InlineData("+3", "+3")
    ]
    public void ReadNext_Integer_ReturnsIntegerToken(string input, string expected)
    {
        var token = Lex(input).ReadNext();
        token.Kind.ShouldBe(PdfTokenKind.Integer);
        Raw(token).ShouldBe(expected);
    }

    [
        Theory,
        InlineData("3.14"),
        InlineData("-.002"),
        InlineData("0.0")
    ]
    public void ReadNext_Real_ReturnsRealToken(string input)
    {
        var token = Lex(input).ReadNext();
        token.Kind.ShouldBe(PdfTokenKind.Real);
        Raw(token).ShouldBe(input);
    }

    [Fact]
    public void ReadNext_True_ReturnsBooleanTrue() =>
        Lex("true").ReadNext().Kind.ShouldBe(PdfTokenKind.BooleanTrue);

    [Fact]
    public void ReadNext_False_ReturnsBooleanFalse() =>
        Lex("false").ReadNext().Kind.ShouldBe(PdfTokenKind.BooleanFalse);

    [Fact]
    public void ReadNext_Null_ReturnsNullToken() =>
        Lex("null").ReadNext().Kind.ShouldBe(PdfTokenKind.Null);

    [Fact]
    public void ReadNext_Name_ReturnsNameToken()
    {
        var token = Lex("/Type").ReadNext();
        token.Kind.ShouldBe(PdfTokenKind.Name);
        Raw(token).ShouldBe("/Type");
    }

    [Fact]
    public void ReadNext_LiteralString_ReturnsLiteralStringToken()
    {
        var token = Lex("(Hello World)").ReadNext();
        token.Kind.ShouldBe(PdfTokenKind.LiteralString);
        Raw(token).ShouldBe("(Hello World)");
    }

    [Fact]
    public void ReadNext_NestedParenthesesInString_ReadsEntireString()
    {
        var token = Lex("(a (b) c)").ReadNext();
        token.Kind.ShouldBe(PdfTokenKind.LiteralString);
        Raw(token).ShouldBe("(a (b) c)");
    }

    [Fact]
    public void ReadNext_EscapedParenInString_DoesNotCloseString()
    {
        var token = Lex(@"(a\)b)").ReadNext();
        token.Kind.ShouldBe(PdfTokenKind.LiteralString);
        Raw(token).ShouldBe(@"(a\)b)");
    }

    [Fact]
    public void ReadNext_HexString_ReturnsHexStringToken()
    {
        var token = Lex("<4E6F>").ReadNext();
        token.Kind.ShouldBe(PdfTokenKind.HexString);
        Raw(token).ShouldBe("<4E6F>");
    }

    // ── Containers ────────────────────────────────────────────────────────────

    [Fact]
    public void ReadNext_DictionaryBegin_ReturnsDictionaryBeginToken() =>
        Lex("<<").ReadNext().Kind.ShouldBe(PdfTokenKind.DictionaryBegin);

    [Fact]
    public void ReadNext_DictionaryEnd_ReturnsDictionaryEndToken() =>
        Lex(">>").ReadNext().Kind.ShouldBe(PdfTokenKind.DictionaryEnd);

    [Fact]
    public void ReadNext_ArrayBegin_ReturnsArrayBeginToken() =>
        Lex("[").ReadNext().Kind.ShouldBe(PdfTokenKind.ArrayBegin);

    [Fact]
    public void ReadNext_ArrayEnd_ReturnsArrayEndToken() =>
        Lex("]").ReadNext().Kind.ShouldBe(PdfTokenKind.ArrayEnd);

    // ── Keywords ─────────────────────────────────────────────────────────────

    [
        Theory,
        InlineData("obj", PdfTokenKind.Obj),
        InlineData("endobj", PdfTokenKind.EndObj),
        InlineData("stream", PdfTokenKind.Stream),
        InlineData("endstream", PdfTokenKind.EndStream),
        InlineData("R", PdfTokenKind.IndirectRef),
        InlineData("xref", PdfTokenKind.Xref),
        InlineData("trailer", PdfTokenKind.Trailer),
        InlineData("startxref", PdfTokenKind.StartXref)
    ]
    public void ReadNext_Keyword_ReturnsCorrectKind(string input, PdfTokenKind expected) =>
        Lex(input).ReadNext().Kind.ShouldBe(expected);

    // ── Whitespace and comments ───────────────────────────────────────────────

    [Fact]
    public void ReadNext_LeadingWhitespace_Skipped()
    {
        var token = Lex("   42").ReadNext();
        token.Kind.ShouldBe(PdfTokenKind.Integer);
        Raw(token).ShouldBe("42");
    }

    [Fact]
    public void ReadNext_Comment_Skipped()
    {
        var token = Lex("% this is a comment\n42").ReadNext();
        token.Kind.ShouldBe(PdfTokenKind.Integer);
        Raw(token).ShouldBe("42");
    }

    [Fact]
    public void ReadNext_EmptyInput_ReturnsEndOfFile() =>
        Lex(string.Empty).ReadNext().Kind.ShouldBe(PdfTokenKind.EndOfFile);

    [Fact]
    public void ReadNext_OnlyWhitespace_ReturnsEndOfFile() =>
        Lex("   \n\t  ").ReadNext().Kind.ShouldBe(PdfTokenKind.EndOfFile);

    // ── Position tracking ────────────────────────────────────────────────────

    [Fact]
    public void Offset_PointsToTokenStart()
    {
        var lexer = Lex("  42");
        var token = lexer.ReadNext();
        token.Offset.ShouldBe(2);
    }

    [Fact]
    public void AtEnd_False_WhenTokensRemain() =>
        Lex("42").AtEnd.ShouldBeFalse();

    [Fact]
    public void AtEnd_True_AfterAllTokensRead()
    {
        var lexer = Lex("x");
        lexer.ReadNext();
        lexer.AtEnd.ShouldBeTrue();
    }

    // ── Peek and Seek ─────────────────────────────────────────────────────────

    [Fact]
    public void Peek_DoesNotAdvancePosition()
    {
        var lexer = Lex("42 true");
        var before = lexer.Position;
        lexer.Peek();
        lexer.Position.ShouldBe(before);
    }

    [Fact]
    public void Peek_ReturnsNextToken()
    {
        var lexer = Lex("true");
        lexer.Peek().Kind.ShouldBe(PdfTokenKind.BooleanTrue);
        lexer.ReadNext().Kind.ShouldBe(PdfTokenKind.BooleanTrue);
    }

    [Fact]
    public void Seek_RepositionsCursor()
    {
        var lexer = Lex("42 99");
        lexer.ReadNext(); // consume 42
        lexer.Seek(0);
        Raw(lexer.ReadNext()).ShouldBe("42");
    }

    // ── Sequential reads ──────────────────────────────────────────────────────

    [Fact]
    public void ReadNext_MultipleTokens_ReadsInOrder()
    {
        var lexer = Lex("1 0 R");
        lexer.ReadNext().Kind.ShouldBe(PdfTokenKind.Integer);
        lexer.ReadNext().Kind.ShouldBe(PdfTokenKind.Integer);
        lexer.ReadNext().Kind.ShouldBe(PdfTokenKind.IndirectRef);
        lexer.ReadNext().Kind.ShouldBe(PdfTokenKind.EndOfFile);
    }
}
