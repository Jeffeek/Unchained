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
}
