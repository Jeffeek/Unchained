using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Unchained.Xlsx.Worksheets;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Comprehensive tests for lookup functions (VLOOKUP, HLOOKUP, INDEX, MATCH, XLOOKUP)
///     targeting edge cases and missing coverage branches.
/// </summary>
public sealed class LookupFunctionTests
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

    private static string? Text(string formula, Action<Worksheet>? setup = null)
        => Eval(formula, setup)?.ToString();

    [Fact]
    public void VLookup_ExactMatch_ReturnsCorrectValue()
    {
        var result = Num(
            "=VLOOKUP(\"B\",A2:B3,2,FALSE)",
            static sheet =>
            {
                sheet.SetValue(2, 1, "A");
                sheet.SetValue(2, 2, 100);
                sheet.SetValue(3, 1, "B");
                sheet.SetValue(3, 2, 200);
            }
        );

        result.ShouldBe(200.0);
    }

    [Fact]
    public void VLookup_ApproximateMatch_FindsLargestLessThanOrEqual()
    {
        var result = Text(
            "=VLOOKUP(60,A1:B3,2,TRUE)",
            static sheet =>
            {
                sheet.SetValue(1, 1, 10);
                sheet.SetValue(1, 2, "Low");
                sheet.SetValue(2, 1, 50);
                sheet.SetValue(2, 2, "Mid");
                sheet.SetValue(3, 1, 100);
                sheet.SetValue(3, 2, "High");
            }
        );

        result.ShouldBe("Mid");
    }

    [Fact]
    public void VLookup_KeyNotFound_ReturnsNA()
    {
        var result = Eval(
            "=VLOOKUP(\"Z\",A1:B2,2,FALSE)",
            static sheet =>
            {
                sheet.SetValue(1, 1, "A");
                sheet.SetValue(1, 2, 1);
                sheet.SetValue(2, 1, "B");
                sheet.SetValue(2, 2, 2);
            }
        );

        result.ShouldBe(CellError.NotAvailable);
    }

    [Fact]
    public void VLookup_InvalidColumnIndex_ReturnsREF()
    {
        var result = Eval(
            "=VLOOKUP(\"A\",A1:B1,5,FALSE)",
            static sheet =>
            {
                sheet.SetValue(1, 1, "A");
                sheet.SetValue(1, 2, 1);
            }
        );

        result.ShouldBe(CellError.Reference);
    }

    [Fact]
    public void VLookup_TooFewArgs_ReturnsVALUE()
    {
        var result = Eval("=VLOOKUP(\"A\",A1:B2)");
        result.ShouldBe(CellError.Value);
    }

    [Fact]
    public void VLookup_NonArrayRange_ReturnsNA()
    {
        var result = Eval(
            "=VLOOKUP(\"A\",A1,2,FALSE)",
            static sheet =>
            {
                sheet.SetValue(1, 1, "A");
            }
        );

        result.ShouldBe(CellError.NotAvailable);
    }

    [Fact]
    public void HLookup_ExactMatch_ReturnsCorrectValue()
    {
        var result = Num(
            "=HLOOKUP(\"B\",A1:C2,2,FALSE)",
            static sheet =>
            {
                sheet.SetValue(1, 1, "A");
                sheet.SetValue(1, 2, "B");
                sheet.SetValue(1, 3, "C");
                sheet.SetValue(2, 1, 100);
                sheet.SetValue(2, 2, 200);
                sheet.SetValue(2, 3, 300);
            }
        );

        result.ShouldBe(200.0);
    }

    [Fact]
    public void HLookup_KeyNotFound_ReturnsNA()
    {
        var result = Eval(
            "=HLOOKUP(\"Z\",A1:B2,2,FALSE)",
            static sheet =>
            {
                sheet.SetValue(1, 1, "A");
                sheet.SetValue(1, 2, "B");
                sheet.SetValue(2, 1, 1);
                sheet.SetValue(2, 2, 2);
            }
        );

        result.ShouldBe(CellError.NotAvailable);
    }

    [Fact]
    public void Index_SingleRowArray_WithOneIndex_ReturnsElement()
    {
        var result = Num(
            "=INDEX(A1:C1,2)",
            static sheet =>
            {
                sheet.SetValue(1, 1, 10);
                sheet.SetValue(1, 2, 20);
                sheet.SetValue(1, 3, 30);
            }
        );

        result.ShouldBe(20.0);
    }

    [Fact]
    public void Index_SingleColumnArray_WithOneIndex_ReturnsElement()
    {
        var result = Num(
            "=INDEX(A1:A3,2)",
            static sheet =>
            {
                sheet.SetValue(1, 1, 10);
                sheet.SetValue(2, 1, 20);
                sheet.SetValue(3, 1, 30);
            }
        );

        result.ShouldBe(20.0);
    }

    [Fact]
    public void Index_2DArray_WithRowAndColumn_ReturnsElement()
    {
        var result = Num(
            "=INDEX(A1:B2,2,1)",
            static sheet =>
            {
                sheet.SetValue(1, 1, 1);
                sheet.SetValue(1, 2, 2);
                sheet.SetValue(2, 1, 3);
                sheet.SetValue(2, 2, 4);
            }
        );

        result.ShouldBe(3.0);
    }

    [Fact]
    public void Index_OutOfRange_ReturnsREF()
    {
        var result = Eval(
            "=INDEX(A1:A2,5)",
            static sheet =>
            {
                sheet.SetValue(1, 1, 10);
                sheet.SetValue(2, 1, 20);
            }
        );

        result.ShouldBe(CellError.Reference);
    }

    [Fact]
    public void Index_ZeroRowIndex_ReturnsREF()
    {
        var result = Eval(
            "=INDEX(A1:B2,0,1)",
            static sheet =>
            {
                sheet.SetValue(1, 1, 1);
                sheet.SetValue(1, 2, 2);
            }
        );

        result.ShouldBe(CellError.Reference);
    }

    [Fact]
    public void Match_ExactMatch_ReturnsPosition()
    {
        var result = Num(
            "=MATCH(\"Banana\",A1:A3,0)",
            static sheet =>
            {
                sheet.SetValue(1, 1, "Apple");
                sheet.SetValue(2, 1, "Banana");
                sheet.SetValue(3, 1, "Cherry");
            }
        );

        result.ShouldBe(2.0);
    }

    [Fact]
    public void Match_ApproximateLessThanOrEqual_FindsLargest()
    {
        var result = Num(
            "=MATCH(25,A1:A3,1)",
            static sheet =>
            {
                sheet.SetValue(1, 1, 10);
                sheet.SetValue(2, 1, 20);
                sheet.SetValue(3, 1, 30);
            }
        );

        result.ShouldBe(2.0);
    }

    [Fact]
    public void Match_ApproximateGreaterThanOrEqual_FindsSmallest()
    {
        var result = Num(
            "=MATCH(15,A1:A3,-1)",
            static sheet =>
            {
                sheet.SetValue(1, 1, 30);
                sheet.SetValue(2, 1, 20);
                sheet.SetValue(3, 1, 10);
            }
        );

        result.ShouldBe(2.0);
    }

    [Fact]
    public void Match_NotFound_ReturnsNA()
    {
        var result = Eval(
            "=MATCH(\"Z\",A1:A2,0)",
            static sheet =>
            {
                sheet.SetValue(1, 1, "A");
                sheet.SetValue(2, 1, "B");
            }
        );

        result.ShouldBe(CellError.NotAvailable);
    }

    [Fact]
    public void Match_TooFewArgs_ReturnsVALUE()
    {
        var result = Eval("=MATCH(\"A\")");
        result.ShouldBe(CellError.Value);
    }

    [Fact]
    public void Match_DefaultMatchType_UsesApproximate()
    {
        // Default match type is 1 (approximate, less than or equal)
        var result = Num(
            "=MATCH(25,A1:A3)",
            static sheet =>
            {
                sheet.SetValue(1, 1, 10);
                sheet.SetValue(2, 1, 20);
                sheet.SetValue(3, 1, 30);
            }
        );

        result.ShouldBe(2.0);
    }

    [Fact]
    public void Match_ApproximateNoMatch_ReturnsNA()
    {
        var result = Eval(
            "=MATCH(5,A1:A3,1)",
            static sheet =>
            {
                sheet.SetValue(1, 1, 10);
                sheet.SetValue(2, 1, 20);
                sheet.SetValue(3, 1, 30);
            }
        );

        result.ShouldBe(CellError.NotAvailable);
    }

    [Fact]
    public void Match_DescendingNoMatch_ReturnsNA()
    {
        var result = Eval(
            "=MATCH(50,A1:A3,-1)",
            static sheet =>
            {
                sheet.SetValue(1, 1, 30);
                sheet.SetValue(2, 1, 20);
                sheet.SetValue(3, 1, 10);
            }
        );

        result.ShouldBe(CellError.NotAvailable);
    }

    [Fact]
    public void Index_NonArrayScalar_ReturnsScalar()
    {
        // Passing a scalar (non-array) to INDEX with row/col <= 1 returns the scalar itself
        var result = Num(
            "=INDEX(A1,1)",
            static sheet =>
            {
                sheet.SetValue(1, 1, 42);
            }
        );

        result.ShouldBe(42.0);
    }
}
