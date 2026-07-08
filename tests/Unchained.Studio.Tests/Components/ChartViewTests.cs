using Bunit;
using MudBlazor;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Drawing;
using Unchained.Studio.Components.Xlsx;
using OoxmlChartType = Unchained.Ooxml.Charts.ChartType;

namespace Unchained.Studio.Tests.Components;

public sealed class ChartViewTests : MudTestContext
{
    [Fact]
    public void Render_BarChart_SvgRendered()
    {
        var cut = Render<ChartView>(static pb => pb.Add(static c => c.Model, Bar(OoxmlChartType.ColumnClustered)));

        cut.Find("svg").ShouldNotBeNull();
    }

    [Fact]
    public void Render_BarChart_ChartReceivesCorrectData()
    {
        var cut = Render<ChartView>(static pb => pb.Add(static c => c.Model, Bar(OoxmlChartType.ColumnClustered)));

        var mudChart = cut.FindComponent<MudChart<double>>();
        var series = mudChart.Instance.ChartSeries;
        series.Count.ShouldBe(1);
        series[0].Name.ShouldBe("Series 1");
        series[0].Data.SequenceEqual(new[] { 10.0, 20.0, 30.0 }).ShouldBeTrue();
    }

    [Fact]
    public void Render_EmptySeries_NoCrash()
    {
        var cut = Render<ChartView>(static pb => pb.Add(static c => c.Model, new ChartModel { Type = OoxmlChartType.ColumnClustered, HasTitle = false }));

        cut.Markup.ShouldNotContain("preview unavailable");
    }

    [Fact]
    public void Render_PieChart_SvgRendered()
    {
        var cut = Render<ChartView>(static pb => pb.Add(static c => c.Model, Bar(OoxmlChartType.Pie)));
        cut.Find("svg").ShouldNotBeNull();
    }

    [Fact]
    public void Render_DoughnutChart_SvgRendered()
    {
        var cut = Render<ChartView>(static pb => pb.Add(static c => c.Model, Bar(OoxmlChartType.Doughnut)));
        cut.Find("svg").ShouldNotBeNull();
    }

    [Fact]
    public void Render_LineChart_SvgRendered()
    {
        var cut = Render<ChartView>(static pb => pb.Add(static c => c.Model, Bar(OoxmlChartType.Line)));
        cut.Find("svg").ShouldNotBeNull();
    }

    [Fact]
    public void Render_AreaChart_SvgRendered()
    {
        var cut = Render<ChartView>(static pb => pb.Add(static c => c.Model, Bar(OoxmlChartType.Area)));
        cut.Find("svg").ShouldNotBeNull();
    }

    [Fact]
    public void Render_StackedColumn_SvgRendered()
    {
        var cut = Render<ChartView>(static pb => pb.Add(static c => c.Model, Bar(OoxmlChartType.ColumnStacked)));
        cut.Find("svg").ShouldNotBeNull();
    }

    [Fact]
    public void Render_ScatterChart_FallbackRendered()
    {
        var cut = Render<ChartView>(static pb => pb.Add(static c => c.Model, Bar(OoxmlChartType.ScatterWithMarkersOnly)));
        cut.Markup.ShouldContain("preview unavailable");
    }

    [Fact]
    public void Render_RadarChart_FallbackRendered()
    {
        var cut = Render<ChartView>(static pb => pb.Add(static c => c.Model, Bar(OoxmlChartType.Radar)));
        cut.Markup.ShouldContain("preview unavailable");
    }

    [Fact]
    public void Render_ChartWithTitle_TitleRendered()
    {
        var model = Bar(OoxmlChartType.ColumnClustered);
        model.Title = "Revenue";
        model.HasTitle = true;

        var cut = Render<ChartView>(pb => pb.Add(static c => c.Model, model));

        cut.Find("div.sg-chart-title").TextContent.ShouldContain("Revenue");
    }

    [Fact]
    public void Render_ChartWithoutTitle_NoTitleDiv()
    {
        var model = Bar(OoxmlChartType.BarClustered);
        model.HasTitle = false;

        var cut = Render<ChartView>(pb => pb.Add(static c => c.Model, model));

        cut.Markup.ShouldNotContain("sg-chart-title");
    }

    [Fact]
    public void Render_ChartWithSeriesFill_ColorFromFill()
    {
        var model = Bar(OoxmlChartType.ColumnClustered);
        var fill = new FillFormat();
        fill.SetSolid(ColorSpec.FromRgb(0xAA, 0xBB, 0xCC));
        model.Data.Series[0].Fill = fill;

        var cut = Render<ChartView>(pb => pb.Add(static c => c.Model, model));

        // ReSharper disable once StringLiteralTypo
        cut.Markup.ShouldContain("AABBCC");
    }

    [
        Theory,
        InlineData(OoxmlChartType.ColumnClustered, true),
        InlineData(OoxmlChartType.Line, true),
        InlineData(OoxmlChartType.Pie, true),
        InlineData(OoxmlChartType.Doughnut, true),
        InlineData(OoxmlChartType.Area, true),
        InlineData(OoxmlChartType.BarClustered, true),
        InlineData(OoxmlChartType.ScatterWithMarkersOnly, false),
        InlineData(OoxmlChartType.Bubble, false),
        InlineData(OoxmlChartType.Radar, false),
        InlineData(OoxmlChartType.RadarWithMarkers, false)
    ]
    public void Render_ChartType_HasExpectedFallback(OoxmlChartType type, bool expectedSupported)
    {
        var cut = Render<ChartView>(pb => pb.Add(static c => c.Model, Bar(type)));

        if (expectedSupported)
            cut.Markup.ShouldNotContain("preview unavailable");
        else
            cut.Markup.ShouldContain("preview unavailable");
    }

    private static ChartModel Bar(OoxmlChartType type)
    {
        var model = new ChartModel { Type = type, HasTitle = false };
        model.Data.Categories.Add("A");
        model.Data.Categories.Add("B");
        model.Data.Categories.Add("C");
        var series = new ChartSeries { Name = "Series 1" };
        series.Values.Add(10);
        series.Values.Add(20);
        series.Values.Add(30);
        model.Data.Series.Add(series);
        return model;
    }
}
