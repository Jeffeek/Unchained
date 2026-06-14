using System.Buffers;
using System.Text;
using Shouldly;
using Unchained.Pdf.Engine;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Engine;

public sealed class ContentStreamWriterTests
{
    private static (ContentStreamWriter writer, ArrayBufferWriter<byte> buffer) CreateWriter()
    {
        var buffer = new ArrayBufferWriter<byte>();
        return (new ContentStreamWriter(buffer), buffer);
    }

    private static string Written(ArrayBufferWriter<byte> buffer) =>
        Encoding.Latin1.GetString(buffer.WrittenSpan);

    [Fact]
    public void Float_WritesFormattedValueWithTrailingSpace()
    {
        var (w, buf) = CreateWriter();
        w.Float(3.14f);
        Written(buf).ShouldBe("3.14 ");
    }

    [Fact]
    public void Float_NegativeValue_WritesMinusSign()
    {
        var (w, buf) = CreateWriter();
        w.Float(-1.5f);
        Written(buf).ShouldBe("-1.5 ");
    }

    [Fact]
    public void Float_ZeroValue_WritesZero()
    {
        var (w, buf) = CreateWriter();
        w.Float(0f);
        Written(buf).ShouldBe("0 ");
    }

    [Fact]
    public void Float_IntegerValue_WritesWithoutDecimalPoint()
    {
        var (w, buf) = CreateWriter();
        w.Float(100f);
        Written(buf).ShouldBe("100 ");
    }

    [Fact]
    public void Int_WritesIntegerWithTrailingSpace()
    {
        var (w, buf) = CreateWriter();
        w.Int(42);
        Written(buf).ShouldBe("42 ");
    }

    [Fact]
    public void Int_NegativeValue_WritesMinusSign()
    {
        var (w, buf) = CreateWriter();
        w.Int(-7);
        Written(buf).ShouldBe("-7 ");
    }

    [Fact]
    public void Int_Zero_WritesZero()
    {
        var (w, buf) = CreateWriter();
        w.Int(0);
        Written(buf).ShouldBe("0 ");
    }

    [Fact]
    public void Name_WritesSlashPrefixedNameWithTrailingSpace()
    {
        var (w, buf) = CreateWriter();
        w.Name("FlateDecode");
        Written(buf).ShouldBe("/FlateDecode ");
    }

    [Fact]
    public void Name_ShortName_CorrectOutput()
    {
        var (w, buf) = CreateWriter();
        w.Name("F1");
        Written(buf).ShouldBe("/F1 ");
    }

    [Fact]
    public void LiteralString_SimpleAscii_WritesParenthesizedWithTrailingSpace()
    {
        var (w, buf) = CreateWriter();
        w.LiteralString("Hello");
        Written(buf).ShouldBe("(Hello) ");
    }

    [Fact]
    public void LiteralString_Empty_WritesEmptyParens()
    {
        var (w, buf) = CreateWriter();
        w.LiteralString(string.Empty);
        Written(buf).ShouldBe("() ");
    }

    [Fact]
    public void LiteralString_ContainsOpenParen_Escaped()
    {
        var (w, buf) = CreateWriter();
        w.LiteralString("a(b");
        Written(buf).ShouldBe(@"(a\(b) ");
    }

    [Fact]
    public void LiteralString_ContainsCloseParen_Escaped()
    {
        var (w, buf) = CreateWriter();
        w.LiteralString("a)b");
        Written(buf).ShouldBe(@"(a\)b) ");
    }

    [Fact]
    public void LiteralString_ContainsBackslash_Escaped()
    {
        var (w, buf) = CreateWriter();
        w.LiteralString("a\\b");
        Written(buf).ShouldBe(@"(a\\b) ");
    }

    [Fact]
    public void LiteralString_NonLatin1Char_ReplacedWithQuestionMark()
    {
        var (w, buf) = CreateWriter();
        w.LiteralString("Ā"); // code point 256, above Latin-1 range
        Written(buf).ShouldBe("(?) ");
    }

    [Fact]
    public void LiteralString_Latin1MaxChar_Written()
    {
        var (w, buf) = CreateWriter();
        w.LiteralString("\xFF"); // 0xFF is within Latin-1
        var output = Written(buf);
        output.ShouldStartWith("(");
        output.ShouldEndWith(") ");
        output[1].ShouldBe('\xFF');
    }

    [Fact]
    public void LiteralString_AllSpecialChars_AllEscaped()
    {
        var (w, buf) = CreateWriter();
        w.LiteralString("()\\");
        Written(buf).ShouldBe(@"(\(\)\\) ");
    }

    [Fact]
    public void Op_WritesKeywordWithNewline()
    {
        var (w, buf) = CreateWriter();
        w.Op("BT"u8);
        Written(buf).ShouldBe("BT\n");
    }

    [Fact]
    public void Op_MultiByteKeyword_WritesCorrectly()
    {
        var (w, buf) = CreateWriter();
        w.Op("Tj"u8);
        Written(buf).ShouldBe("Tj\n");
    }

    [Fact]
    public void MultipleWritesCombined_ProduceCorrectContentStream()
    {
        var (w, buf) = CreateWriter();
        w.Op("BT"u8);
        w.Name("F1");
        w.Int(12);
        w.Op("Tf"u8);
        w.Float(100f);
        w.Float(700f);
        w.Op("Td"u8);
        w.LiteralString("Hello");
        w.Op("Tj"u8);
        w.Op("ET"u8);

        Written(buf).ShouldBe("BT\n/F1 12 Tf\n100 700 Td\n(Hello) Tj\nET\n");
    }

    [Fact]
    public void Float_G6Format_LargeValue_DoesNotUseScientificNotation()
    {
        // G6 with values that fit 6 significant digits should not use 'E' notation
        // for typical PDF coordinate values.
        var (w, buf) = CreateWriter();
        w.Float(595.276f);
        var output = Written(buf);
        output.ShouldNotContain("E");
        output.TrimEnd().Length.ShouldBeGreaterThan(0);
    }
}
