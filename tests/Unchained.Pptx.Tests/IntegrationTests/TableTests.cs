using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class TableTests : PptxTestBase
{
    [Fact]
    public void AddTable_HasCorrectDimensions()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0]
            .Shapes.AddTable(
                Emu.Zero,
                Emu.Zero,
                [Emu.FromInches(2), Emu.FromInches(2), Emu.FromInches(2)],
                [Emu.FromInches(0.5), Emu.FromInches(0.5)]
            );

        table.Grid.ColumnCount.ShouldBe(3);
        table.Grid.RowCount.ShouldBe(2);
    }

    [Fact]
    public void TableCell_TextFrame_CanSetText()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0]
            .Shapes.AddTable(
                Emu.Zero,
                Emu.Zero,
                [Emu.FromInches(2), Emu.FromInches(2)],
                [Emu.FromInches(0.5)]
            );

        table.Grid[0, 0].TextFrame.PlainText = "Header 1";
        table.Grid[1, 0].TextFrame.PlainText = "Header 2";

        table.Grid[0, 0].TextFrame.PlainText.ShouldBe("Header 1");
        table.Grid[1, 0].TextFrame.PlainText.ShouldBe("Header 2");
    }

    [Fact]
    public void Table_Indexer_AccessesCorrectCell()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0]
            .Shapes.AddTable(
                Emu.Zero,
                Emu.Zero,
                [Emu.FromInches(2), Emu.FromInches(2)],
                [Emu.FromInches(0.5), Emu.FromInches(0.5)]
            );

        // table[col, row]
        table[0, 0].TextFrame.PlainText = "R0C0";
        table[1, 1].TextFrame.PlainText = "R1C1";

        table.Grid[0, 0].TextFrame.PlainText.ShouldBe("R0C0");
        table.Grid[1, 1].TextFrame.PlainText.ShouldBe("R1C1");
    }

    [Fact]
    public void Table_HasHeaderRow_DefaultFalse()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0]
            .Shapes.AddTable(
                Emu.Zero,
                Emu.Zero,
                [Emu.FromInches(2)],
                [Emu.FromInches(0.5)]
            );

        table.HasHeaderRow.ShouldBeFalse();
    }

    [Fact]
    public void Table_SetHasHeaderRow_IsPreserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0]
            .Shapes.AddTable(
                Emu.Zero,
                Emu.Zero,
                [Emu.FromInches(2)],
                [Emu.FromInches(0.5)]
            );

        table.HasHeaderRow = true;
        table.HasHeaderRow.ShouldBeTrue();
    }

    [Fact]
    public async Task Table_RoundTrips_PreservesDimensions()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0]
            .Shapes.AddTable(
                Emu.Zero,
                Emu.Zero,
                [Emu.FromInches(2), Emu.FromInches(2)],
                [Emu.FromInches(0.5), Emu.FromInches(0.5), Emu.FromInches(0.5)]
            );

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var table = reloaded.Slides[0].Shapes.OfType<TableShape>().FirstOrDefault();
        table.ShouldNotBeNull();
        table.Grid.ColumnCount.ShouldBe(2);
        table.Grid.RowCount.ShouldBe(3);
    }

    private static TableShape MakeTable(int columns, int rows)
    {
        var doc = PptxFixtures.WithSlides(1);
        var widths = Enumerable.Range(0, columns).Select(static _ => Emu.FromInches(1)).ToArray();
        var heights = Enumerable.Range(0, rows).Select(static _ => Emu.FromInches(0.5)).ToArray();
        return doc.Slides[0].Shapes.AddTable(Emu.Zero, Emu.Zero, widths, heights);
    }

    [Fact]
    public void MergeCells_Block_SetsAnchorSpanAndContinuations()
    {
        var table = MakeTable(3, 3);
        table.MergeCells(0, 0, 1, 1);

        var anchor = table[0, 0];
        anchor.ColumnSpan.ShouldBe(2);
        anchor.RowSpan.ShouldBe(2);
        table[1, 0].IsHorizontalMergeContinuation.ShouldBeTrue();
        table[0, 1].IsVerticalMergeContinuation.ShouldBeTrue();
        table[1, 1].IsHorizontalMergeContinuation.ShouldBeTrue();
        table[1, 1].IsVerticalMergeContinuation.ShouldBeTrue();
    }

    [Fact]
    public void MergeCells_ReversedCoordinates_AreNormalised()
    {
        var table = MakeTable(3, 3);
        table.MergeCells(2, 2, 1, 1);
        table[1, 1].ColumnSpan.ShouldBe(2);
        table[1, 1].RowSpan.ShouldBe(2);
    }

    [Fact]
    public void MergeCells_SingleCell_IsNoOp()
    {
        var table = MakeTable(2, 2);
        table.MergeCells(0, 0, 0, 0);
        table[0, 0].ColumnSpan.ShouldBe(1);
        table[0, 0].RowSpan.ShouldBe(1);
    }

    [Fact]
    public void MergeCells_OutOfRange_Throws()
    {
        var table = MakeTable(2, 2);
        Should.Throw<ArgumentOutOfRangeException>(() => table.MergeCells(0, 0, 5, 5));
    }

    [Fact]
    public void MergeCells_CellOverload_MergesBoundingBlock()
    {
        var table = MakeTable(3, 3);
        table.MergeCells(table[0, 0], table[2, 1]);
        table[0, 0].ColumnSpan.ShouldBe(3);
        table[0, 0].RowSpan.ShouldBe(2);
    }

    [Fact]
    public void MergeCells_CellOverload_ForeignCell_Throws()
    {
        var table = MakeTable(2, 2);
        var other = MakeTable(2, 2);
        Should.Throw<ArgumentException>(() => table.MergeCells(table[0, 0], other[1, 1]));
    }

    [Fact]
    public void StyleFlags_AreSettable()
    {
        var table = MakeTable(2, 2);
        table.HasTotalRow = true;
        table.HasBandedRows = true;
        table.HasBandedColumns = true;
        table.HasFirstColumn = true;
        table.HasLastColumn = true;

        table.HasTotalRow.ShouldBeTrue();
        table.HasBandedRows.ShouldBeTrue();
        table.HasBandedColumns.ShouldBeTrue();
        table.HasFirstColumn.ShouldBeTrue();
        table.HasLastColumn.ShouldBeTrue();
    }
}
