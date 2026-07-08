using Shouldly;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Drawing;
using Unchained.Xlsx.Drawings;
using Unchained.Xlsx.Extensions.Highcharts;
using Unchained.Xlsx.Extensions.Highcharts.Models;
using Xunit;

namespace Unchained.Xlsx.Extensions.Tests.UnitTests;

/// <summary>
///     Tests for <see cref="HighchartsConverter" /> — baseline and V2.0 features
///     (stacking, dual-axis, datetime, legend), plus V3 deep nesting.
/// </summary>
public class HighchartsConverterTests
{
    private static ChartDrawing BuildChart(
        ChartType type,
        string title,
        List<string> categories,
        List<(string name, List<double> values)> seriesData
    )
    {
        var drawing = new ChartDrawing
        {
            Chart =
            {
                Type = type,
                Title = title
            }
        };

        foreach (var cat in categories)
            drawing.Chart.Data.Categories.Add(cat);

        foreach (var (name, values) in seriesData)
        {
            var series = new ChartSeries { Name = name };
            series.Values.AddRange(values);
            drawing.Chart.Data.Series.Add(series);
        }

        return drawing;
    }

    // ── V1 features ────────────────────────────────────────────────────────

    [Theory, InlineData(ChartType.ColumnClustered, "column"), InlineData(ChartType.BarClustered, "bar"), InlineData(ChartType.LineWithMarkers, "line"),
     InlineData(ChartType.Pie, "pie"), InlineData(ChartType.Doughnut, "doughnut"), InlineData(ChartType.AreaStacked, "area"),
     InlineData(ChartType.ScatterWithMarkersOnly, "scatter"), InlineData(ChartType.Bubble, "bubble")]
    public void Convert_MapChartType_ChartDrawing(ChartType type, string expectedHighchartsType)
    {
        var chart = BuildChart(type, "Sales Report", ["Jan", "Feb", "Mar"], [("Revenue", [100.0, 200.0, 150.0])]);

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.Chart.Type.ShouldBe(expectedHighchartsType);
    }

    [Fact]
    public void Convert_NullTitle_DefaultsToUntitledChart()
    {
        var chart = BuildChart(ChartType.ColumnClustered, string.Empty, ["Q1", "Q2"], [("Product A", [50.0, 60.0])]);

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.Title.Text.ShouldBe("Untitled Chart");
    }

    [Fact]
    public void Convert_DataSeries_PopulatesDataArrayCorrectly()
    {
        var categories = new List<string> { "January", "February", "March", "April" };
        var seriesA = new List<double> { 10.0, 20.0, 30.0, 40.0 };
        var seriesB = new List<double> { 100.0, 200.0, 300.0, 400.0 };

        var chart = BuildChart(ChartType.Line, "Monthly Revenue", categories, [("Revenue A", seriesA), ("Revenue B", seriesB)]);

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.XAxis.ShouldNotBeNull();
        result.XAxis.Categories.ShouldBe(categories);
        result.Series.Count.ShouldBe(2);

        var series1 = result.Series[0];
        series1.Name.ShouldBe("Revenue A");
        series1.Type.ShouldBe("line");
        series1.Data.ShouldBe(seriesA.Cast<double?>().ToList());

        var series2 = result.Series[1];
        series2.Name.ShouldBe("Revenue B");
        series2.Data.ShouldBe(seriesB.Cast<double?>().ToList());
    }

    [Fact]
    public void Convert_EmptyCategories_NoXAxisCategories()
    {
        var chart = BuildChart(ChartType.Pie, "Distribution", [], [("Category", [50.0, 30.0, 20.0])]);

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.XAxis.ShouldNotBeNull();
        result.XAxis.Categories.ShouldBeNull();
        result.Series[0].Data.Count.ShouldBe(3);
    }

    [Fact]
    public void Convert_ExplicitFillColor_ExtractsHexColor()
    {
        var chart = BuildChart(ChartType.ColumnClustered, "Coloured Chart", ["A", "B"], [("Red Series", [1.0, 2.0])]);

        chart.Chart.Data.Series[0].Fill = new()
        {
            Type = FillType.Solid,
            Solid = new SolidFill { Color = ColorSpec.FromRgb(0xCC, 0x44, 0x11) }
        };

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.Series[0].Color.ShouldBe("#CC4411");
    }

    [Fact]
    public void Convert_NoExplicitFillColor_ColorIsNull()
    {
        var chart = BuildChart(ChartType.Line, "Themed Chart", ["X", "Y"], [("Series", [1.0, 2.0])]);
        chart.Chart.Data.Series[0].Fill = null;

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.Series[0].Color.ShouldBeNull();
    }

    [Fact]
    public void Convert_NaNAndInfinityData_BecomesNullInDataArray()
    {
        var chart = BuildChart(ChartType.Line, "Edge Cases", ["A", "B", "C"], [("Data", [1.0, double.NaN, double.PositiveInfinity])]);

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.Series[0].Data[0].ShouldBe(1.0);
        result.Series[0].Data[1].ShouldBeNull();
        result.Series[0].Data[2].ShouldBeNull();
    }

    [Fact]
    public void Convert_SerializesToJson_CamelCaseNoNulls()
    {
        var chart = BuildChart(ChartType.ColumnClustered, "Test Chart", ["Jan", "Feb"], [("Sales", [10.0, 20.0])]);

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        var json = result.ToJson();

        json.ShouldContain("\"chart\"");
        json.ShouldContain("\"type\"");
        json.ShouldContain("\"title\"");
        json.ShouldContain("\"xaxis\"");
        json.ShouldContain("\"series\"");
        json.ShouldContain("\"name\"");
        json.ShouldContain("\"data\"");
        json.ShouldContain("\"color\"");
    }

    #region Colors

    [Fact]
    public void Convert_SeriesWithFills_ColorsFromSeries()
    {
        var chart = BuildChart(ChartType.ColumnClustered, "Coloured", ["A", "B"], [("S1", [1.0, 2.0]), ("S2", [3.0, 4.0])]);

        chart.Chart.Data.Series[0].Fill = new()
        {
            Type = FillType.Solid,
            Solid = new SolidFill { Color = ColorSpec.FromRgb(0xCC, 0x44, 0x11) }
        };
        chart.Chart.Data.Series[1].Fill = new()
        {
            Type = FillType.Solid,
            Solid = new SolidFill { Color = ColorSpec.FromRgb(0x11, 0x22, 0x33) }
        };

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.Colors.ShouldBe(["#CC4411", "#112233"]);
    }

    [Fact]
    public void Convert_NoSeriesFills_ColorsIsNull()
    {
        var chart = BuildChart(ChartType.Line, "Themed", ["A", "B"], [("S1", [1.0, 2.0])]);
        chart.Chart.Data.Series[0].Fill = null;

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.Colors.ShouldBeNull();
    }

    #endregion

    #region YAxis Config

    [Fact]
    public void Convert_ConvertsYAxisTitle()
    {
        var chart = BuildChart(ChartType.ColumnClustered, "Y", ["A"], [("X", [1.0])]);
        chart.Chart.ValueAxis.Title = "Revenue ($)";
        chart.Chart.ValueAxis.Minimum = 0;
        chart.Chart.ValueAxis.Maximum = 1000;

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.YAxis[0].Title!.ShouldBe("Revenue ($)");
        result.YAxis[0].Min.ShouldBe(0);
        result.YAxis[0].Max.ShouldBe(1000);
    }

    #endregion

    #region Tooltip

    [Fact]
    public void Convert_Tooltip_CreatedWithDefaults()
    {
        var chart = BuildChart(ChartType.Line, "Test", ["A"], [("X", [1.0])]);

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.Tooltip.ShouldNotBeNull();
        result.Tooltip.Crosshairs.ShouldNotBeNull();
        result.Tooltip.Crosshairs.Value.ShouldBe(true);
        result.Tooltip.Snap.ShouldNotBeNull();
        result.Tooltip.Snap.Value.ShouldBe(true);
    }

    #endregion

    // ── V2 features ────────────────────────────────────────────────────────

    #region Stacking

    [Fact]
    public void Convert_ColumnStacked_ProducesNormalStacking()
    {
        var chart = BuildChart(
            ChartType.ColumnStacked,
            "Stacked Sales",
            ["Jan", "Feb", "Mar"],
            [("North", [100.0, 200.0, 150.0]), ("South", [50.0, 80.0, 60.0])]
        );

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.PlotOptions.ShouldNotBeNull();
        result.PlotOptions.Series!.Stacking.ShouldBe("normal");
    }

    [Fact]
    public void Convert_ColumnFullStacked_ProducesPercentStacking()
    {
        var chart = BuildChart(
            ChartType.ColumnFullStacked,
            "Market Share",
            ["Q1", "Q2"],
            [("A", [30.0, 40.0]), ("B", [70.0, 60.0])]
        );

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.PlotOptions.ShouldNotBeNull();
        result.PlotOptions.Series!.Stacking.ShouldBe("percent");
    }

    [Fact]
    public void Convert_BarStacked_ProducesBarWithNormalStacking()
    {
        var chart = BuildChart(
            ChartType.BarStacked,
            "Horizontal Stacked",
            ["Jan", "Feb"],
            [("X", [1.0, 2.0]), ("Y", [3.0, 4.0])]
        );

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.Chart.Type.ShouldBe("bar");
        result.PlotOptions!.Series!.Stacking.ShouldBe("normal");
    }

    [Fact]
    public void Convert_StackedJson_ProducesCorrectNestedKey()
    {
        var chart = BuildChart(
            ChartType.LineStacked,
            "Stacked Line",
            ["A", "B"],
            [("S1", [1.0, 2.0])]
        );

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        var json = result.ToJson();

        json.ShouldContain("\"plotOptions\"");
        json.ShouldContain("\"series\"");
        json.ShouldContain("\"stacking\"");
        json.ShouldContain("\"normal\"");
    }

    #endregion

    #region Dual Axis

    [Fact]
    public void Convert_SingleSeries_NoSecondaryAxis()
    {
        var chart = BuildChart(
            ChartType.Line,
            "Single",
            ["A", "B"],
            [("S1", [1.0, 2.0])]
        );

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.YAxis.Count.ShouldBe(1);
        result.YAxis[0].Index.ShouldBe(0);
        result.YAxis[0].Opposite.ShouldBeFalse();
    }

    [Fact]
    public void Convert_SecondarySeries_AddsSecondaryYAxis()
    {
        var chart = BuildChart(
            ChartType.Line,
            "Mixed",
            ["A", "B"],
            [("Primary", [1.0, 2.0]), ("Secondary", [100.0, 200.0])]
        );

        // Mark the second series as secondary
        chart.Chart.Data.Series[1].DataLabels = new() { Position = "secondary" };

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.YAxis.Count.ShouldBe(2);
        result.YAxis[0].Index.ShouldBe(0);
        result.YAxis[1].Index.ShouldBe(1);
        result.YAxis[1].Opposite.ShouldBeTrue();

        result.Series[0].YAxis.ShouldBe(0);
        result.Series[1].YAxis.ShouldBe(1);
    }

    [Fact]
    public void Convert_MultipleSeries_AllPrimary_ByDefault()
    {
        var chart = BuildChart(
            ChartType.ColumnClustered,
            "Multi",
            ["A", "B"],
            [("A1", [1.0]), ("A2", [2.0]), ("A3", [3.0])]
        );

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.Series.All(static s => s.YAxis == 0).ShouldBeTrue();
    }

    #endregion

    #region Legend

    [Fact]
    public void Convert_LegendBottom_DefaultConfig()
    {
        var chart = BuildChart(ChartType.Pie, "Pie", [], [("Slice", [50.0])]);
        chart.Chart.Legend.Position = ChartLegendPosition.Bottom;

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.Legend.ShouldNotBeNull();
        result.Legend.Enabled.ShouldBeTrue();
        result.Legend.VerticalAlign.ShouldBe("bottom");
        result.Legend.Align.ShouldBe("center");
    }

    [Fact]
    public void Convert_LegendLeft_VerticalLayout()
    {
        var chart = BuildChart(ChartType.ColumnClustered, "Cols", ["A"], [("X", [1.0])]);
        chart.Chart.Legend.Position = ChartLegendPosition.Left;

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.Legend.ShouldNotBeNull();
        result.Legend.LayoutAlign.ShouldBe("vertical");
        result.Legend.Align.ShouldBe("left");
        result.Legend.VerticalAlign.ShouldBe("middle");
    }

    [Fact]
    public void Convert_LegendRight_RightAligned()
    {
        var chart = BuildChart(ChartType.Line, "Line", ["A"], [("X", [1.0])]);
        chart.Chart.Legend.Position = ChartLegendPosition.Right;

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.Legend.ShouldNotBeNull();
        result.Legend.Align.ShouldBe("right");
        result.Legend.VerticalAlign.ShouldBe("middle");
    }

    [Fact]
    public void Convert_LegendHidden_Disabled()
    {
        var chart = BuildChart(ChartType.Pie, "No Legend", [], [("X", [1.0])]);
        chart.Chart.Legend.IsVisible = false;

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.Legend.ShouldNotBeNull();
        result.Legend.Enabled.ShouldBeFalse();
    }

    #endregion

    #region X-Axis Type

    [Fact]
    public void Convert_CategoryAxis_DefaultType()
    {
        var chart = BuildChart(
            ChartType.ColumnClustered,
            "Cat",
            ["Jan", "Feb", "Mar"],
            [("Sales", [10.0, 20.0, 30.0])]
        );

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.XAxis.ShouldNotBeNull();
        result.XAxis.Type.ShouldBeNull();
    }

    [Fact]
    public void Convert_DateTimeFormattedAxis_DetectsDatetime()
    {
        var chart = BuildChart(
            ChartType.Line,
            "Time Series",
            [],
            [("Temp", [20.0, 21.0, 22.0])]
        );

        chart.Chart.CategoryAxis.NumberFormat = "yyyy-mm-dd";

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.XAxis.ShouldNotBeNull();
        result.XAxis.Type.ShouldBe("datetime");
    }

    #endregion

    // ── V3 features ────────────────────────────────────────────────────────

    #region DataPoint Model

    [Fact]
    public void DataPoint_IsComplex_True_WhenPropertiesSet()
    {
        var dp = new DataPoint { Y = 10.0, Color = "#FF0000", Sliced = true };
        dp.IsComplex.ShouldBe(true);
    }

    [Fact]
    public void DataPoint_IsComplex_False_WhenOnlyYSet()
    {
        var dp = new DataPoint { Y = 10.0 };
        dp.IsComplex.ShouldBe(false);
    }

    [Fact]
    public void DataPoint_SerializesAsObject_WhenComplex()
    {
        var dp = new DataPoint { Y = 106.4, Color = "#FF0000", Sliced = true, Selected = true };
        var json = dp.ToJson();

        json.ShouldContain("\"y\"");
        json.ShouldContain("\"color\"");
        json.ShouldContain("\"sliced\"");
        json.ShouldContain("\"selected\"");
    }

    [Fact]
    public void DataPoint_SerializesAsScalar_WhenSimple()
    {
        var dp = new DataPoint { Y = 29.9 };
        var json = dp.ToJson();

        json.ShouldBe("29.9");
    }

    [Fact]
    public void Convert_SeriesConfig_HasDataPointsProperty()
    {
        var chart = BuildChart(ChartType.Line, "Test", ["A"], [("X", [1.0])]);

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.Series[0].DataPoints.ShouldBeEmpty();
        result.Series[0].Data.ShouldNotBeEmpty();
    }

    #endregion

    #region Deep Nesting Validation

    [Fact]
    public void Convert_DeepChart_JsonContainsAllSections()
    {
        var chart = BuildChart(
            ChartType.LineStacked,
            "Deep",
            ["A", "B"],
            [("S1", [10.0, 20.0]), ("S2", [30.0, 40.0])]
        );

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        var json = result.ToJson();

        json.ShouldContain("\"chart\"");
        json.ShouldContain("\"title\"");
        json.ShouldContain("\"xaxis\"");
        json.ShouldContain("\"yaxis\"");
        json.ShouldContain("\"series\"");
        json.ShouldContain("\"plotOptions\"");
        json.ShouldContain("\"legend\"");
        json.ShouldContain("\"tooltip\"");
        json.ShouldContain("\"stacking\"");
    }

    [Fact]
    public void Convert_FullOptions_ContainsAllTopLevelKeys()
    {
        var chart = BuildChart(ChartType.Pie, "Full", [], [("X", [100.0])]);

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        // All top-level properties exist (may be null)
        result.Chart.ShouldNotBeNull();
        result.Title.ShouldNotBeNull();
        result.Series.ShouldNotBeNull();
        result.YAxis.ShouldNotBeNull();
        result.Legend.ShouldNotBeNull();
        result.Tooltip.ShouldNotBeNull();
    }

    #endregion

    #region PlotOptions Per-Type

    [Fact]
    public void Convert_AreaStacked_PlotOptionsHasAreaWithStacking()
    {
        var chart = BuildChart(ChartType.AreaStacked, "Area", ["A"], [("X", [1.0])]);

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.PlotOptions.ShouldNotBeNull();
        result.PlotOptions.Area.ShouldNotBeNull();
        result.PlotOptions.Area.Stacking.ShouldBe("normal");
    }

    [Fact]
    public void Convert_BarFullStacked_PlotOptionsHasBarWithPercentStacking()
    {
        var chart = BuildChart(ChartType.BarFullStacked, "Bar", ["A"], [("X", [1.0])]);

        var converter = new HighchartsConverter();
        var result = converter.Convert(chart);

        result.PlotOptions.ShouldNotBeNull();
        result.PlotOptions.Bar.ShouldNotBeNull();
        result.PlotOptions.Bar.Stacking.ShouldBe("percent");
    }

    #endregion
}
