using Shouldly;
using Unchained.Xlsx.Formulas;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests;

/// <summary>Additional branch coverage for <c>FormulaShifter</c>, especially <c>ShiftRelative</c>.</summary>
public class FormulaShifterCoverageTests
{
    // ── ShiftRelative ────────────────────────────────────────────────────────

    [
        Theory,
        InlineData("A1", 1, 0, "A2"),
        InlineData("A1", 0, 1, "B1"),
        InlineData("A1", 2, 3, "D3"),
        InlineData("$A$1", 5, 5, "$A$1"),       // both absolute → unchanged
        InlineData("$A1", 0, 4, "$A1"),         // column absolute, no row delta
        InlineData("A$1", 4, 0, "A$1")          // row absolute, no column delta
    ]
    public void ShiftRelative_AppliesDeltas(string formula, int rowDelta, int columnDelta, string expected) =>
        FormulaShifter.ShiftRelative(formula, rowDelta, columnDelta).ShouldBe(expected);

    [Fact]
    public void ShiftRelative_ZeroDeltas_ReturnsOriginal() =>
        FormulaShifter.ShiftRelative("A1+B2", 0, 0).ShouldBe("A1+B2");

    [Fact]
    public void ShiftRelative_Empty_ReturnsEmpty() =>
        FormulaShifter.ShiftRelative("", 1, 1).ShouldBe("");

    [Fact]
    public void ShiftRelative_OutOfRange_BecomesRefError() =>
        FormulaShifter.ShiftRelative("A1", -5, 0).ShouldBe("#REF!");

    [Fact]
    public void ShiftRelative_LeavesQuotedText() =>
        FormulaShifter.ShiftRelative("\"A1\"&B2", 1, 0).ShouldBe("\"A1\"&B3");

    // ── Shift column delete & #REF! ──────────────────────────────────────────

    [Fact]
    public void Shift_Columns_Delete() =>
        FormulaShifter.Shift("D5", FormulaShifter.Axis.Column, 2, -1).ShouldBe("C5");

    [Fact]
    public void Shift_Columns_DeletedReference_BecomesRefError() =>
        FormulaShifter.Shift("C5", FormulaShifter.Axis.Column, 3, -1).ShouldBe("#REF!");

    [Fact]
    public void Shift_MultiCharColumn() =>
        FormulaShifter.Shift("AA1", FormulaShifter.Axis.Column, 2, 1).ShouldBe("AB1");

    [Fact]
    public void Shift_ColumnBeforeInsertionPoint_Unchanged() =>
        FormulaShifter.Shift("A5", FormulaShifter.Axis.Column, 5, 1).ShouldBe("A5");

    [Fact]
    public void Shift_RowInsert_PushesRefIntoRefErrorAtMax()
    {
        // A reference at the maximum row pushed further down overflows → #REF!.
        FormulaShifter.Shift("A1048576", FormulaShifter.Axis.Row, 1, 1).ShouldBe("#REF!");
    }
}
