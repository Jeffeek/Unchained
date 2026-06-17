using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Charts;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Drives <c>ChartParser</c> across the full chart-type vocabulary by building each chart
///     through the public API, saving (ChartWriter), and reloading (ChartParser), asserting the
///     model survives. Covers the plot-area type dispatch and series/category readers.
/// </summary>
public sealed class ChartParserRoundTripTests : PptxTestBase
{
    private static ChartShape AddChartWithData(
        PresentationDocument doc,
        ChartType type
    )
    {
        var chart = doc.Slides[0]
            .Shapes.AddChart(type, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(6), Emu.FromInches(4));
        chart.Chart.Data.Categories.AddRange(["A", "B", "C"]);
        var series = new ChartSeries { Name = "S1" };
        series.Values.AddRange([1.0, 2.0, 3.0]);
        chart.Chart.Data.Series.Add(series);
        return chart;
    }

    [
        Theory,
        InlineData(ChartType.ColumnClustered),
        InlineData(ChartType.ColumnStacked),
        InlineData(ChartType.BarClustered),
        InlineData(ChartType.BarStacked),
        InlineData(ChartType.Line),
        InlineData(ChartType.LineStacked),
        InlineData(ChartType.Pie),
        InlineData(ChartType.Doughnut),
        InlineData(ChartType.Area),
        InlineData(ChartType.AreaStacked),
        InlineData(ChartType.Radar)
    ]
    public async Task ChartType_RoundTrips(ChartType type)
    {
        var doc = PptxFixtures.WithSlides(1);
        AddChartWithData(doc, type);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var chart = reloaded.Slides[0].Shapes.OfType<ChartShape>().Single();
        chart.Chart.Type.ShouldBe(type);
        chart.Chart.Data.Series.Count.ShouldBe(1);
        chart.Chart.Data.Series[0].Values.Count.ShouldBe(3);
    }

    [
        Theory,
        InlineData(ChartType.ScatterWithMarkersOnly),
        InlineData(ChartType.ScatterWithStraightLines),
        InlineData(ChartType.ScatterWithSmoothLines)
    ]
    public async Task ScatterChart_WithXValues_RoundTrips(ChartType type)
    {
        var doc = PptxFixtures.WithSlides(1);
        var chart = doc.Slides[0]
            .Shapes.AddChart(type, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(6), Emu.FromInches(4));
        var series = new ChartSeries { Name = "XY" };
        series.XValues.AddRange([1.0, 2.0, 3.0]);
        series.Values.AddRange([4.0, 5.0, 6.0]);
        chart.Chart.Data.Series.Add(series);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var rc = reloaded.Slides[0].Shapes.OfType<ChartShape>().Single();
        rc.Chart.Type.ShouldBe(type);
        rc.Chart.Data.Series[0].Values.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ChartTitle_RoundTrips()
    {
        var doc = PptxFixtures.WithSlides(1);
        var chart = AddChartWithData(doc, ChartType.ColumnClustered);
        chart.Chart.HasTitle = true;
        chart.Chart.Title = "Quarterly Revenue";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var rc = reloaded.Slides[0].Shapes.OfType<ChartShape>().Single();
        rc.Chart.HasTitle.ShouldBeTrue();
        rc.Chart.Title.ShouldBe("Quarterly Revenue");
    }

    [
        Theory,
        InlineData(ChartLegendPosition.Top),
        InlineData(ChartLegendPosition.Left),
        InlineData(ChartLegendPosition.Right),
        InlineData(ChartLegendPosition.Bottom)
    ]
    public async Task LegendPosition_RoundTrips(ChartLegendPosition position)
    {
        var doc = PptxFixtures.WithSlides(1);
        var chart = AddChartWithData(doc, ChartType.ColumnClustered);
        chart.Chart.Legend.IsVisible = true;
        chart.Chart.Legend.Position = position;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var rc = reloaded.Slides[0].Shapes.OfType<ChartShape>().Single();
        rc.Chart.Legend.IsVisible.ShouldBeTrue();
        rc.Chart.Legend.Position.ShouldBe(position);
    }

    [Fact]
    public async Task MultipleSeries_RoundTrip()
    {
        var doc = PptxFixtures.WithSlides(1);
        var chart = doc.Slides[0]
            .Shapes.AddChart(ChartType.ColumnClustered, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(6), Emu.FromInches(4));
        chart.Chart.Data.Categories.AddRange(["Q1", "Q2"]);
        for (var i = 0; i < 3; i++)
        {
            var s = new ChartSeries { Name = $"Series {i}" };
            s.Values.AddRange([i + 1.0, i + 2.0]);
            chart.Chart.Data.Series.Add(s);
        }

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var rc = reloaded.Slides[0].Shapes.OfType<ChartShape>().Single();
        rc.Chart.Data.Series.Count.ShouldBe(3);
        rc.Chart.Data.Categories.Count.ShouldBe(2);
    }

    [Fact]
    public async Task NoLegend_RoundTripsAsInvisible()
    {
        var doc = PptxFixtures.WithSlides(1);
        var chart = AddChartWithData(doc, ChartType.Pie);
        chart.Chart.Legend.IsVisible = false;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var rc = reloaded.Slides[0].Shapes.OfType<ChartShape>().Single();
        rc.Chart.Legend.IsVisible.ShouldBeFalse();
    }
}
