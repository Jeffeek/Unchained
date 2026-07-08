using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>M-D: rich chart model — axes (title/min/max/gridlines/number-format) round-trip.</summary>
public sealed class RichChartTests : PptxTestBase
{
    private static ChartShape NewColumnChart(out PresentationDocument doc)
    {
        doc = PptxFixtures.WithSlides(1);
        var chart = doc.Slides[0]
            .Shapes.AddChart(
                ChartType.ColumnClustered,
                Emu.FromInches(1),
                Emu.FromInches(1),
                Emu.FromInches(6),
                Emu.FromInches(4)
            );
        chart.Chart.Data.Categories.AddRange(["Q1", "Q2", "Q3"]);
        var series = new ChartSeries { Name = "Revenue" };
        series.Values.AddRange([10, 20, 30]);
        chart.Chart.Data.Series.Add(series);
        return chart;
    }

    [Fact]
    public async Task ValueAxis_MinMaxGridlinesTitle_RoundTrip()
    {
        var chart = NewColumnChart(out var doc);
        var v = chart.Chart.ValueAxis;
        v.Minimum = 0;
        v.Maximum = 50;
        v.MajorUnit = 10;
        v.HasMajorGridlines = true;
        v.Title = "Sales (USD)";
        v.NumberFormat = "0.0";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var rv = reloaded.Slides[0].Shapes.OfType<ChartShape>().Single().Chart.ValueAxis;
        rv.Minimum.ShouldBe(0);
        rv.Maximum.ShouldBe(50);
        rv.MajorUnit.ShouldBe(10);
        rv.HasMajorGridlines.ShouldBeTrue();
        rv.Title.ShouldBe("Sales (USD)");
        rv.NumberFormat.ShouldBe("0.0");
    }

    [Fact]
    public async Task CategoryAxis_TitleAndVisibility_RoundTrip()
    {
        var chart = NewColumnChart(out var doc);
        chart.Chart.CategoryAxis.Title = "Quarter";
        chart.Chart.CategoryAxis.IsVisible = false;

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var c = reloaded.Slides[0].Shapes.OfType<ChartShape>().Single().Chart.CategoryAxis;
        c.Title.ShouldBe("Quarter");
        c.IsVisible.ShouldBeFalse();
    }

    [Fact]
    public async Task SeriesFill_DataLabels_Trendline_RoundTrip()
    {
        var chart = NewColumnChart(out var doc);
        var s = chart.Chart.Data.Series[0];
        s.Fill = new FillFormat();
        s.Fill.SetSolid(ColorSpec.FromRgb(0xC0, 0x00, 0x00));
        s.DataLabels = new ChartDataLabels { IsVisible = true, ShowValue = true, ShowPercentage = true, Position = "outEnd" };
        s.Trendline = new ChartTrendline { Type = "linear", DisplayEquation = true, DisplayRSquared = true };

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var rs = reloaded.Slides[0].Shapes.OfType<ChartShape>().Single().Chart.Data.Series[0];

        rs.Fill.ShouldNotBeNull();
        rs.Fill.Solid.ShouldNotBeNull();
        rs.DataLabels.ShouldNotBeNull();
        rs.DataLabels.ShowPercentage.ShouldBeTrue();
        rs.DataLabels.Position.ShouldBe("outEnd");
        rs.Trendline.ShouldNotBeNull();
        rs.Trendline.Type.ShouldBe("linear");
        rs.Trendline.DisplayEquation.ShouldBeTrue();
    }
}
