using System.Buffers;
using System.Text;
using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Writing;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Writing;

public sealed class PdfWriterValueTests
{
    private static string Write(PdfObject obj)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new PdfWriter(buffer);
        writer.WriteValue(obj);
        return Encoding.Latin1.GetString(buffer.WrittenSpan);
    }

    [Fact]
    public void WriteValue_True_WritesTrue() =>
        Write(PdfBoolean.True).ShouldBe("true");

    [Fact]
    public void WriteValue_False_WritesFalse() =>
        Write(PdfBoolean.False).ShouldBe("false");

    [Fact]
    public void WriteValue_Null_WritesNull() =>
        Write(PdfNull.Instance).ShouldBe("null");

    [
        Theory,
        InlineData(0L, "0"),
        InlineData(42L, "42"),
        InlineData(-7L, "-7")
    ]
    public void WriteValue_Integer_WritesDecimal(long value, string expected) =>
        Write(new PdfInteger(value)).ShouldBe(expected);

    [Fact]
    public void WriteValue_Name_WritesWithSlash() =>
        Write(PdfName.Type).ShouldBe("/Type");

    [Fact]
    public void WriteValue_LiteralString_WritesWithParentheses()
    {
        var result = Write(PdfString.FromLatin1("Hi"));
        result.ShouldBe("(Hi)");
    }

    [Fact]
    public void WriteValue_LiteralString_EscapesSpecialChars()
    {
        var s = PdfString.FromLatin1("(hello)");
        var result = Write(s);
        result.ShouldContain("\\(");
        result.ShouldContain("\\)");
    }

    [Fact]
    public void WriteValue_HexString_WritesWithAngleBrackets()
    {
        var s = new PdfString("Hi"u8.ToArray(), isHex: true);
        Write(s).ShouldBe("<4869>");
    }

    [Fact]
    public void WriteValue_EmptyArray_WritesEmptyBrackets() =>
        Write(PdfArray.Empty).ShouldBe("[]");

    [Fact]
    public void WriteValue_Array_WritesElementsSeparatedBySpace()
    {
        var array = new PdfArray([new PdfInteger(1), new PdfInteger(2)]);
        Write(array).ShouldBe("[1 2]");
    }

    [Fact]
    public void WriteValue_EmptyDictionary_WritesEmptyDelimiters() =>
        Write(new PdfDictionary()).ShouldBe("<<\n>>");

    [Fact]
    public void WriteValue_Dictionary_WritesNameValuePairs()
    {
        var dict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.Page
        });
        var result = Write(dict);
        result.ShouldContain("/Type");
        result.ShouldContain("/Page");
    }

    [Fact]
    public void WriteValue_IndirectReference_WritesSyntax() =>
        Write(new PdfIndirectReference(5, 0)).ShouldBe("5 0 R");

    [Fact]
    public void WriteValue_UnknownType_ThrowsPdfException()
    {
        // Create an anonymous subtype that PdfWriter doesn't know about
        var unknown = new UnknownPdfObject();
        Should.Throw<PdfException>(() => Write(unknown));
    }

    private sealed class UnknownPdfObject : PdfObject;
}

public sealed class PdfWriterDocumentTests
{
    [Fact]
    public void Write_MinimalDocument_StartsWithPdfHeader()
    {
        var bytes = WriteMinimalDocument();
        Encoding.Latin1.GetString(bytes.Span[..7]).ShouldBe("%PDF-1.");
    }

    [Fact]
    public void Write_MinimalDocument_ContainsXref()
    {
        var bytes = WriteMinimalDocument();
        Encoding.Latin1.GetString(bytes.Span).ShouldContain("xref");
    }

    [Fact]
    public void Write_MinimalDocument_ContainsTrailer()
    {
        var bytes = WriteMinimalDocument();
        Encoding.Latin1.GetString(bytes.Span).ShouldContain("trailer");
    }

    [Fact]
    public void Write_MinimalDocument_EndsWithEof()
    {
        var bytes = WriteMinimalDocument();
        Encoding.Latin1.GetString(bytes.Span).ShouldContain("%%EOF");
    }

    [Fact]
    public void WriteIndirectObject_RecordsOffset()
    {
        // The first indirect object should appear after the header (~20 bytes)
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new PdfWriter(buffer);
        writer.WriteIndirectObject(new PdfIndirectObject(1, 0, PdfNull.Instance));
        var content = Encoding.Latin1.GetString(buffer.WrittenSpan);
        content.ShouldContain("1 0 obj");
        content.ShouldContain("endobj");
    }

    private static ReadOnlyMemory<byte> WriteMinimalDocument()
    {
        var catalog = new PdfIndirectObject(
            1,
            0,
            new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["Type"] = PdfName.Catalog,
                ["Pages"] = new PdfIndirectReference(2, 0)
            }));
        var pages = new PdfIndirectObject(
            2,
            0,
            new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["Type"] = PdfName.Pages,
                ["Kids"] = new PdfArray([new PdfIndirectReference(3, 0)]),
                ["Count"] = new PdfInteger(1)
            }));
        var page = new PdfIndirectObject(
            3,
            0,
            new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["Type"] = PdfName.Page,
                ["Parent"] = new PdfIndirectReference(2, 0),
                ["MediaBox"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(595), new PdfInteger(842)])
            }));

        var trailer = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Size"] = new PdfInteger(4),
            ["Root"] = new PdfIndirectReference(1, 0)
        });

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new PdfWriter(buffer);
        writer.Write([catalog, pages, page], trailer);

        return buffer.WrittenMemory;
    }
}
