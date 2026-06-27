using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Unchained.Xlsx.Worksheets;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class FormulaEvaluationTests
{
    private static double? Eval(string formula, Action<Worksheet>? setup = null)
    {
        using var document = XlsxFixtures.WithSheets("S");
        var sheet = document.Sheets[0];
        setup?.Invoke(sheet);
        var result = SpreadsheetDocument.EvaluateFormula(sheet, formula);
        return result is double d ? d : null;
    }

    [
        Theory,
        InlineData("=1+2", 3),
        InlineData("=2*3+4", 10),
        InlineData("=2+3*4", 14),
        InlineData("=(2+3)*4", 20),
        InlineData("=2^10", 1024),
        InlineData("=10/4", 2.5),
        InlineData("=-5+3", -2),
        InlineData("=10%", 0.1),
        InlineData("=2^3^2", 512)
    ]
    public void Arithmetic(string formula, double expected) =>
        Eval(formula)!.Value.ShouldBe(expected, 1e-9);

    [
        Theory,
        InlineData("=SUM(1,2,3,4)", 10),
        InlineData("=AVERAGE(2,4,6)", 4),
        InlineData("=MIN(5,2,8)", 2),
        InlineData("=MAX(5,2,8)", 8),
        InlineData("=COUNT(1,2,\"x\",4)", 3),
        InlineData("=PRODUCT(2,3,4)", 24),
        InlineData("=ROUND(3.14159,2)", 3.14),
        InlineData("=ABS(-7)", 7),
        InlineData("=MOD(10,3)", 1),
        InlineData("=POWER(2,8)", 256),
        InlineData("=SQRT(144)", 12),
        InlineData("=INT(7.9)", 7)
    ]
    public void MathFunctions(string formula, double expected) =>
        Eval(formula)!.Value.ShouldBe(expected, 1e-9);

    [Fact]
    public void If_ChoosesBranch()
    {
        Eval("=IF(1>0,10,20)").ShouldBe(10);
        Eval("=IF(1>2,10,20)").ShouldBe(20);
    }

    [Fact]
    public void RangeAggregation_OverCells() =>
        Eval(
                "=SUM(A1:A3)",
                static s =>
                {
                    s.SetValue(1, 1, 10.0);
                    s.SetValue(2, 1, 20.0);
                    s.SetValue(3, 1, 30.0);
                }
            )
            .ShouldBe(60);

    [Fact]
    public void CellReferences_Resolve() =>
        Eval(
                "=A1*B1+5",
                static s =>
                {
                    s.SetValue(1, 1, 4.0);
                    s.SetValue(1, 2, 3.0);
                }
            )
            .ShouldBe(17);

    [Fact]
    public void SumIf_FiltersByCriterion() =>
        Eval(
                "=SUMIF(A1:A4,\">15\")",
                static s =>
                {
                    s.SetValue(1, 1, 10.0);
                    s.SetValue(2, 1, 20.0);
                    s.SetValue(3, 1, 30.0);
                    s.SetValue(4, 1, 5.0);
                }
            )
            .ShouldBe(50);

    [Fact]
    public void CountIf_CountsMatches() =>
        Eval(
                "=COUNTIF(A1:A4,\">15\")",
                static s =>
                {
                    s.SetValue(1, 1, 10.0);
                    s.SetValue(2, 1, 20.0);
                    s.SetValue(3, 1, 30.0);
                    s.SetValue(4, 1, 5.0);
                }
            )
            .ShouldBe(2);

    [Fact]
    public void TextFunctions()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var sheet = document.Sheets[0];
        SpreadsheetDocument.EvaluateFormula(sheet, "=CONCATENATE(\"a\",\"b\",\"c\")").ShouldBe("abc");
        SpreadsheetDocument.EvaluateFormula(sheet, "=UPPER(\"hello\")").ShouldBe("HELLO");
        SpreadsheetDocument.EvaluateFormula(sheet, "=LEN(\"hello\")").ShouldBe(5.0);
        SpreadsheetDocument.EvaluateFormula(sheet, "=LEFT(\"hello\",2)").ShouldBe("he");
        SpreadsheetDocument.EvaluateFormula(sheet, "=MID(\"hello\",2,3)").ShouldBe("ell");
        SpreadsheetDocument.EvaluateFormula(sheet, "=\"x\"&\"y\"").ShouldBe("xy");
    }

    [Fact]
    public void DivisionByZero_ReturnsError()
    {
        using var document = XlsxFixtures.WithSheets("S");
        SpreadsheetDocument.EvaluateFormula(document.Sheets[0], "=1/0").ShouldBe(CellError.DivisionByZero);
    }

    [Fact]
    public void UnknownFunction_ReturnsNameError()
    {
        using var document = XlsxFixtures.WithSheets("S");
        SpreadsheetDocument.EvaluateFormula(document.Sheets[0], "=BOGUS(1)").ShouldBe(CellError.Name);
    }

    [Fact]
    public void Comparison_ReturnsBoolean()
    {
        using var document = XlsxFixtures.WithSheets("S");
        SpreadsheetDocument.EvaluateFormula(document.Sheets[0], "=5>3").ShouldBe(true);
        SpreadsheetDocument.EvaluateFormula(document.Sheets[0], "=5<3").ShouldBe(false);
        SpreadsheetDocument.EvaluateFormula(document.Sheets[0], "=2=2").ShouldBe(true);
    }

    [Fact]
    public void NestedFunctions()
    {
        Eval("=ROUND(AVERAGE(1,2,3,4),0)").ShouldBe(3);
        Eval("=IF(SUM(1,2)>2,MAX(10,20),0)").ShouldBe(20);
    }

    [Fact]
    public void IfError_CatchesError()
    {
        Eval("=IFERROR(1/0,99)").ShouldBe(99);
        Eval("=IFERROR(10,99)").ShouldBe(10);
    }
}
