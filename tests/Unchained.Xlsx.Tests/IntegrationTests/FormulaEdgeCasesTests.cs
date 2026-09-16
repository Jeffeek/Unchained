using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Unchained.Xlsx.Worksheets;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Tests for formula evaluation edge cases, error handling, and complex expressions.
///     Targets FormulaEvaluator and FormulaCalculator coverage gaps.
/// </summary>
public sealed class FormulaEdgeCasesTests
{
    private static object? Eval(string formula, Action<Worksheet>? setup = null)
    {
        using var document = XlsxFixtures.WithSheets("S");
        var sheet = document.Sheets[0];
        setup?.Invoke(sheet);
        return SpreadsheetDocument.EvaluateFormula(sheet, formula);
    }

    private static double? Num(string formula, Action<Worksheet>? setup = null)
        => Eval(formula, setup) is double d ? d : null;

    [Fact]
    public void Formula_DivisionByZero_ReturnsError() => Eval("=1/0").ShouldBe(CellError.DivisionByZero);

    [Fact]
    public void Formula_CircularReference_HandledGracefully()
    {
        using var processor = new SpreadsheetProcessor();
        using var doc = processor.CreateBlank("Sheet1");
        var sheet = doc.Sheets[0];

        sheet.SetFormula(1, 1, "=A1+1"); // Self-reference
        doc.Recalculate();

        // Should handle without infinite loop
        var cell = sheet.GetCell(1, 1);
        cell.ShouldNotBeNull();
    }

    [Fact]
    public void Formula_NestedFunctions_EvaluatesCorrectly() => Num("=SUM(1,2,MAX(3,4,5))").ShouldBe(8.0); // 1+2+5

    [Fact]
    public void Formula_ArrayInFunction_WorksCorrectly() => Num("=SUM({1,2,3,4})").ShouldBe(10.0);

    [Fact]
    public void Formula_RangeAcrossMultipleCells_Sums()
    {
        var result = Num(
            "=SUM(A1:A3)",
            static sheet =>
            {
                sheet.SetValue(1, 1, 10);
                sheet.SetValue(2, 1, 20);
                sheet.SetValue(3, 1, 30);
            }
        );

        result.ShouldBe(60.0);
    }

    [Fact]
    public void Formula_EmptyCellReference_TreatsAsZero() => Num("=A1+5").ShouldBe(5.0); // A1 is empty = 0

    [Fact]
    public void Formula_TextConcatenation_Works()
    {
        var result = Eval("=\"Hello\" & \" \" & \"World\"");
        result.ShouldBe("Hello World");
    }

    [Fact]
    public void Formula_ComparisonOperators_ReturnBoolean()
    {
        Eval("=5>3").ShouldBe(true);
        Eval("=5<3").ShouldBe(false);
        Eval("=5=5").ShouldBe(true);
        Eval("=5<>5").ShouldBe(false);
        Eval("=5>=5").ShouldBe(true);
        Eval("=5<=4").ShouldBe(false);
    }

    [Fact]
    public void Formula_BooleanLogic_AND_OR()
    {
        Eval("=AND(TRUE,TRUE)").ShouldBe(true);
        Eval("=AND(TRUE,FALSE)").ShouldBe(false);
        Eval("=OR(FALSE,TRUE)").ShouldBe(true);
        Eval("=OR(FALSE,FALSE)").ShouldBe(false);
    }

    [Fact]
    public void Formula_NOT_InvertsBoolean()
    {
        Eval("=NOT(TRUE)").ShouldBe(false);
        Eval("=NOT(FALSE)").ShouldBe(true);
    }

    [Fact]
    public void Formula_IF_BranchesCorrectly()
    {
        Num("=IF(5>3,10,20)").ShouldBe(10.0);
        Num("=IF(5<3,10,20)").ShouldBe(20.0);
    }

    [Fact]
    public void Formula_NestedIF_EvaluatesCorrectly() => Num("=IF(FALSE,1,IF(TRUE,2,3))").ShouldBe(2.0);

    [Fact]
    public void Formula_IFERROR_CatchesErrors()
    {
        Num("=IFERROR(1/0,999)").ShouldBe(999.0);
        Num("=IFERROR(1/2,999)").ShouldBe(0.5);
    }

    [Fact]
    public void Formula_IFNA_CatchesNAError()
    {
        Num("=IFNA(NA(),100)").ShouldBe(100.0);
        Num("=IFNA(42,100)").ShouldBe(42.0);
    }

    [Fact]
    public void Formula_COUNTIF_CountsMatching()
    {
        var result = Num(
            "=COUNTIF(A1:A4,\">15\")",
            static sheet =>
            {
                sheet.SetValue(1, 1, 10);
                sheet.SetValue(2, 1, 20);
                sheet.SetValue(3, 1, 30);
                sheet.SetValue(4, 1, 5);
            }
        );

        result.ShouldBe(2.0); // 20 and 30 are > 15
    }

    [Fact]
    public void Formula_SUMIF_SumsMatching()
    {
        var result = Num(
            "=SUMIF(A1:A4,\">10\")",
            static sheet =>
            {
                sheet.SetValue(1, 1, 5);
                sheet.SetValue(2, 1, 15);
                sheet.SetValue(3, 1, 25);
                sheet.SetValue(4, 1, 8);
            }
        );

        result.ShouldBe(40.0); // 15 + 25
    }

    [Fact]
    public void Formula_ABS_ReturnsAbsoluteValue()
    {
        Num("=ABS(-5)").ShouldBe(5.0);
        Num("=ABS(5)").ShouldBe(5.0);
    }

    [Fact]
    public void Formula_ROUND_RoundsToDigits()
    {
        Num("=ROUND(3.14159,2)").ShouldBe(3.14);
        Num("=ROUND(3.14159,0)").ShouldBe(3.0);
    }

    [Fact]
    public void Formula_MOD_ReturnsRemainder()
    {
        Num("=MOD(10,3)").ShouldBe(1.0);
        Num("=MOD(10,5)").ShouldBe(0.0);
    }

    [Fact]
    public void Formula_POWER_RaisesToPower()
    {
        Num("=POWER(2,3)").ShouldBe(8.0);
        Num("=POWER(5,2)").ShouldBe(25.0);
    }

    [Fact]
    public void Formula_SQRT_ReturnsSquareRoot()
    {
        Num("=SQRT(16)").ShouldBe(4.0);
        Num("=SQRT(25)").ShouldBe(5.0);
    }

    [Fact]
    public void Formula_MIN_MAX_FindExtremes()
    {
        Num("=MIN(5,2,8,1)").ShouldBe(1.0);
        Num("=MAX(5,2,8,1)").ShouldBe(8.0);
    }

    [Fact]
    public void Formula_AVERAGE_CalculatesMean() => Num("=AVERAGE(10,20,30)").ShouldBe(20.0);

    [Fact]
    public void Formula_COUNT_CountsNumbers()
    {
        var result = Num(
            "=COUNT(A1:A4)",
            static sheet =>
            {
                sheet.SetValue(1, 1, 10);
                sheet.SetValue(2, 1, "text");
                sheet.SetValue(3, 1, 20);
                sheet.SetValue(4, 1, 30);
            }
        );

        result.ShouldBe(3.0); // Only numbers
    }

    [Fact]
    public void Formula_COUNTA_CountsNonEmpty()
    {
        var result = Num(
            "=COUNTA(A1:A4)",
            static sheet =>
            {
                sheet.SetValue(1, 1, 10);
                sheet.SetValue(2, 1, "text");
                sheet.SetValue(3, 1, 20);
                // A4 is empty
            }
        );

        result.ShouldBe(3.0); // All non-empty cells
    }

    [Fact]
    public void Formula_LEN_ReturnsTextLength()
    {
        Num("=LEN(\"Hello\")").ShouldBe(5.0);
        Num("=LEN(\"\")").ShouldBe(0.0);
    }

    [Fact]
    public void Formula_TRIM_RemovesExtraSpaces() => Eval("=TRIM(\"  Hello   World  \")").ShouldBe("Hello World");

    [Fact]
    public void Formula_UPPER_LOWER_ChangeCase()
    {
        Eval("=UPPER(\"hello\")").ShouldBe("HELLO");
        Eval("=LOWER(\"HELLO\")").ShouldBe("hello");
    }

    [Fact]
    public void Formula_CONCATENATE_JoinsText() => Eval("=CONCATENATE(\"A\",\"B\",\"C\")").ShouldBe("ABC");

    [Fact]
    public void Formula_LEFT_RIGHT_ExtractSubstring()
    {
        Eval("=LEFT(\"Hello\",2)").ShouldBe("He");
        Eval("=RIGHT(\"Hello\",2)").ShouldBe("lo");
    }

    [Fact]
    public void Formula_MID_ExtractsMiddle() => Eval("=MID(\"Hello\",2,3)").ShouldBe("ell");

    [Fact]
    public void Formula_ISBLANK_DetectsEmpty()
    {
        Eval("=ISBLANK(A1)").ShouldBe(true);

        var result = Eval(
            "=ISBLANK(A1)",
            static sheet =>
            {
                sheet.SetValue(1, 1, "value");
            }
        );

        result.ShouldBe(false);
    }

    [Fact]
    public void Formula_ISNUMBER_DetectsNumber()
    {
        Eval("=ISNUMBER(42)").ShouldBe(true);
        Eval("=ISNUMBER(\"text\")").ShouldBe(false);
    }

    [Fact]
    public void Formula_ISTEXT_DetectsText()
    {
        Eval("=ISTEXT(\"hello\")").ShouldBe(true);
        Eval("=ISTEXT(42)").ShouldBe(false);
    }

    [Fact]
    public void Formula_ISERROR_DetectsError()
    {
        Eval("=ISERROR(1/0)").ShouldBe(true);
        Eval("=ISERROR(42)").ShouldBe(false);
    }
}
