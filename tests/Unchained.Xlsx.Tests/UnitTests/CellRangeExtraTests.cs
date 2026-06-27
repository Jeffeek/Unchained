using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests;

public class CellRangeExtraTests
{
    [Fact]
    public void Dimensions_AreComputed()
    {
        var range = CellRange.FromBounds(2, 2, 5, 4);
        range.RowCount.ShouldBe(4);
        range.ColumnCount.ShouldBe(3);
        range.CellCount.ShouldBe(12);
    }

    [Fact]
    public void FromA1_TwoArguments_BuildsRange() =>
        CellRange.FromA1("A1", "C3").ToA1().ShouldBe("A1:C3");

    [Fact]
    public void FromA1_AbsoluteMarkers_AreParsed() =>
        CellRange.FromA1("$A$1:$B$2").ToA1().ShouldBe("A1:B2");

    [Fact]
    public void FromA1_NullOrEmpty_Throws() =>
        Should.Throw<ArgumentException>(static () => CellRange.FromA1(""));

    [
        Theory,
        InlineData("A1:C3", true),
        InlineData("B2", true),
        InlineData(null, false),
        InlineData("", false),
        InlineData("ZZZZ1:A1", false),
        InlineData("A1:ZZZZ9", false)
    ]
    public void TryFromA1_HandlesValidAndInvalid(string? a1, bool expected) =>
        CellRange.TryFromA1(a1, out _).ShouldBe(expected);

    [Fact]
    public void TryFromA1_SingleCell_Succeeds()
    {
        CellRange.TryFromA1("B2", out var range).ShouldBeTrue();
        range.CellCount.ShouldBe(1);
    }

    [Fact]
    public void FromCorners_BuildsRange() =>
        CellRange.FromCorners(new CellReference(1, 1), new CellReference(3, 3)).CellCount.ShouldBe(9);

    [Fact]
    public void EntireSheet_SpansWholeGrid()
    {
        CellRange.EntireSheet.TopLeft.ShouldBe(new CellReference(1, 1));
        CellRange.EntireSheet.BottomRight.ShouldBe(
            new CellReference(CellReference.MaxRow, CellReference.MaxColumn)
        );
    }

    [Fact]
    public void ToAbsoluteA1_AddsDollars() =>
        CellRange.FromBounds(1, 1, 3, 3).ToAbsoluteA1().ShouldBe("$A$1:$C$3");

    [
        Theory,
        InlineData("Bob's", "'Bob''s'!$A$1:$B$2"),
        InlineData("a!b", "'a!b'!$A$1:$B$2")
    ]
    public void ToSheetQualifiedA1_QuotesSpecialNames(string sheetName, string expected) =>
        CellRange.FromBounds(1, 1, 2, 2).ToSheetQualifiedA1(sheetName).ShouldBe(expected);

    [Fact]
    public void ToString_RendersA1() =>
        CellRange.FromBounds(1, 1, 3, 3).ToString().ShouldBe("A1:C3");

    [Fact]
    public void Contains_Range()
    {
        var outer = CellRange.FromBounds(1, 1, 10, 10);
        outer.Contains(CellRange.FromBounds(2, 2, 4, 4)).ShouldBeTrue();
        outer.Contains(CellRange.FromBounds(5, 5, 11, 11)).ShouldBeFalse();
    }

    [Fact]
    public void Equality_Operators_AndHashCode()
    {
        var a = CellRange.FromBounds(1, 1, 3, 3);
        var b = CellRange.FromBounds(1, 1, 3, 3);
        var c = CellRange.FromBounds(1, 1, 4, 4);

        (a == b).ShouldBeTrue();
        (a != c).ShouldBeTrue();
        a.Equals(b).ShouldBeTrue();
        a.Equals((object)b).ShouldBeTrue();
        // ReSharper disable once SuspiciousTypeConversion.Global
        a.Equals("not a range").ShouldBeFalse();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }
}
