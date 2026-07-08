using System.Text;
using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Parsing;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Parsing;

public sealed class PdfParserReadValueTests
{
    private static PdfObject ReadValue(string input)
    {
        var source = (ReadOnlyMemory<byte>)Encoding.Latin1.GetBytes(input);
        var parser = new PdfParser(source);
        var lexer = new Lexer(source);
        return parser.ReadValue(lexer);
    }

    [Fact]
    public void ReadValue_True_ReturnsTrueSingleton() =>
        ReadValue("true").ShouldBeSameAs(PdfBoolean.True);

    [Fact]
    public void ReadValue_False_ReturnsFalseSingleton() =>
        ReadValue("false").ShouldBeSameAs(PdfBoolean.False);

    [Fact]
    public void ReadValue_Null_ReturnsNullInstance() =>
        ReadValue("null").ShouldBeSameAs(PdfNull.Instance);

    [
        Theory,
        InlineData("42", 42L),
        InlineData("-7", -7L),
        InlineData("0", 0L)
    ]
    public void ReadValue_Integer_ReturnsCorrectValue(string input, long expected) =>
        ((PdfInteger)ReadValue(input)).Value.ShouldBe(expected);

    [
        Theory,
        InlineData("3.14", 3.14),
        InlineData("-.5", -0.5)
    ]
    public void ReadValue_Real_ReturnsCorrectValue(string input, double expected) =>
        ((PdfReal)ReadValue(input)).Value.ShouldBe(expected, 0.0001);

    [Fact]
    public void ReadValue_Name_ReturnsInternedPdfName() =>
        ((PdfName)ReadValue("/Type")).ShouldBeSameAs(PdfName.Type);

    [Fact]
    public void ReadValue_LiteralString_ReturnsPdfString()
    {
        var result = (PdfString)ReadValue("(Hello)");
        result.IsHex.ShouldBeFalse();
    }

    [Fact]
    public void ReadValue_HexString_ReturnsPdfStringWithIsHexTrue()
    {
        var result = (PdfString)ReadValue("<48656C6C6F>");
        result.IsHex.ShouldBeTrue();
    }

    [Fact]
    public void ReadValue_EmptyArray_ReturnsEmptyPdfArray()
    {
        var result = (PdfArray)ReadValue("[]");
        result.Count.ShouldBe(0);
    }

    [Fact]
    public void ReadValue_Array_ReturnsCorrectElements()
    {
        var result = (PdfArray)ReadValue("[1 2 3]");
        result.Count.ShouldBe(3);
        ((PdfInteger)result[0]).Value.ShouldBe(1);
        ((PdfInteger)result[1]).Value.ShouldBe(2);
        ((PdfInteger)result[2]).Value.ShouldBe(3);
    }

    [Fact]
    public void ReadValue_NestedArray_ParsesNested()
    {
        var result = (PdfArray)ReadValue("[[1 2] [3]]");
        result.Count.ShouldBe(2);
        ((PdfArray)result[0]).Count.ShouldBe(2);
        ((PdfArray)result[1]).Count.ShouldBe(1);
    }

    [Fact]
    public void ReadValue_EmptyDictionary_ReturnsEmptyPdfDictionary()
    {
        var result = (PdfDictionary)ReadValue("<<>>");
        result.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void ReadValue_Dictionary_ParsesEntries()
    {
        var result = (PdfDictionary)ReadValue("<< /Type /Page /Count 5 >>");
        result.GetName("Type").ShouldBe("Page");
        result.Get<PdfInteger>("Count")!.Value.ShouldBe(5);
    }

    [Fact]
    public void ReadValue_IndirectReference_ReturnsPdfIndirectReference()
    {
        var result = (PdfIndirectReference)ReadValue("3 0 R");
        result.ObjectNumber.ShouldBe(3);
        result.Generation.ShouldBe(0);
    }

    [Fact]
    public void ReadValue_TwoIntegers_NotConfusedWithReference()
    {
        // "1 2" should produce first integer only, not a reference
        var source = (ReadOnlyMemory<byte>)Encoding.Latin1.GetBytes("1 2");
        var parser = new PdfParser(source);
        var lexer = new Lexer(source);
        var first = parser.ReadValue(lexer);
        first.ShouldBeOfType<PdfInteger>();
        ((PdfInteger)first).Value.ShouldBe(1);
    }

    [Fact]
    public void ReadValue_UnexpectedToken_ThrowsPdfException()
    {
        var ex = Should.Throw<PdfException>(static () => ReadValue("endobj"));
        ex.ShouldNotBeNull();
    }
}

public sealed class PdfParserStructureTests
{
    [Fact]
    public void ParseStructure_SinglePage_ReturnsXrefAndTrailer()
    {
        var source = (ReadOnlyMemory<byte>)PdfFixtures.SinglePage();
        var parser = new PdfParser(source);
        var (xref, trailer) = parser.ParseStructure();

        xref.ShouldNotBeNull();
        trailer.ShouldNotBeNull();
        trailer["Root"].ShouldNotBeNull();
        trailer["Root"].ShouldBeOfType<PdfIndirectReference>();
        trailer["Size"].ShouldNotBeNull();
    }

    [Fact]
    public void ParseStructure_MultiPage_XrefContainsAllObjects()
    {
        var source = (ReadOnlyMemory<byte>)PdfFixtures.MultiPage(3);
        var parser = new PdfParser(source);
        var (xref, _) = parser.ParseStructure();

        // 1=Catalog, 2=Pages, 3/4/5=Page nodes => 5 objects + free object 0
        xref.Count.ShouldBeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public void ParseStructure_MissingStartXref_ThrowsPdfException()
    {
        var source = (ReadOnlyMemory<byte>)Encoding.Latin1.GetBytes("%PDF-1.7\n");
        var parser = new PdfParser(source);
        Should.Throw<PdfException>(() => parser.ParseStructure());
    }

    [Fact]
    public void ReadObject_FirstObject_ReturnsCatalog()
    {
        var bytes = PdfFixtures.SinglePage();
        var source = (ReadOnlyMemory<byte>)bytes;
        var parser = new PdfParser(source);
        var (xref, _) = parser.ParseStructure();

        var entry = xref.GetEntry(1);
        var obj = parser.ReadObject(entry.Offset);

        obj.ObjectNumber.ShouldBe(1);
        obj.Generation.ShouldBe(0);
        var dict = obj.Value.ShouldBeOfType<PdfDictionary>();
        dict.GetName("Type").ShouldBe("Catalog");
    }

    [Fact]
    public void ReadObject_PageNode_HasMediaBox()
    {
        var bytes = PdfFixtures.SinglePage();
        var source = (ReadOnlyMemory<byte>)bytes;
        var parser = new PdfParser(source);
        var (xref, _) = parser.ParseStructure();

        // Object 3 = first page
        var entry = xref.GetEntry(3);
        var obj = parser.ReadObject(entry.Offset);
        var page = obj.Value.ShouldBeOfType<PdfDictionary>();
        page["MediaBox"].ShouldNotBeNull();
    }
}
