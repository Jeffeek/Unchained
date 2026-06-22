using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Shapes;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Shapes;

/// <summary>
///     Unit tests for <see cref="TableGrid" /> — construction, indexed cell access, and the
///     add/insert/remove row/column operations including their out-of-range boundary guards.
/// </summary>
public sealed class TableGridTests
{
    private static TableGrid Make(int columns, int rows) =>
        TableGrid.Create(
            Enumerable.Range(0, columns).Select(static _ => Emu.FromInches(1)).ToArray(),
            Enumerable.Range(0, rows).Select(static _ => Emu.FromInches(0.5)).ToArray()
        );

    [Fact]
    public void Create_SetsDimensionsAndCells()
    {
        var grid = Make(3, 2);
        grid.ColumnCount.ShouldBe(3);
        grid.RowCount.ShouldBe(2);
        grid.ColumnWidths.Count.ShouldBe(3);
        grid.RowHeights.Count.ShouldBe(2);
        grid[2, 1].ShouldNotBeNull();
    }

    [Fact]
    public void Create_NullColumnWidths_Throws() =>
        Should.Throw<ArgumentNullException>(static () => TableGrid.Create(null!, [Emu.FromInches(1)]));

    [Fact]
    public void Create_NullRowHeights_Throws() =>
        Should.Throw<ArgumentNullException>(static () => TableGrid.Create([Emu.FromInches(1)], null!));

    [Fact]
    public void AddRow_AppendsRowOfCells()
    {
        var grid = Make(2, 1);
        grid.AddRow(Emu.FromInches(0.5));
        grid.RowCount.ShouldBe(2);
        grid[1, 1].ShouldNotBeNull();
    }

    [Fact]
    public void InsertRow_AtStart_ShiftsExisting()
    {
        var grid = Make(2, 2);
        grid[0, 0].TextFrame.PlainText = "wasFirst";
        grid.InsertRow(0, Emu.FromInches(0.5));
        grid.RowCount.ShouldBe(3);
        grid[0, 1].TextFrame.PlainText.ShouldBe("wasFirst");
    }

    [
        Theory,
        InlineData(-1),
        InlineData(3)
    ]
    public void InsertRow_OutOfRange_Throws(int index)
    {
        var grid = Make(2, 2);
        Should.Throw<ArgumentOutOfRangeException>(() => grid.InsertRow(index, Emu.FromInches(0.5)));
    }

    [Fact]
    public void AddColumn_AppendsCellToEveryRow()
    {
        var grid = Make(2, 2);
        grid.AddColumn(Emu.FromInches(1));
        grid.ColumnCount.ShouldBe(3);
        grid[2, 0].ShouldNotBeNull();
        grid[2, 1].ShouldNotBeNull();
    }

    [Fact]
    public void InsertColumn_AtStart_ShiftsExisting()
    {
        var grid = Make(2, 1);
        grid[0, 0].TextFrame.PlainText = "wasCol0";
        grid.InsertColumn(0, Emu.FromInches(1));
        grid.ColumnCount.ShouldBe(3);
        grid[1, 0].TextFrame.PlainText.ShouldBe("wasCol0");
    }

    [
        Theory,
        InlineData(-1),
        InlineData(3)
    ]
    public void InsertColumn_OutOfRange_Throws(int index)
    {
        var grid = Make(2, 1);
        Should.Throw<ArgumentOutOfRangeException>(() => grid.InsertColumn(index, Emu.FromInches(1)));
    }

    [Fact]
    public void RemoveRow_DropsRow()
    {
        var grid = Make(2, 3);
        grid.RemoveRow(1);
        grid.RowCount.ShouldBe(2);
    }

    [
        Theory,
        InlineData(-1),
        InlineData(3)
    ]
    public void RemoveRow_OutOfRange_Throws(int index)
    {
        var grid = Make(2, 3);
        Should.Throw<ArgumentOutOfRangeException>(() => grid.RemoveRow(index));
    }

    [Fact]
    public void RemoveColumn_DropsColumnFromEveryRow()
    {
        var grid = Make(3, 2);
        grid.RemoveColumn(1);
        grid.ColumnCount.ShouldBe(2);
        grid[1, 0].ShouldNotBeNull();
    }

    [
        Theory,
        InlineData(-1),
        InlineData(3)
    ]
    public void RemoveColumn_OutOfRange_Throws(int index)
    {
        var grid = Make(3, 2);
        Should.Throw<ArgumentOutOfRangeException>(() => grid.RemoveColumn(index));
    }
}
