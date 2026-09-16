using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests.Models;

/// <summary>Tests for <see cref="CellError" /> enum and conversion extensions.</summary>
public sealed class CellErrorTests
{
    [
        Theory,
        InlineData(CellError.Null, "#NULL!"),
        InlineData(CellError.DivisionByZero, "#DIV/0!"),
        InlineData(CellError.Value, "#VALUE!"),
        InlineData(CellError.Reference, "#REF!"),
        InlineData(CellError.Name, "#NAME?"),
        InlineData(CellError.Number, "#NUM!"),
        InlineData(CellError.NotAvailable, "#N/A")
    ]
    public void ToLiteral_ReturnsCorrectString(CellError error, string expected) => error.ToLiteral().ShouldBe(expected);

    [
        Theory,
        InlineData("#NULL!", CellError.Null), InlineData("#DIV/0!", CellError.DivisionByZero), InlineData("#VALUE!", CellError.Value),
        InlineData("#REF!", CellError.Reference), InlineData("#NAME?", CellError.Name), InlineData("#NUM!", CellError.Number), InlineData("#N/A", CellError.NotAvailable)
    ]
    public void FromLiteral_ReturnsCorrectError(string literal, CellError expected) => CellErrorExtensions.FromLiteral(literal).ShouldBe(expected);

    [Fact]
    public void FromLiteral_UnknownLiteral_ReturnsNull() => CellErrorExtensions.FromLiteral("#UNKNOWN!").ShouldBeNull();

    [Fact]
    public void FromLiteral_NullLiteral_ReturnsNull() => CellErrorExtensions.FromLiteral(null).ShouldBeNull();

    [Fact]
    public void ToLiteral_InvalidEnumValue_ReturnsValueError() => ((CellError)999).ToLiteral().ShouldBe("#VALUE!");
}
