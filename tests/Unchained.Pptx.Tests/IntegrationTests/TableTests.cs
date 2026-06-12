using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class TableTests : PptxTestBase
{
    [Fact]
    public void AddTable_HasCorrectDimensions()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0].Shapes.AddTable(
            Emu.Zero,
            Emu.Zero,
            [Emu.FromInches(2), Emu.FromInches(2), Emu.FromInches(2)],
            [Emu.FromInches(0.5), Emu.FromInches(0.5)]);

        table.Grid.ColumnCount.ShouldBe(3);
        table.Grid.RowCount.ShouldBe(2);
    }

    [Fact]
    public void TableCell_TextFrame_CanSetText()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0].Shapes.AddTable(
            Emu.Zero,
            Emu.Zero,
            [Emu.FromInches(2), Emu.FromInches(2)],
            [Emu.FromInches(0.5)]);

        table.Grid[0, 0].TextFrame.PlainText = "Header 1";
        table.Grid[1, 0].TextFrame.PlainText = "Header 2";

        table.Grid[0, 0].TextFrame.PlainText.ShouldBe("Header 1");
        table.Grid[1, 0].TextFrame.PlainText.ShouldBe("Header 2");
    }

    [Fact]
    public void Table_Indexer_AccessesCorrectCell()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0].Shapes.AddTable(
            Emu.Zero,
            Emu.Zero,
            [Emu.FromInches(2), Emu.FromInches(2)],
            [Emu.FromInches(0.5), Emu.FromInches(0.5)]);

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
        var table = doc.Slides[0].Shapes.AddTable(
            Emu.Zero,
            Emu.Zero,
            [Emu.FromInches(2)],
            [Emu.FromInches(0.5)]);

        table.HasHeaderRow.ShouldBeFalse();
    }

    [Fact]
    public void Table_SetHasHeaderRow_IsPreserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        var table = doc.Slides[0].Shapes.AddTable(
            Emu.Zero,
            Emu.Zero,
            [Emu.FromInches(2)],
            [Emu.FromInches(0.5)]);

        table.HasHeaderRow = true;
        table.HasHeaderRow.ShouldBeTrue();
    }

    [Fact]
    public async Task Table_RoundTrips_PreservesDimensions()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTable(
            Emu.Zero,
            Emu.Zero,
            [Emu.FromInches(2), Emu.FromInches(2)],
            [Emu.FromInches(0.5), Emu.FromInches(0.5), Emu.FromInches(0.5)]);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var table = reloaded.Slides[0].Shapes.OfType<TableShape>().FirstOrDefault();
        table.ShouldNotBeNull();
        table.Grid.ColumnCount.ShouldBe(2);
        table.Grid.RowCount.ShouldBe(3);
    }
}
