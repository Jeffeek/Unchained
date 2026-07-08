using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>Targets remaining FormulaEvaluator / FormulaTokenizer branches.</summary>
public class FormulaEngineExtraCoverageTests
{
    private static object? Eval(string formula, Action<SpreadsheetDocument>? setup = null)
    {
        using var document = XlsxFixtures.WithSheets("Sheet1", "Sheet2");
        setup?.Invoke(document);
        return SpreadsheetDocument.EvaluateFormula(document.Sheets[0], formula);
    }

    private static double? Num(string formula, Action<SpreadsheetDocument>? setup = null)
        => Eval(formula, setup) is double d ? d : null;

    // ── Unary operators ──────────────────────────────────────────────────────

    [Fact]
    public void UnaryPlus_OnReference() =>
        Num("=+A1", static d => d.Sheets[0].SetValue(1, 1, 7.0)).ShouldBe(7);

    [Fact]
    public void UnaryMinus_OnError_Propagates() =>
        Eval("=-(1/0)").ShouldBe(CellError.DivisionByZero);

    [Fact]
    public void Percent_OnReference() =>
        Num("=A1%", static d => d.Sheets[0].SetValue(1, 1, 50.0)).ShouldBe(0.5);

    // ── Whitespace & tokenizer edges ─────────────────────────────────────────

    [Fact]
    public void Whitespace_IsIgnored() =>
        Num("=  1  +  2  ").ShouldBe(3);

    [Fact]
    public void UnknownCharacter_IsSkipped() =>
        // A stray '~' is skipped by the tokenizer; "1~+~2" tokenizes as 1+2.
        Num("=1~+~2").ShouldBe(3);

    // ── Array coercion ───────────────────────────────────────────────────────

    [Fact]
    public void RangeInArithmetic_CoercesToFirstValue() =>
        Num(
                "=A1:A2+0",
                static d =>
                {
                    d.Sheets[0].SetValue(1, 1, 5.0);
                    d.Sheets[0].SetValue(2, 1, 9.0);
                }
            )
            .ShouldBe(5);

    [Fact]
    public void RangeInBooleanContext_UsesFirst() =>
        Eval(
                "=IF(A1:A2,\"yes\",\"no\")",
                static d =>
                {
                    d.Sheets[0].SetValue(1, 1, 1.0);
                    d.Sheets[0].SetValue(2, 1, 0.0);
                }
            )
            .ShouldBe("yes");

    // ── Sheet-qualified range ────────────────────────────────────────────────

    [Fact]
    public void SheetQualifiedRange_Sums() =>
        Num(
                "=SUM(Sheet2!A1:A2)",
                static d =>
                {
                    d.Sheets[1].SetValue(1, 1, 3.0);
                    d.Sheets[1].SetValue(2, 1, 4.0);
                }
            )
            .ShouldBe(7);

    // ── Defined names ────────────────────────────────────────────────────────

    [Fact]
    public void UnknownName_ReturnsNameError() => Eval("=NoSuchName+1").ShouldBe(CellError.Name);

    // ── ToText coercions via concatenation ───────────────────────────────────

    [Fact]
    public void Concat_BooleanRendersUpperCase() => Eval("=TRUE&\"\"").ShouldBe("TRUE");

    [Fact]
    public void Concat_BlankRendersEmpty() => Eval("=A1&\"end\"").ShouldBe("end");

    [Fact]
    public void Concat_NumberRendersInvariant() => Eval("=1.5&\"\"").ShouldBe("1.5");

    // ── Comparison with text vs number ───────────────────────────────────────

    [Fact]
    public void TextNumberComparison_FallsBackToText() =>
        Eval("=\"abc\"<5").ShouldBe(false); // text > number in Excel ordering → "abc" < 5 is false

    // ── Bad range node ───────────────────────────────────────────────────────

    [Fact]
    public void MalformedReference_InRange_ReturnsError() =>
        Eval("=SUM(A1:ZZZZ9)").ShouldBe(CellError.Reference);
}
