using Bunit;
using MudBlazor;
using MudBlazor.Services;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Drawing;
using Unchained.Studio.Components.Xlsx;
using OoxmlChartType = Unchained.Ooxml.Charts.ChartType;

namespace Unchained.Studio.Tests.Components;

public sealed class ChartViewTests : BunitContext
{
    public ChartViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    [Fact]
    public void Render_BarChart_SvgRendered()
    {
        var cut = Render<ChartView>(pb => pb.Add(c => c.Model, Bar(OoxmlChartType.ColumnClustered)));
        cut.Find("svg").ShouldNotBeNull();
    }

    [Fact]
    public void Render_PieChart_SvgRendered()
    {
        var cut = Render<ChartView>(pb => pb.Add(c => c.Model, Bar(OoxmlChartType.Pie)));
        cut.Find("svg").ShouldNotBeNull();
    }

    [Fact]
    public void Render_DoughnutChart_SvgRendered()
    {
        var cut = Render<ChartView>(pb => pb.Add(c => c.Model, Bar(OoxmlChartType.Doughnut)));
        cut.Find("svg").ShouldNotBeNull();
    }

    [Fact]
    public void Render_LineChart_SvgRendered()
    {
        var cut = Render<ChartView>(pb => pb.Add(c => c.Model, Bar(OoxmlChartType.Line)));
        cut.Find("svg").ShouldNotBeNull();
    }

    [Fact]
    public void Render_AreaChart_SvgRendered()
    {
        var cut = Render<ChartView>(pb => pb.Add(c => c.Model, Bar(OoxmlChartType.Area)));
        cut.Find("svg").ShouldNotBeNull();
    }

    [Fact]
    public void Render_StackedColumn_SvgRendered()
    {
        var cut = Render<ChartView>(pb => pb.Add(c => c.Model, Bar(OoxmlChartType.ColumnStacked)));
        cut.Find("svg").ShouldNotBeNull();
    }

    [Fact]
    public void Render_ScatterChart_FallbackRendered()
    {
        var cut = Render<ChartView>(pb => pb.Add(c => c.Model, Bar(OoxmlChartType.ScatterWithMarkersOnly)));
        cut.Markup.ShouldContain("preview unavailable");
    }

    [Fact]
    public void Render_RadarChart_FallbackRendered()
    {
        var cut = Render<ChartView>(pb => pb.Add(c => c.Model, Bar(OoxmlChartType.Radar)));
        cut.Markup.ShouldContain("preview unavailable");
    }

    [Fact]
    public void Render_ChartWithTitle_TitleRendered()
    {
        var model = Bar(OoxmlChartType.ColumnClustered);
        model.Title = "Revenue";
        model.HasTitle = true;

        var cut = Render<ChartView>(pb => pb.Add(c => c.Model, model));

        cut.Find("div.sg-chart-title").TextContent.ShouldContain("Revenue");
    }

    [Fact]
    public void Render_ChartWithoutTitle_NoTitleDiv()
    {
        var model = Bar(OoxmlChartType.BarClustered);
        model.HasTitle = false;

        var cut = Render<ChartView>(pb => pb.Add(c => c.Model, model));

        cut.Markup.ShouldNotContain("sg-chart-title");
    }

    [Fact]
    public void Render_ChartWithSeriesFill_ColorFromFill()
    {
        var model = Bar(OoxmlChartType.ColumnClustered);
        var fill = new FillFormat();
        fill.SetSolid(ColorSpec.FromRgb(0xAA, 0xBB, 0xCC));
        model.Data.Series[0].Fill = fill;

        var cut = Render<ChartView>(pb => pb.Add(c => c.Model, model));

        cut.Markup.ShouldContain("AABBCC");
    }

    [
        Theory,
        InlineData(OoxmlChartType.ColumnClustered),
        InlineData(OoxmlChartType.Line),
        InlineData(OoxmlChartType.Pie),
        InlineData(OoxmlChartType.Doughnut),
        InlineData(OoxmlChartType.Area),
        InlineData(OoxmlChartType.BarClustered)
    ]
    public void Render_SupportedTypes_NoFallback(OoxmlChartType type)
    {
        var cut = Render<ChartView>(pb => pb.Add(c => c.Model, Bar(type)));
        cut.Markup.ShouldNotContain("preview unavailable");
    }

    [
        Theory,
        InlineData(OoxmlChartType.ScatterWithMarkersOnly),
        InlineData(OoxmlChartType.Bubble),
        InlineData(OoxmlChartType.Radar),
        InlineData(OoxmlChartType.RadarWithMarkers)
    ]
    public void Render_UnsupportedTypes_Fallback(OoxmlChartType type)
    {
        var cut = Render<ChartView>(pb => pb.Add(c => c.Model, Bar(type)));
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
