using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Tests for logical formula functions: IF, IFS, SWITCH, IFERROR, IFNA, AND, OR, XOR.
/// </summary>
public sealed class FormulaLogicalTests
{
    private static object? Eval(string formula)
    {
        using var document = XlsxFixtures.WithSheets("Sheet1");
        return SpreadsheetDocument.EvaluateFormula(document.Sheets[0], formula);
    }

    [
        Theory,
        InlineData("=IF(TRUE,\"yes\",\"no\")", "yes"),
        InlineData("=IF(FALSE,\"yes\",\"no\")", "no"),
        InlineData("=IF(1>0,10,20)", 10.0),
        InlineData("=IF(1<0,10,20)", 20.0)
    ]
    public void If_BasicConditions_ReturnsCorrectBranch(string formula, object expected) =>
        Eval(formula).ShouldBe(expected);

    [Fact]
    public void If_TwoArgs_NoElse_ReturnsFalse() =>
        Eval("=IF(FALSE,\"yes\")").ShouldBe(false);

    [Fact]
    public void If_InsufficientArgs_ReturnsValueError() =>
        Eval("=IF(TRUE)").ShouldBe(CellError.Value);

    [Fact]
    public void If_ErrorCondition_PropagatesError() =>
        Eval("=IF(1/0,\"yes\",\"no\")").ShouldBe(CellError.DivisionByZero);

    [
        Theory,
        InlineData("=IFS(TRUE,\"a\",FALSE,\"b\")", "a"),
        InlineData("=IFS(FALSE,\"a\",TRUE,\"b\")", "b"),
        InlineData("=IFS(1>2,\"a\",2>1,\"b\")", "b")
    ]
    public void Ifs_MultipleConditions_ReturnsFirstMatch(string formula, string expected) =>
        Eval(formula).ShouldBe(expected);

    [Fact]
    public void Ifs_NoMatch_ReturnsNaError() =>
        Eval("=IFS(FALSE,\"a\",FALSE,\"b\")").ShouldBe(CellError.NotAvailable);

    [Fact]
    public void Ifs_ErrorInCondition_PropagatesError() =>
        Eval("=IFS(1/0,\"a\")").ShouldBe(CellError.DivisionByZero);

    [
        Theory,
        InlineData("=SWITCH(1,1,\"a\",2,\"b\")", "a"),
        InlineData("=SWITCH(2,1,\"a\",2,\"b\")", "b"),
        InlineData("=SWITCH(\"x\",\"x\",\"found\",\"other\")", "found")
    ]
    public void Switch_MatchesValue_ReturnsResult(string formula, string expected) =>
        Eval(formula).ShouldBe(expected);

    [Fact]
    public void Switch_NoMatch_ReturnsNaError() =>
        Eval("=SWITCH(3,1,\"a\",2,\"b\")").ShouldBe(CellError.NotAvailable);

    [Fact]
    public void Switch_NoMatch_WithDefault_ReturnsDefault() =>
        Eval("=SWITCH(3,1,\"a\",2,\"b\",\"default\")").ShouldBe("default");

    [Fact]
    public void Switch_InsufficientArgs_ReturnsValueError() =>
        Eval("=SWITCH(1)").ShouldBe(CellError.Value);

    [
        Theory,
        InlineData("=IFERROR(1/0,\"caught\")", "caught"),
        InlineData("=IFERROR(42,\"caught\")", 42.0),
        InlineData("=IFERROR(\"text\",\"caught\")", "text")
    ]
    public void IfError_CatchesErrors(string formula, object expected) =>
        Eval(formula).ShouldBe(expected);

    [Fact]
    public void IfError_InsufficientArgs_ReturnsValueError() =>
        Eval("=IFERROR(1)").ShouldBe(CellError.Value);

    [
        Theory,
        InlineData("=IFNA(NA(),\"caught\")", "caught"),
        InlineData("=IFNA(42,\"caught\")", 42.0)
    ]
    public void IfNa_CatchesNaErrorOnly(string formula, object expected) =>
        Eval(formula).ShouldBe(expected);

    [Fact]
    public void IfNa_NonNaError_NotCaught() =>
        Eval("=IFNA(1/0,\"not caught\")").ShouldBe(CellError.DivisionByZero);

    [Fact]
    public void IfNa_InsufficientArgs_ReturnsValueError() =>
        Eval("=IFNA(1)").ShouldBe(CellError.Value);

    [
        Theory,
        InlineData("=AND(TRUE,TRUE)", true),
        InlineData("=AND(TRUE,FALSE)", false),
        InlineData("=AND(FALSE,FALSE)", false),
        InlineData("=AND(1>0,2>1)", true)
    ]
    public void And_LogicalConditions_ReturnsCorrectResult(string formula, bool expected) =>
        Eval(formula).ShouldBe(expected);

    [Fact]
    public void And_EmptyArgs_ReturnsFalse() =>
        Eval("=AND()").ShouldBe(false);

    [Fact]
    public void And_ErrorInArg_PropagatesError() =>
        Eval("=AND(TRUE,1/0)").ShouldBe(CellError.DivisionByZero);

    [
        Theory,
        InlineData("=OR(TRUE,FALSE)", true),
        InlineData("=OR(FALSE,FALSE)", false),
        InlineData("=OR(FALSE,TRUE)", true),
        InlineData("=OR(1>0,2<1)", true)
    ]
    public void Or_LogicalConditions_ReturnsCorrectResult(string formula, bool expected) =>
        Eval(formula).ShouldBe(expected);

    [Fact]
    public void Or_EmptyArgs_ReturnsFalse() =>
        Eval("=OR()").ShouldBe(false);

    [Fact]
    public void Or_ErrorInArg_PropagatesError() =>
        Eval("=OR(FALSE,1/0)").ShouldBe(CellError.DivisionByZero);

    [
        Theory,
        InlineData("=XOR(TRUE,FALSE)", true),
        InlineData("=XOR(TRUE,TRUE)", false),
        InlineData("=XOR(FALSE,FALSE)", false),
        InlineData("=XOR(TRUE,TRUE,TRUE)", true)
    ]
    // Odd number of TRUE
    public void Xor_LogicalConditions_ReturnsCorrectResult(string formula, bool expected) =>
        Eval(formula).ShouldBe(expected);

    [Fact]
    public void Xor_EmptyArgs_ReturnsFalse() =>
        Eval("=XOR()").ShouldBe(false);

    [Fact]
    public void And_ShortCircuits_OnFirstFalse() =>
        Eval("=AND(FALSE,1/0)").ShouldBe(false); // Should not evaluate 1/0

    [Fact]
    public void Or_ShortCircuits_OnFirstTrue() =>
        Eval("=OR(TRUE,1/0)").ShouldBe(true); // Should not evaluate 1/0
}
