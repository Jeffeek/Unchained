using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Exercises the tokenizer + evaluator: operators, comparisons, concatenation, unary,
///     scientific notation, quoted strings, sheet-qualified references, defined names,
///     and circular-reference detection.
/// </summary>
public class FormulaEngineCoverageTests
{
    private static object? Eval(string formula, Action<SpreadsheetDocument>? setup = null)
    {
        using var document = XlsxFixtures.WithSheets("Sheet1", "Sheet2");
        setup?.Invoke(document);
        return SpreadsheetDocument.EvaluateFormula(document.Sheets[0], formula);
    }

    private static double? Num(string formula, Action<SpreadsheetDocument>? setup = null)
        => Eval(formula, setup) is double d ? d : null;

    // ── Arithmetic operators ─────────────────────────────────────────────────

    [
        Theory,
        InlineData("=1+2", 3),
        InlineData("=10-4", 6),
        InlineData("=3*4", 12),
        InlineData("=20/5", 4),
        InlineData("=2^8", 256),
        InlineData("=-5", -5),
        InlineData("=+5", 5),
        InlineData("=50%", 0.5),
        InlineData("=1+2*3", 7),
        InlineData("=(1+2)*3", 9),
        InlineData("=2^3^2", 512)
    ]
    public void Arithmetic(string formula, double expected) => Num(formula)!.Value.ShouldBe(expected, 1e-9);

    [Fact]
    public void DivideByZero_ReturnsError() => Eval("=5/0").ShouldBe(CellError.DivisionByZero);

    [Fact]
    public void ScientificNotation()
    {
        Num("=1.5e3").ShouldBe(1500);
        Num("=2E-2").ShouldBe(0.02);
        Num("=.5").ShouldBe(0.5);
    }

    // ── Concatenation & comparison ───────────────────────────────────────────

    [Fact]
    public void Concatenation_Operator() => Eval("=\"a\"&\"b\"&\"c\"").ShouldBe("abc");

    [Fact]
    public void Concatenation_MixesNumberAndText() => Eval("=\"x\"&5").ShouldBe("x5");

    [
        Theory,
        InlineData("=1=1", true),
        InlineData("=1<>2", true),
        InlineData("=1<2", true),
        InlineData("=2>1", true),
        InlineData("=2<=2", true),
        InlineData("=3>=4", false)
    ]
    public void NumericComparison(string formula, bool expected) => Eval(formula).ShouldBe(expected);

    [
        Theory,
        InlineData("=\"apple\"<\"banana\"", true),
        InlineData("=\"a\"=\"A\"", true),       // case-insensitive
        InlineData("=\"z\">\"a\"", true)
    ]
    public void TextComparison(string formula, bool expected) => Eval(formula).ShouldBe(expected);

    // ── String literal escapes ───────────────────────────────────────────────

    [Fact]
    public void QuotedString_WithEscapedQuotes() => Eval("=\"say \"\"hi\"\"\"").ShouldBe("say \"hi\"");

    // ── Error-literal tokens ─────────────────────────────────────────────────

    [
        Theory,
        InlineData("=#DIV/0!", CellError.DivisionByZero),
        InlineData("=#N/A", CellError.NotAvailable),
        InlineData("=#NAME?", CellError.Name),
        InlineData("=#NULL!", CellError.Null),
        InlineData("=#NUM!", CellError.Number),
        InlineData("=#REF!", CellError.Reference),
        InlineData("=#VALUE!", CellError.Value)
    ]
    public void ErrorLiterals(string formula, CellError expected) => Eval(formula).ShouldBe(expected);

    // ── References ───────────────────────────────────────────────────────────

    [Fact]
    public void CellReference_ReadsValue() =>
        Num("=A1*2", d => d.Sheets[0].SetValue(1, 1, 21.0)).ShouldBe(42);

    [Fact]
    public void RangeSum() =>
        Num("=SUM(A1:A3)", d =>
        {
            d.Sheets[0].SetValue(1, 1, 1.0);
            d.Sheets[0].SetValue(2, 1, 2.0);
            d.Sheets[0].SetValue(3, 1, 3.0);
        }).ShouldBe(6);

    [Fact]
    public void SheetQualifiedReference()
    {
        Num("=Sheet2!A1+1", d => d.Sheets[1].SetValue(1, 1, 99.0)).ShouldBe(100);
    }

    [Fact]
    public void QuotedSheetQualifiedReference()
    {
        using var document = XlsxFixtures.WithSheets("Main", "My Data");
        document.Sheets[1].SetValue(1, 1, 7.0);
        SpreadsheetDocument.EvaluateFormula(document.Sheets[0], "='My Data'!A1*2").ShouldBe(14.0);
    }

    [Fact]
    public void BooleanInArithmetic_CoercesToOneAndZero()
    {
        Num("=TRUE+TRUE").ShouldBe(2);
        Num("=FALSE+5").ShouldBe(5);
    }

    [Fact]
    public void TextInArithmetic_NumericString_Coerces() => Num("=\"3\"+\"4\"").ShouldBe(7);

    // ── Reference to formula cell & circular detection ───────────────────────

    [Fact]
    public void ReferenceToFormulaCell_EvaluatesChain()
    {
        Num("=A1+1", d =>
        {
            d.Sheets[0].SetValue(2, 1, 10.0);     // A2 = 10
            d.Sheets[0].SetFormula(1, 1, "=A2*2"); // A1 = A2*2 = 20
        }).ShouldBe(21);
    }

    [Fact]
    public void CircularReference_ReturnsReferenceError()
    {
        Eval("=A1", d => d.Sheets[0].SetFormula(1, 1, "=A1+1")).ShouldBe(CellError.Reference);
    }

    [Fact]
    public void ReferenceToErrorCell_PropagatesError()
    {
        Eval("=A1+1", d => d.Sheets[0][1, 1].SetValue(CellError.Value)).ShouldBe(CellError.Value);
    }

    // ── Defined names ────────────────────────────────────────────────────────

    [Fact]
    public void DefinedName_ResolvesToFormula()
    {
        using var document = XlsxFixtures.WithSheets("Sheet1");
        document.Sheets[0].SetValue(1, 1, 5.0);
        document.DefinedNames.Add("MyCell", "=Sheet1!$A$1");

        SpreadsheetDocument.EvaluateFormula(document.Sheets[0], "=MyCell*3").ShouldBe(15.0);
    }

    // ── Parse failure ────────────────────────────────────────────────────────

    [Fact]
    public void MalformedFormula_ReturnsNameError() => Eval("=1+").ShouldBe(CellError.Name);

    [Fact]
    public void EmptyParens_NonFunction_DoesNotThrow() => Should.NotThrow(() => Eval("=()"));
}
