using Shouldly;
using Unchained.Xlsx.Formulas;
using Unchained.Xlsx.Models.Cell;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests.Formulas;

/// <summary>Tests for <see cref="FormulaValue" /> struct and factory methods.</summary>
public sealed class FormulaValueTests
{
    [Fact]
    public void Blank_IsBlankKind()
    {
        FormulaValue.Blank.Kind.ShouldBe(FormulaValueKind.Blank);
        FormulaValue.Blank.Rows.ShouldBe(0);
        FormulaValue.Blank.Columns.ShouldBe(0);
    }

    [Fact]
    public void FromNumber_CreatesNumberValue()
    {
        var value = FormulaValue.FromNumber(42.5);
        value.Kind.ShouldBe(FormulaValueKind.Number);
        value.Number.ShouldBe(42.5);
        value.Rows.ShouldBe(0);
        value.Columns.ShouldBe(0);
    }

    [Fact]
    public void FromText_CreatesTextValue()
    {
        var value = FormulaValue.FromText("hello");
        value.Kind.ShouldBe(FormulaValueKind.Text);
        value.Text.ShouldBe("hello");
        value.Rows.ShouldBe(0);
        value.Columns.ShouldBe(0);
    }

    [Fact]
    public void FromBoolean_CreatesBooleanValue()
    {
        var valueTrue = FormulaValue.FromBoolean(true);
        valueTrue.Kind.ShouldBe(FormulaValueKind.Boolean);
        valueTrue.Boolean.ShouldBeTrue();
        valueTrue.Number.ShouldBe(1);

        var valueFalse = FormulaValue.FromBoolean(false);
        valueFalse.Boolean.ShouldBeFalse();
        valueFalse.Number.ShouldBe(0);
    }

    [Fact]
    public void FromError_CreatesErrorValue()
    {
        var value = FormulaValue.FromError(CellError.DivisionByZero);
        value.Kind.ShouldBe(FormulaValueKind.Error);
        value.Error.ShouldBe(CellError.DivisionByZero);
        value.IsError.ShouldBeTrue();
    }

    [Fact]
    public void FromArray_Creates1DArray()
    {
        var values = new[] { FormulaValue.FromNumber(1), FormulaValue.FromNumber(2), FormulaValue.FromNumber(3) };
        var array = FormulaValue.FromArray(values);

        array.Kind.ShouldBe(FormulaValueKind.Array);
        array.Rows.ShouldBe(3);
        array.Columns.ShouldBe(1);
        array.Array.ShouldNotBeNull();
        array.Array!.Count.ShouldBe(3);
    }

    [Fact]
    public void FromGrid_Creates2DArray()
    {
        var values = new[]
        {
            FormulaValue.FromNumber(1), FormulaValue.FromNumber(2),
            FormulaValue.FromNumber(3), FormulaValue.FromNumber(4)
        };
        var array = FormulaValue.FromGrid(values, 2, 2);

        array.Kind.ShouldBe(FormulaValueKind.Array);
        array.Rows.ShouldBe(2);
        array.Columns.ShouldBe(2);
        array.Array!.Count.ShouldBe(4);
    }

    [Fact]
    public void At_ReturnsElementAtIndex()
    {
        var values = new[]
        {
            FormulaValue.FromNumber(1), FormulaValue.FromNumber(2),
            FormulaValue.FromNumber(3), FormulaValue.FromNumber(4)
        };
        var array = FormulaValue.FromGrid(values, 2, 2);

        array.At(0, 0).Number.ShouldBe(1);
        array.At(0, 1).Number.ShouldBe(2);
        array.At(1, 0).Number.ShouldBe(3);
        array.At(1, 1).Number.ShouldBe(4);
    }

    [Fact]
    public void At_OutOfRange_ReturnsBlank()
    {
        var values = new[] { FormulaValue.FromNumber(1) };
        var array = FormulaValue.FromArray(values);

        array.At(10, 10).Kind.ShouldBe(FormulaValueKind.Blank);
    }

    [Fact]
    public void At_NonArray_ReturnsBlank()
    {
        var value = FormulaValue.FromNumber(42);
        value.At(0, 0).Kind.ShouldBe(FormulaValueKind.Blank);
    }

    [Fact]
    public void Flatten_ScalarValue_YieldsSelf()
    {
        var value = FormulaValue.FromNumber(42);
        var flattened = value.Flatten().ToList();

        flattened.Count.ShouldBe(1);
        flattened[0].Number.ShouldBe(42);
    }

    [Fact]
    public void Flatten_ArrayValue_YieldsAllElements()
    {
        var values = new[]
        {
            FormulaValue.FromNumber(1),
            FormulaValue.FromNumber(2),
            FormulaValue.FromNumber(3)
        };
        var array = FormulaValue.FromArray(values);
        var flattened = array.Flatten().ToList();

        flattened.Count.ShouldBe(3);
        flattened[0].Number.ShouldBe(1);
        flattened[1].Number.ShouldBe(2);
        flattened[2].Number.ShouldBe(3);
    }

    [Fact]
    public void Flatten_NestedArray_FlattensRecursively()
    {
        var inner = FormulaValue.FromArray(new[] { FormulaValue.FromNumber(1), FormulaValue.FromNumber(2) });
        var outer = FormulaValue.FromArray(new[] { inner, FormulaValue.FromNumber(3) });

        var flattened = outer.Flatten().ToList();

        flattened.Count.ShouldBe(3);
        flattened[0].Number.ShouldBe(1);
        flattened[1].Number.ShouldBe(2);
        flattened[2].Number.ShouldBe(3);
    }

    [Fact]
    public void IsError_ErrorValue_ReturnsTrue()
    {
        var value = FormulaValue.FromError(CellError.Value);
        value.IsError.ShouldBeTrue();
    }

    [Fact]
    public void IsError_NonErrorValue_ReturnsFalse()
    {
        FormulaValue.FromNumber(42).IsError.ShouldBeFalse();
        FormulaValue.FromText("test").IsError.ShouldBeFalse();
        FormulaValue.Blank.IsError.ShouldBeFalse();
    }
}
