using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Exercises the conditional worksheet functions: IF/IFS/SWITCH/IFERROR and the range-based
///     aggregates SUMIF, COUNTIF, AVERAGEIF, SUMIFS, AVERAGEIFS, MAXIFS, MINIFS.
/// </summary>
public sealed class FormulaConditionalTests
{
    private static object? Eval(string formula, bool withData = false)
    {
        using var document = XlsxFixtures.WithSheets("Sheet1");
        if (withData)
        {
            document.Sheets[0].SetValue(1, 1, 1.0);
            document.Sheets[0].SetValue(2, 1, 2.0);
            document.Sheets[0].SetValue(3, 1, 3.0);
        }

        return SpreadsheetDocument.EvaluateFormula(document.Sheets[0], formula);
    }

    [
        Theory,
        InlineData("=IF(1>0,\"yes\",\"no\")", "yes"),
        InlineData("=IF(1<0,\"yes\",\"no\")", "no"),
        InlineData("=IFS(FALSE,\"a\",TRUE,\"b\")", "b"),
        InlineData("=SWITCH(2,1,\"a\",2,\"b\",\"def\")", "b"),
        InlineData("=SWITCH(9,1,\"a\",\"def\")", "def"),
        InlineData("=IFERROR(1/0,\"caught\")", "caught")
    ]
    public void Conditionals_ReturnExpectedString(string formula, string expected) => Eval(formula).ShouldBe(expected);

    [Fact]
    public void Ifs_NoMatch_ReturnsNaError() => Eval("=IFS(FALSE,1,FALSE,2)").ShouldBe(CellError.NotAvailable);

    [Fact]
    public void IfError_NoError_PassesValueThrough() => Eval("=IFERROR(5,\"caught\")").ShouldBe(5.0);

    [
        Theory,
        InlineData("=SUMIF(A1:A3,\">1\")", 5),
        InlineData("=COUNTIF(A1:A3,\">1\")", 2),
        InlineData("=AVERAGEIF(A1:A3,\">1\")", 2.5),
        InlineData("=SUMIFS(A1:A3,A1:A3,\">1\")", 5),
        InlineData("=AVERAGEIFS(A1:A3,A1:A3,\">1\")", 2.5),
        InlineData("=MAXIFS(A1:A3,A1:A3,\">1\")", 3),
        InlineData("=MINIFS(A1:A3,A1:A3,\">1\")", 2)
    ]
    public void RangeAggregates_WithCriteria(string formula, double expected) =>
        ((double)Eval(formula, withData: true)!).ShouldBe(expected, 1e-9);

    [Fact]
    public void SumIf_InsufficientArgs_ReturnsValueError() => Eval("=SUMIF(A1:A3)").ShouldBe(CellError.Value);

    [Fact]
    public void CountIf_InsufficientArgs_ReturnsValueError() => Eval("=COUNTIF(A1:A3)").ShouldBe(CellError.Value);

    [Fact]
    public void AverageIf_InsufficientArgs_ReturnsValueError() => Eval("=AVERAGEIF(A1:A3)").ShouldBe(CellError.Value);

    [Fact]
    public void SumIfs_InsufficientArgs_ReturnsValueError() => Eval("=SUMIFS(A1:A3,A1:A3)").ShouldBe(CellError.Value);

    [Fact]
    public void AverageIfs_InsufficientArgs_ReturnsValueError() => Eval("=AVERAGEIFS(A1:A3,A1:A3)").ShouldBe(CellError.Value);

    [Fact]
    public void MaxIfs_InsufficientArgs_ReturnsValueError() => Eval("=MAXIFS(A1:A3,A1:A3)").ShouldBe(CellError.Value);

    [Fact]
    public void MinIfs_InsufficientArgs_ReturnsValueError() => Eval("=MINIFS(A1:A3,A1:A3)").ShouldBe(CellError.Value);

    [Fact]
    public void SumIf_WithSumRange_AggregatesDifferentRange()
    {
        using var doc = XlsxFixtures.WithSheets("Sheet1");
        doc.Sheets[0].SetValue(1, 1, 10.0); // A1
        doc.Sheets[0].SetValue(2, 1, 20.0); // A2
        doc.Sheets[0].SetValue(1, 2, 1.0);  // B1
        doc.Sheets[0].SetValue(2, 2, 2.0);  // B2
        var result = SpreadsheetDocument.EvaluateFormula(doc.Sheets[0], "=SUMIF(B1:B2,\">1\",A1:A2)");
        result.ShouldBe(20.0); // Only A2 where B2>1
    }

    [Fact]
    public void AverageIf_WithRange_AggregatesDifferentRange()
    {
        using var doc = XlsxFixtures.WithSheets("Sheet1");
        doc.Sheets[0].SetValue(1, 1, 10.0); // A1
        doc.Sheets[0].SetValue(2, 1, 20.0); // A2
        doc.Sheets[0].SetValue(1, 2, 1.0);  // B1
        doc.Sheets[0].SetValue(2, 2, 2.0);  // B2
        var result = SpreadsheetDocument.EvaluateFormula(doc.Sheets[0], "=AVERAGEIF(B1:B2,\">1\",A1:A2)");
        result.ShouldBe(20.0); // Only A2 where B2>1
    }
}
