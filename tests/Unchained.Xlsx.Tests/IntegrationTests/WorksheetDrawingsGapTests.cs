using Shouldly;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Drawing;
using Unchained.Xlsx.Drawings;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Tests for <see cref="Unchained.Xlsx.Worksheets.Worksheet" /> drawing APIs not covered by the
///     baseline suite: <c>AddChart</c> with a title and <c>RebindChart</c> (fill-preserving and
///     series-count-changed paths).
/// </summary>
public sealed class WorksheetDrawingsGapTests
{
    private static DrawingAnchor Anchor() => DrawingAnchor.OneCell(CellReference.FromA1("H1"), 300, 200);

    private static SpreadsheetProcessor _processor = null!;

    private static Worksheets.Worksheet SheetWithGrid()
    {
        _processor = new SpreadsheetProcessor();
        var document = _processor.CreateBlank("Sheet1");
        var sheet = document.Sheets[0];

        sheet.SetValue(1, 1, "Cat");
        sheet.SetValue(1, 2, "S1");
        sheet.SetValue(1, 3, "S2");
        sheet.SetValue(2, 1, "Jan");
        sheet.SetValue(2, 2, 1.0); sheet.SetValue(2, 3, 4.0);
        sheet.SetValue(3, 1, "Feb");
        sheet.SetValue(3, 2, 2.0);
        sheet.SetValue(3, 3, 5.0);
        sheet.SetValue(4, 1, "Mar");
        sheet.SetValue(4, 2, 3.0);
        sheet.SetValue(4, 3, 6.0);

        return sheet;
    }

    private static FillFormat RedFill() =>
        new() { Type = FillType.Solid, Solid = new SolidFill { Color = ColorSpec.FromRgb(0xFF, 0x00, 0x00) } };

    [Fact]
    public void AddChart_WithTitle_SetsHasTitleAndText()
    {
        var sheet = SheetWithGrid();

        var chart = sheet.AddChart(ChartType.ColumnClustered, CellRange.FromA1("A1:B4"), Anchor(), "Quarterly Sales");

        chart.Chart.HasTitle.ShouldBeTrue();
        chart.Chart.Title.ShouldBe("Quarterly Sales");
    }

    [Fact]
    public void RebindChart_SameSeriesCount_PreservesFills()
    {
        var sheet = SheetWithGrid();
        var chart = sheet.AddChart(ChartType.ColumnClustered, CellRange.FromA1("A1:B4"), Anchor());
        chart.Chart.Data.Series.Count.ShouldBe(1);
        chart.Chart.Data.Series[0].Fill = RedFill();

        sheet.RebindChart(chart, CellRange.FromA1("A1:B4"));

        chart.Chart.Data.Series.Count.ShouldBe(1);
        chart.Chart.Data.Series[0].Fill.ShouldNotBeNull();
        chart.Chart.Data.Series[0].Fill!.Solid!.Color.Resolve(null!).ShouldBe(RedFill().Solid!.Color.Resolve(null!));
    }

    [Fact]
    public void RebindChart_DifferentSeriesCount_DropsFills()
    {
        var sheet = SheetWithGrid();
        var chart = sheet.AddChart(ChartType.ColumnClustered, CellRange.FromA1("A1:B4"), Anchor());
        chart.Chart.Data.Series[0].Fill = RedFill();

        // Rebinding to a two-series range makes the counts differ, so old fills are not carried.
        sheet.RebindChart(chart, CellRange.FromA1("A1:C4"));

        chart.Chart.Data.Series.Count.ShouldBe(2);
        chart.Chart.Data.Series[0].Fill.ShouldBeNull();
    }
}
