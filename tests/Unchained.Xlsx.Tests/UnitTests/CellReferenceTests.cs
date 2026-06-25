using Shouldly;
using Unchained.Xlsx;
using Unchained.Xlsx.Models.Cell;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests;

public class CellReferenceTests
{
    [
        Theory,
        InlineData("A1", 1, 1),
        InlineData("B3", 3, 2),
        InlineData("Z1", 1, 26),
        InlineData("AA1", 1, 27),
        InlineData("AB10", 10, 28),
        InlineData("XFD1048576", 1048576, 16384),
        InlineData("$C$5", 5, 3),
        InlineData("c5", 5, 3)
    ]
    public void FromA1_ParsesRowAndColumn(string a1, int row, int column)
    {
        var reference = CellReference.FromA1(a1);
        reference.Row.ShouldBe(row);
        reference.Column.ShouldBe(column);
    }

    [
        Theory,
        InlineData(1, 1, "A1"),
        InlineData(3, 2, "B3"),
        InlineData(1, 27, "AA1"),
        InlineData(100, 28, "AB100")
    ]
    public void ToA1_RendersNotation(int row, int column, string expected) =>
        new CellReference(row, column).ToA1().ShouldBe(expected);

    [
        Theory,
        InlineData(1, "A"),
        InlineData(26, "Z"),
        InlineData(27, "AA"),
        InlineData(702, "ZZ"),
        InlineData(703, "AAA"),
        InlineData(16384, "XFD")
    ]
    public void ColumnNumberToLetters_RoundTrips(int column, string letters)
    {
        CellReference.ColumnNumberToLetters(column).ShouldBe(letters);
        CellReference.ColumnLettersToNumber(letters).ShouldBe(column);
    }

    [
        Theory,
        InlineData(""),
        InlineData("1"),
        InlineData("A"),
        InlineData("A0"),
        InlineData("1A"),
        InlineData("A1B"),
        InlineData("ZZZZ1")
    ]
    public void TryFromA1_RejectsInvalid(string a1) =>
        CellReference.TryFromA1(a1, out _).ShouldBeFalse();

    [Fact]
    public void Offset_AddsDeltas() =>
        new CellReference(3, 2).Offset(2, 1).ShouldBe(new CellReference(5, 3));

    [Fact]
    public void ToAbsoluteA1_AddsDollarSigns() =>
        new CellReference(5, 3).ToAbsoluteA1().ShouldBe("$C$5");

    [Fact]
    public void Comparison_IsRowMajor()
    {
        (new CellReference(1, 5) < new CellReference(2, 1)).ShouldBeTrue();
        (new CellReference(2, 1) < new CellReference(2, 2)).ShouldBeTrue();
    }

    [
        Theory,
        InlineData(0, 1),
        InlineData(1, 0),
        InlineData(1048577, 1),
        InlineData(1, 16385)
    ]
    public void Constructor_OutOfRange_Throws(int row, int column) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new CellReference(row, column));
}
