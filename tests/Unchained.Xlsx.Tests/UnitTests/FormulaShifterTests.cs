using Shouldly;
using Unchained.Xlsx.Formulas;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests;

public class FormulaShifterTests
{
    [
        Theory,
        InlineData("B5", 3, 1, "B6"),
        InlineData("B5", 6, 1, "B5"),
        InlineData("B$5", 3, 1, "B$5"),
        InlineData("SUM(A1:A10)", 5, 1, "SUM(A1:A11)"),
        InlineData("A1+A2", 1, 1, "A2+A3")
    ]
    public void Shift_Rows_Insert(string formula, int at, int count, string expected) =>
        FormulaShifter.Shift(formula, FormulaShifter.Axis.Row, at, count).ShouldBe(expected);

    [
        Theory,
        InlineData("B5", 3, -1, "B4"),
        InlineData("B10", 3, -1, "B9"),
        InlineData("$B$5", 3, -1, "$B$5")
    ]
    public void Shift_Rows_Delete(string formula, int at, int count, string expected) =>
        FormulaShifter.Shift(formula, FormulaShifter.Axis.Row, at, count).ShouldBe(expected);

    [Fact]
    public void Shift_Rows_DeletedReference_BecomesRefError() =>
        FormulaShifter.Shift("B3", FormulaShifter.Axis.Row, 3, -1).ShouldBe("#REF!");

    [
        Theory,
        InlineData("C5", 2, 1, "D5"),
        InlineData("$C5", 2, 1, "$C5"),
        InlineData("A1:C1", 2, 1, "A1:D1")
    ]
    public void Shift_Columns_Insert(string formula, int at, int count, string expected) =>
        FormulaShifter.Shift(formula, FormulaShifter.Axis.Column, at, count).ShouldBe(expected);

    [Fact]
    public void Shift_DoesNotTouchQuotedText() =>
        FormulaShifter.Shift("\"A1 is here\"&B2", FormulaShifter.Axis.Row, 1, 1)
            .ShouldBe("\"A1 is here\"&B3");

    [Fact]
    public void Shift_EmptyFormula_ReturnsEmpty() =>
        FormulaShifter.Shift("", FormulaShifter.Axis.Row, 1, 1).ShouldBe("");
}
