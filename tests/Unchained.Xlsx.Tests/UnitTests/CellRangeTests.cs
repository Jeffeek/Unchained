using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests;

public class CellRangeTests
{
    [Fact]
    public void FromA1_ParsesCorners()
    {
        var range = CellRange.FromA1("A1:C3");
        range.TopLeft.ShouldBe(new CellReference(1, 1));
        range.BottomRight.ShouldBe(new CellReference(3, 3));
        range.RowCount.ShouldBe(3);
        range.ColumnCount.ShouldBe(3);
        range.CellCount.ShouldBe(9);
    }

    [Fact]
    public void FromA1_SingleCell_IsOneByOne()
    {
        var range = CellRange.FromA1("B2");
        range.RowCount.ShouldBe(1);
        range.ColumnCount.ShouldBe(1);
    }

    [Fact]
    public void Constructor_NormalisesCorners()
    {
        var range = new CellRange(new CellReference(3, 3), new CellReference(1, 1));
        range.TopLeft.ShouldBe(new CellReference(1, 1));
        range.BottomRight.ShouldBe(new CellReference(3, 3));
    }

    [Fact]
    public void Contains_Cell()
    {
        var range = CellRange.FromA1("B2:D4");
        range.Contains(CellReference.FromA1("C3")).ShouldBeTrue();
        range.Contains(CellReference.FromA1("A1")).ShouldBeFalse();
        range.Contains(CellReference.FromA1("E5")).ShouldBeFalse();
    }

    [Fact]
    public void Overlaps_DetectsIntersection()
    {
        CellRange.FromA1("A1:C3").Overlaps(CellRange.FromA1("B2:D4")).ShouldBeTrue();
        CellRange.FromA1("A1:B2").Overlaps(CellRange.FromA1("C3:D4")).ShouldBeFalse();
    }

    [Fact]
    public void Union_ExpandsToCover()
    {
        var union = CellRange.FromA1("A1:B2").Union(CellRange.FromA1("D4:E5"));
        union.ToA1().ShouldBe("A1:E5");
    }

    [Fact]
    public void Cells_EnumeratesRowMajor()
    {
        var cells = CellRange.FromA1("A1:B2").Cells().Select(static c => c.ToA1()).ToList();
        cells.ShouldBe(["A1", "B1", "A2", "B2"]);
    }

    [Fact]
    public void ToSheetQualifiedA1_QuotesNamesWithSpaces()
    {
        CellRange.FromA1("A1:C3").ToSheetQualifiedA1("Sheet1").ShouldBe("Sheet1!$A$1:$C$3");
        CellRange.FromA1("A1:C3").ToSheetQualifiedA1("My Sheet").ShouldBe("'My Sheet'!$A$1:$C$3");
    }
}
