using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class MergedCellTests
{
    [Fact]
    public async Task MergeCells_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Title");
        sheet.MergeCells(CellRange.FromA1("A1:C1"));

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].MergedCells.Count.ShouldBe(1);
        reloaded.Sheets[0].MergedCells[0].ToA1().ShouldBe("A1:C1");
    }

    [Fact]
    public async Task MultipleMerges_RoundTrip()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.MergeCells(CellRange.FromA1("A1:C1"));
        sheet.MergeCells(CellRange.FromA1("D5:E8"));

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].MergedCells.Count.ShouldBe(2);
        reloaded.Sheets[0].MergedCells[0].ToA1().ShouldBe("A1:C1");
        reloaded.Sheets[0].MergedCells[1].ToA1().ShouldBe("D5:E8");
    }

    [Fact]
    public void Unmerge_RemovesRange()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        var range = CellRange.FromA1("A1:C1");
        sheet.MergeCells(range);
        sheet.UnmergeCells(range);

        sheet.MergedCells.Count.ShouldBe(0);
    }

    [Fact]
    public async Task IsMerged_DetectsMembership()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Title");
        sheet.MergeCells(CellRange.FromA1("A1:C1"));

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0];
        result[1, 2].IsMerged.ShouldBeTrue();
        result[1, 1].MergeRange!.Value.ToA1().ShouldBe("A1:C1");
    }
}
