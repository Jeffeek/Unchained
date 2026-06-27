using Shouldly;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Xml;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Charts;

/// <summary>
///     Round-trip tests for the shared <see cref="ChartXmlWriter" /> / <see cref="ChartXmlReader" />
///     used by both PPTX and XLSX. Verifies a model survives serialize → parse unchanged.
/// </summary>
public sealed class ChartXmlRoundTripTests
{
    private static ChartModel RoundTrip(ChartModel model)
    {
        var bytes = ChartXmlWriter.Write(model);
        var root = OoXmlHelper.ParseXml(bytes).Root!;
        return ChartXmlReader.Parse(root);
    }

    private static ChartModel SampleColumn()
    {
        var model = new ChartModel
        {
            Type = ChartType.ColumnClustered,
            Title = "Sales",
            HasTitle = true,
            Legend =
            {
                IsVisible = true,
                Position = ChartLegendPosition.Right
            }
        };
        model.Data.Categories.AddRange(["Jan", "Feb", "Mar"]);
        model.Data.Series.Add(new ChartSeries { Name = "2023", Values = { 10, 20, 30 } });
        model.Data.Series.Add(new ChartSeries { Name = "2024", Values = { 15, 25, 35 } });
        return model;
    }

    [Fact]
    public void ColumnChart_RoundTrips()
    {
        var result = RoundTrip(SampleColumn());

        result.Type.ShouldBe(ChartType.ColumnClustered);
        result.HasTitle.ShouldBeTrue();
        result.Title.ShouldBe("Sales");
        result.Legend.IsVisible.ShouldBeTrue();
        result.Legend.Position.ShouldBe(ChartLegendPosition.Right);
        result.Data.Categories.ShouldBe(["Jan", "Feb", "Mar"]);
        result.Data.Series.Count.ShouldBe(2);
        result.Data.Series[0].Name.ShouldBe("2023");
        result.Data.Series[0].Values.ShouldBe([10, 20, 30]);
        result.Data.Series[1].Values.ShouldBe([15, 25, 35]);
    }

    [
        Theory,
        InlineData(ChartType.ColumnClustered),
        InlineData(ChartType.BarStacked),
        InlineData(ChartType.Line),
        InlineData(ChartType.LineWithMarkers),
        InlineData(ChartType.Pie),
        InlineData(ChartType.Doughnut),
        InlineData(ChartType.Area),
        InlineData(ChartType.Radar)
    ]
    public void ChartType_RoundTrips(ChartType type)
    {
        var model = new ChartModel { Type = type };
        model.Data.Categories.AddRange(["A", "B"]);
        model.Data.Series.Add(new ChartSeries { Name = "S", Values = { 1, 2 } });

        RoundTrip(model).Type.ShouldBe(type);
    }

    [Fact]
    public void Scatter_RoundTripsXAndYValues()
    {
        var model = new ChartModel { Type = ChartType.ScatterWithMarkersOnly };
        model.Data.Series.Add(new ChartSeries { Name = "pts", XValues = { 1, 2, 3 }, Values = { 4, 5, 6 } });

        var result = RoundTrip(model);
        result.Type.ShouldBe(ChartType.ScatterWithMarkersOnly);
        result.Data.Series[0].XValues.ShouldBe([1, 2, 3]);
        result.Data.Series[0].Values.ShouldBe([4, 5, 6]);
    }

    [Fact]
    public void Axes_TitleAndBounds_RoundTrip()
    {
        var model = SampleColumn();
        model.ValueAxis.Title = "Units";
        model.ValueAxis.Minimum = 0;
        model.ValueAxis.Maximum = 100;
        model.CategoryAxis.Title = "Month";

        var result = RoundTrip(model);
        result.ValueAxis.Title.ShouldBe("Units");
        result.ValueAxis.Minimum.ShouldBe(0);
        result.ValueAxis.Maximum.ShouldBe(100);
        result.CategoryAxis.Title.ShouldBe("Month");
    }

    [Fact]
    public void NoTitle_NoLegend_RoundTrips()
    {
        var model = new ChartModel
        {
            Type = ChartType.Pie,
            HasTitle = false,
            Legend =
            {
                IsVisible = false
            }
        };
        model.Data.Categories.AddRange(["X", "Y"]);
        model.Data.Series.Add(new ChartSeries { Name = "s", Values = { 60, 40 } });

        var result = RoundTrip(model);
        result.HasTitle.ShouldBeFalse();
        result.Legend.IsVisible.ShouldBeFalse();
        result.Data.Series[0].Values.ShouldBe([60, 40]);
    }
}
