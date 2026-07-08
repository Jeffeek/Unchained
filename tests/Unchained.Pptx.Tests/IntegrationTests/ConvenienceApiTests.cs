using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     M-A convenience APIs: table cell merge, public row/column add/insert/remove, AddLine,
///     and the AutoShape.Text shortcut.
/// </summary>
public sealed class ConvenienceApiTests : PptxTestBase
{
    private static TableShape NewTable(int cols, int rows)
    {
        var doc = PptxFixtures.WithSlides(1);
        var widths = Enumerable.Repeat(Emu.FromInches(1), cols).ToArray();
        var heights = Enumerable.Repeat(Emu.FromInches(0.5), rows).ToArray();
        return doc.Slides[0].Shapes.AddTable(Emu.Zero, Emu.Zero, widths, heights);
    }

    [Fact]
    public void MergeCells_ByIndex_SetsSpanAndContinuationFlags()
    {
        var table = NewTable(3, 3);
        table.MergeCells(0, 0, 1, 1); // 2x2 block

        var anchor = table[0, 0];
        anchor.ColumnSpan.ShouldBe(2);
        anchor.RowSpan.ShouldBe(2);
        table[1, 0].IsHorizontalMergeContinuation.ShouldBeTrue();
        table[0, 1].IsVerticalMergeContinuation.ShouldBeTrue();
        table[1, 1].IsHorizontalMergeContinuation.ShouldBeTrue();
        table[1, 1].IsVerticalMergeContinuation.ShouldBeTrue();
        // Untouched cell.
        table[2, 2].ColumnSpan.ShouldBe(1);
    }

    [Fact]
    public void MergeCells_ByCellReference_Works()
    {
        var table = NewTable(2, 2);
        table.MergeCells(table[0, 0], table[1, 0]); // merge two cells in row 0
        table[0, 0].ColumnSpan.ShouldBe(2);
        table[1, 0].IsHorizontalMergeContinuation.ShouldBeTrue();
    }

    [Fact]
    public void MergeCells_OutOfRange_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(static () => NewTable(2, 2).MergeCells(0, 0, 5, 5));

    [Fact]
    public void Grid_AddInsertRemoveRowColumn_AdjustsCounts()
    {
        var table = NewTable(2, 2);
        var grid = table.Grid;

        grid.AddRow(Emu.FromInches(0.5));
        grid.RowCount.ShouldBe(3);

        grid.InsertRow(0, Emu.FromInches(0.5));
        grid.RowCount.ShouldBe(4);

        grid.AddColumn(Emu.FromInches(1));
        grid.ColumnCount.ShouldBe(3);

        grid.InsertColumn(1, Emu.FromInches(1));
        grid.ColumnCount.ShouldBe(4);

        grid.RemoveRow(0);
        grid.RowCount.ShouldBe(3);

        grid.RemoveColumn(0);
        grid.ColumnCount.ShouldBe(3);

        // Every row still has the right number of cells after column edits.
        for (var r = 0; r < grid.RowCount; r++)
            _ = grid[grid.ColumnCount - 1, r];
    }

    [Fact]
    public void AddLine_NormalizesBoxAndSetsFlips()
    {
        var doc = PptxFixtures.WithSlides(1);
        // End point is left-and-up of the start: expect flips + positive extents.
        var line = doc.Slides[0]
            .Shapes.AddLine(
                Emu.FromInches(4),
                Emu.FromInches(3),
                Emu.FromInches(1),
                Emu.FromInches(1)
            );

        line.ConnectorType.ShouldBe(ConnectorType.Straight);
        line.X.Value.ShouldBe(Emu.FromInches(1).Value);
        line.Y.Value.ShouldBe(Emu.FromInches(1).Value);
        line.Width.Value.ShouldBe(Emu.FromInches(3).Value);
        line.Height.Value.ShouldBe(Emu.FromInches(2).Value);
        line.FlipHorizontal.ShouldBeTrue();
        line.FlipVertical.ShouldBeTrue();
    }

    [Fact]
    public void AutoShape_TextShortcut_RoundTripsWithTextFrame()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(
                AutoShapeType.Rectangle,
                Emu.Zero,
                Emu.Zero,
                Emu.FromInches(2),
                Emu.FromInches(1)
            );

        shape.Text = "Hello";
        shape.Text.ShouldBe("Hello");
        shape.TextFrame.PlainText.ShouldBe("Hello");
    }

    [Fact]
    public async Task MergedTable_SurvivesRoundTrip()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0]
            .Shapes.AddTable(
                Emu.Zero,
                Emu.Zero,
                [Emu.FromInches(1), Emu.FromInches(1)],
                [Emu.FromInches(0.5), Emu.FromInches(0.5)]
            );
        table.MergeCells(0, 0, 1, 0);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var t = reloaded.Slides[0].Shapes.OfType<TableShape>().Single();
        t[0, 0].ColumnSpan.ShouldBe(2);
    }
}
