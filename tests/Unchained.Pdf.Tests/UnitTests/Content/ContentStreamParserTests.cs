using System.Text;
using Shouldly;
using Unchained.Pdf.Content;
using Unchained.Pdf.Core;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Content;

public sealed class ContentStreamParserTests
{
    private static IReadOnlyList<Unchained.Pdf.Models.ContentOperator> Parse(string stream) =>
        ContentStreamParser.Parse(Encoding.Latin1.GetBytes(stream));

    // ── Zero-operand operators ────────────────────────────────────────────────

    [Fact]
    public void Parse_SingleZeroOperandOp_ReturnsOneOperator()
    {
        var ops = Parse("BT");
        ops.Count.ShouldBe(1);
        ops[0].Name.ShouldBe("BT");
        ops[0].Operands.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_TwoZeroOperandOps_ReturnsBoth()
    {
        var ops = Parse("BT ET");
        ops.Count.ShouldBe(2);
        ops[0].Name.ShouldBe("BT");
        ops[1].Name.ShouldBe("ET");
    }

    [Fact]
    public void Parse_EmptyStream_ReturnsEmpty() =>
        Parse("").ShouldBeEmpty();

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsEmpty() =>
        Parse("   \n\t  ").ShouldBeEmpty();

    // ── Integer operands ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_IntegerOperands_AssignedToFollowingOperator()
    {
        var ops = Parse("100 700 Td");
        ops.Count.ShouldBe(1);
        ops[0].Name.ShouldBe("Td");
        ops[0].Operands.Count.ShouldBe(2);
        ((PdfInteger)ops[0].Operands[0]).Value.ShouldBe(100);
        ((PdfInteger)ops[0].Operands[1]).Value.ShouldBe(700);
    }

    [Fact]
    public void Parse_RealOperands_ParsedAsReal()
    {
        var ops = Parse("1.0 0.0 0.0 1.0 50.5 700.0 cm");
        ops[0].Name.ShouldBe("cm");
        ops[0].Operands.Count.ShouldBe(6);
        foreach (var operand in ops[0].Operands)
            (operand is PdfReal or PdfInteger).ShouldBeTrue();
    }

    // ── String operands ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_LiteralString_ParsedAsOperand()
    {
        var ops = Parse("(Hello World) Tj");
        ops[0].Name.ShouldBe("Tj");
        var str = ops[0].Operands[0].ShouldBeOfType<PdfString>();
        str.IsHex.ShouldBeFalse();
    }

    [Fact]
    public void Parse_HexString_ParsedAsOperand()
    {
        var ops = Parse("<48656C6C6F> Tj");
        ops[0].Name.ShouldBe("Tj");
        ops[0].Operands[0].ShouldBeOfType<PdfString>();
    }

    // ── Name operands ─────────────────────────────────────────────────────────

    [Fact]
    public void Parse_NameOperand_ParsedAsPdfName()
    {
        var ops = Parse("/Helvetica 12 Tf");
        ops[0].Name.ShouldBe("Tf");
        ops[0].Operands.Count.ShouldBe(2);
        var name = ops[0].Operands[0].ShouldBeOfType<PdfName>();
        name.Value.ShouldBe("Helvetica");
        ((PdfInteger)ops[0].Operands[1]).Value.ShouldBe(12);
    }

    // ── Array operands (TJ) ───────────────────────────────────────────────────

    [Fact]
    public void Parse_ArrayOperand_ParsedAsPdfArray()
    {
        var ops = Parse("[(Hello) -20 (World)] TJ");
        ops[0].Name.ShouldBe("TJ");
        ops[0].Operands.Count.ShouldBe(1);
        var array = ops[0].Operands[0].ShouldBeOfType<PdfArray>();
        array.Count.ShouldBe(3);
        array[0].ShouldBeOfType<PdfString>();
        array[1].ShouldBeOfType<PdfInteger>();
        array[2].ShouldBeOfType<PdfString>();
    }

    [Fact]
    public void Parse_EmptyArray_ParsedCorrectly()
    {
        var ops = Parse("[] TJ");
        var array = ops[0].Operands[0].ShouldBeOfType<PdfArray>();
        array.Count.ShouldBe(0);
    }

    // ── Operand stack cleared between operators ───────────────────────────────

    [Fact]
    public void Parse_OperandsResetBetweenOperators()
    {
        var ops = Parse("1 2 Td (Hello) Tj");
        ops.Count.ShouldBe(2);
        ops[0].Name.ShouldBe("Td");
        ops[0].Operands.Count.ShouldBe(2);
        ops[1].Name.ShouldBe("Tj");
        ops[1].Operands.Count.ShouldBe(1);
    }

    // ── Realistic content stream ──────────────────────────────────────────────

    [Fact]
    public void Parse_TextBlock_ExtractsAllOperators()
    {
        const string stream = """
            BT
            /Helvetica 12 Tf
            100 700 Td
            (Hello, Unchained!) Tj
            ET
            """;

        var ops = Parse(stream);

        ops.Count.ShouldBe(5);
        ops.Select(static o => o.Name).ShouldBe(["BT", "Tf", "Td", "Tj", "ET"]);
    }

    [Fact]
    public void Parse_GraphicsAndText_ParsesAll()
    {
        const string stream = "q 1 0 0 1 50 700 cm BT (Hi) Tj ET Q";
        var ops = Parse(stream);
        ops.Select(static o => o.Name).ShouldBe(["q", "cm", "BT", "Tj", "ET", "Q"]);
    }

    // ── Comments ──────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_CommentsSkipped()
    {
        var ops = Parse("% this is a comment\nBT ET");
        ops.Count.ShouldBe(2);
        ops[0].Name.ShouldBe("BT");
    }

    // ── HasOperands ───────────────────────────────────────────────────────────

    [Fact]
    public void HasOperands_WithOperands_True()
    {
        var ops = Parse("100 700 Td");
        ops[0].HasOperands.ShouldBeTrue();
    }

    [Fact]
    public void HasOperands_WithoutOperands_False()
    {
        var ops = Parse("BT");
        ops[0].HasOperands.ShouldBeFalse();
    }
}
