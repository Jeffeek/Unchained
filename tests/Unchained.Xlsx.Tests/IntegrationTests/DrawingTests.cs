using Shouldly;
using Unchained.Ooxml.Charts;
using Unchained.Xlsx.Drawings;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Drawings;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class DrawingTests
{
    // A 1×1 transparent PNG.
    private static readonly byte[] TinyPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M8AAAMBAQDJ/pLvAAAAAElFTkSuQmCC");

    [Fact]
    public async Task AddImage_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].AddImage(TinyPng, "image/png", CellReference.FromA1("B2"));

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var pictures = reloaded.Sheets[0].Drawings.Pictures.ToList();
        pictures.Count.ShouldBe(1);
        pictures[0].Image.ContentType.ShouldBe("image/png");
        pictures[0].Image.Data.Length.ShouldBe(TinyPng.Length);
        pictures[0].Anchor.From.ShouldBe(CellReference.FromA1("B2"));
    }

    [Fact]
    public async Task AddImage_TwoCellAnchor_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var anchor = DrawingAnchor.TwoCell(CellReference.FromA1("A1"), CellReference.FromA1("D10"));
        document.Sheets[0].AddImage(TinyPng, "image/png", anchor);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var pic = reloaded.Sheets[0].Drawings.Pictures.Single();
        pic.Anchor.AnchorType.ShouldBe(DrawingAnchorType.TwoCell);
        pic.Anchor.From.ShouldBe(CellReference.FromA1("A1"));
        pic.Anchor.To.ShouldBe(CellReference.FromA1("D10"));
    }

    [Fact]
    public async Task AddChart_ColumnChart_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Month");
        sheet.SetValue(1, 2, "Sales");
        sheet.SetValue(2, 1, "Jan");
        sheet.SetValue(2, 2, 100.0);
        sheet.SetValue(3, 1, "Feb");
        sheet.SetValue(3, 2, 150.0);

        sheet.AddChart(ChartType.ColumnClustered, CellRange.FromA1("A1:B3"),
            DrawingAnchor.TwoCell(CellReference.FromA1("D1"), CellReference.FromA1("J15")), "Sales by Month");

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var charts = reloaded.Sheets[0].Drawings.OfType<ChartDrawing>().ToList();
        charts.Count.ShouldBe(1);
        var chart = charts[0].Chart;
        chart.Type.ShouldBe(ChartType.ColumnClustered);
        chart.Title.ShouldBe("Sales by Month");
        chart.Data.Categories.ShouldBe(["Jan", "Feb"]);
        chart.Data.Series.Count.ShouldBe(1);
        chart.Data.Series[0].Name.ShouldBe("Sales");
        chart.Data.Series[0].Values.ShouldBe([100.0, 150.0]);
    }

    [Fact]
    public async Task AddChart_PieChart_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Slice");
        sheet.SetValue(1, 2, "Value");
        sheet.SetValue(2, 1, "A");
        sheet.SetValue(2, 2, 30.0);
        sheet.SetValue(3, 1, "B");
        sheet.SetValue(3, 2, 70.0);

        sheet.AddChart(ChartType.Pie, CellRange.FromA1("A1:B3"),
            DrawingAnchor.OneCell(CellReference.FromA1("D1")));

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var chart = reloaded.Sheets[0].Drawings.OfType<ChartDrawing>().Single().Chart;
        chart.Type.ShouldBe(ChartType.Pie);
        chart.Data.Series[0].Values.ShouldBe([30.0, 70.0]);
    }

    [Fact]
    public async Task ImageAndChart_CoexistOnSameSheet()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "X");
        sheet.SetValue(2, 1, 5.0);
        sheet.AddImage(TinyPng, "image/png", CellReference.FromA1("C1"));
        sheet.AddChart(ChartType.Line, CellRange.FromA1("A1:A2"),
            DrawingAnchor.OneCell(CellReference.FromA1("E1")));

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].Drawings.Count.ShouldBe(2);
        reloaded.Sheets[0].Drawings.Pictures.Count().ShouldBe(1);
        reloaded.Sheets[0].Drawings.OfType<ChartDrawing>().Count().ShouldBe(1);
    }
}
