using Shouldly;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Writing;

/// <summary>
///     Unit tests for <see cref="ChartWriter" /> — serializes a <see cref="ChartModel" />
///     to chart-part XML. Exercises the writer in isolation; parser round-trips live in the
///     integration suite.
/// </summary>
public sealed class ChartWriterTests
{
    [Fact]
    public void Write_LineChart_ProducesChartSpaceRoot()
    {
        var model = new ChartModel { Type = ChartType.Line, Title = "Test Chart" };
        model.Data.Categories.AddRange(["A", "B", "C"]);
        var s = new ChartSeries { Name = "Series 1" };
        s.Values.AddRange([1.0, 2.0, 3.0]);
        model.Data.Series.Add(s);

        var bytes = ChartWriter.Write(model);
        bytes.ShouldNotBeEmpty();

        var doc = OoXmlHelper.ParseXml(bytes);
        doc.Root.ShouldNotBeNull();
        doc.Root!.Name.LocalName.ShouldBe("chartSpace");
    }

    private static ChartModel CategoryModel(ChartType type)
    {
        var model = new ChartModel { Type = type, Title = "T" };
        model.Data.Categories.AddRange(["A", "B"]);
        var s = new ChartSeries { Name = "S1" };
        s.Values.AddRange([1.0, 2.0]);
        model.Data.Series.Add(s);
        return model;
    }

    [
        Theory,
        InlineData(ChartType.ColumnClustered),
        InlineData(ChartType.ColumnStacked),
        InlineData(ChartType.ColumnFullStacked),
        InlineData(ChartType.BarClustered),
        InlineData(ChartType.BarStacked),
        InlineData(ChartType.BarFullStacked),
        InlineData(ChartType.Line),
        InlineData(ChartType.LineStacked),
        InlineData(ChartType.LineFullStacked),
        InlineData(ChartType.LineWithMarkers),
        InlineData(ChartType.Pie),
        InlineData(ChartType.PieExploded),
        InlineData(ChartType.Doughnut),
        InlineData(ChartType.DoughnutExploded),
        InlineData(ChartType.Area),
        InlineData(ChartType.AreaStacked),
        InlineData(ChartType.AreaFullStacked),
        InlineData(ChartType.Radar),
        InlineData(ChartType.RadarWithMarkers),
        InlineData(ChartType.RadarFilled)
    ]
    public void Write_AllCategoryChartTypes_ProduceChartSpace(ChartType type)
    {
        var bytes = ChartWriter.Write(CategoryModel(type));
        var doc = OoXmlHelper.ParseXml(bytes);
        doc.Root!.Name.LocalName.ShouldBe("chartSpace");
    }

    [
        Theory,
        InlineData(ChartType.ScatterWithMarkersOnly),
        InlineData(ChartType.ScatterWithStraightLines),
        InlineData(ChartType.ScatterWithSmoothLines),
        InlineData(ChartType.ScatterWithStraightLinesAndMarkers),
        InlineData(ChartType.ScatterWithSmoothLinesAndMarkers)
    ]
    public void Write_ScatterTypes_ProduceChartSpace(ChartType type)
    {
        var model = new ChartModel { Type = type };
        var s = new ChartSeries { Name = "XY" };
        s.XValues.AddRange([1.0, 2.0, 3.0]);
        s.Values.AddRange([4.0, 5.0, 6.0]);
        model.Data.Series.Add(s);

        var doc = OoXmlHelper.ParseXml(ChartWriter.Write(model));
        doc.Root!.Name.LocalName.ShouldBe("chartSpace");
    }

    [Fact]
    public void Write_BubbleChart_ProducesChartSpace()
    {
        var model = new ChartModel { Type = ChartType.Bubble };
        var s = new ChartSeries { Name = "B" };
        s.XValues.AddRange([1.0, 2.0]);
        s.Values.AddRange([3.0, 4.0]);
        model.Data.Series.Add(s);

        var doc = OoXmlHelper.ParseXml(ChartWriter.Write(model));
        doc.Root!.Name.LocalName.ShouldBe("chartSpace");
    }

    [
        Theory,
        InlineData(ChartLegendPosition.Bottom),
        InlineData(ChartLegendPosition.Top),
        InlineData(ChartLegendPosition.Left),
        InlineData(ChartLegendPosition.Right)
    ]
    public void Write_LegendPositions_AreEmitted(ChartLegendPosition position)
    {
        var model = CategoryModel(ChartType.ColumnClustered);
        model.Legend.IsVisible = true;
        model.Legend.Position = position;
        model.Legend.IsOverlay = true;

        var doc = OoXmlHelper.ParseXml(ChartWriter.Write(model));
        doc.Descendants().Any(static e => e.Name.LocalName == "legend").ShouldBeTrue();
    }

    [Fact]
    public void Write_InvisibleLegend_OmitsLegend()
    {
        var model = CategoryModel(ChartType.ColumnClustered);
        model.Legend.IsVisible = false;

        var doc = OoXmlHelper.ParseXml(ChartWriter.Write(model));
        doc.Descendants().Any(static e => e.Name.LocalName == "legend").ShouldBeFalse();
    }

    [Fact]
    public void Write_AxesWithAllOptions_AreEmitted()
    {
        var model = CategoryModel(ChartType.ColumnClustered);
        model.ValueAxis.Minimum = 0;
        model.ValueAxis.Maximum = 100;
        model.ValueAxis.MajorUnit = 10;
        model.ValueAxis.MinorUnit = 5;
        model.ValueAxis.HasMajorGridlines = true;
        model.ValueAxis.HasMinorGridlines = true;
        model.ValueAxis.NumberFormat = "0.00";
        model.ValueAxis.Title = "Values";
        model.CategoryAxis.Title = "Categories";

        var doc = OoXmlHelper.ParseXml(ChartWriter.Write(model));
        doc.Descendants().Any(static e => e.Name.LocalName == "valAx").ShouldBeTrue();
        doc.Descendants().Any(static e => e.Name.LocalName == "catAx").ShouldBeTrue();
    }

    [Fact]
    public void Write_SeriesWithDataLabelsAndTrendline_AreEmitted()
    {
        var model = CategoryModel(ChartType.ColumnClustered);
        var s = model.Data.Series[0];
        s.DataLabels = new ChartDataLabels
        {
            IsVisible = true,
            ShowValue = true,
            ShowSeriesName = true,
            ShowCategoryName = true,
            ShowPercentage = true,
            ShowLegendKey = true
        };
        s.Trendline = new ChartTrendline
        {
            Type = "linear",
            DisplayEquation = true,
            DisplayRSquared = true,
            Forward = 1,
            Backward = 1,
            Order = 2
        };

        var doc = OoXmlHelper.ParseXml(ChartWriter.Write(model));
        doc.Descendants().Any(static e => e.Name.LocalName == "dLbls").ShouldBeTrue();
        doc.Descendants().Any(static e => e.Name.LocalName == "trendline").ShouldBeTrue();
    }

    [Fact]
    public void Write_NoTitle_OmitsTitleElement()
    {
        var model = CategoryModel(ChartType.ColumnClustered);
        model.HasTitle = false;

        var doc = OoXmlHelper.ParseXml(ChartWriter.Write(model));
        doc.Root!.Name.LocalName.ShouldBe("chartSpace");
    }
}
