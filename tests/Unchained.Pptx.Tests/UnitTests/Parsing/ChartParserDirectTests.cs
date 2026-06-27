using System.Globalization;
using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Parsing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

/// <summary>
///     Branch coverage for <see cref="ChartParser" /> driven directly with hand-built
///     <c>&lt;c:chartSpace&gt;</c> trees: every chart-type element + grouping/direction/style
///     combination, axis delete/scaling/gridlines/title branches, data labels, trendlines,
///     series-name literal vs reference, and the literal/cache string &amp; numeric point readers.
/// </summary>
public sealed class ChartParserDirectTests
{
    private static readonly XNamespace C = CmlNames.Cml;
    private static readonly XNamespace A = DmlNames.Dml;

    private static XElement Val(string name, object value) =>
        new(C + name, new XAttribute(DmlNames.AttributeValue, value));

    private static XElement Pt(int idx, string v) =>
        new(CmlNames.Point, new XAttribute(CmlNames.AttributeIndex, idx), new XElement(CmlNames.PointValue, v));

    private static XElement NumLit(params double[] values)
    {
        var lit = new XElement(CmlNames.NumberLiteral);
        for (var i = 0; i < values.Length; i++)
            lit.Add(Pt(i, values[i].ToString(CultureInfo.InvariantCulture)));
        return lit;
    }

    private static XElement StrLit(params string[] values)
    {
        var lit = new XElement(CmlNames.StringLiteral);
        for (var i = 0; i < values.Length; i++)
            lit.Add(Pt(i, values[i]));
        return lit;
    }

    private static XElement SeriesWith(params object[] content) =>
        new(CmlNames.Series, content);

    private static XElement ChartSpace(XElement plotAreaContent, XElement? title = null, XElement? legend = null)
    {
        var plotArea = new XElement(CmlNames.PlotArea, plotAreaContent);
        var chart = new XElement(CmlNames.Chart, title, plotArea, legend);
        return new XElement(CmlNames.ChartSpace, chart);
    }

    private static ChartModel ParseChartSpace(XElement chartSpace)
    {
        var model = new ChartModel();
        ChartParser.Parse(chartSpace, model);
        return model;
    }

    // ── No chart element ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_NoChartElement_LeavesDefaults()
    {
        var root = new XElement(CmlNames.ChartSpace);
        var model = new ChartModel();
        ChartParser.Parse(root, model);
        model.Data.Series.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_NoPlotArea_StillReadsTitleAndLegend()
    {
        var chart = new XElement(
            CmlNames.Chart,
            new XElement(CmlNames.Title),
            new XElement(CmlNames.Legend)
        );
        var model = new ChartModel();
        ChartParser.Parse(new XElement(CmlNames.ChartSpace, chart), model);
        model.HasTitle.ShouldBeTrue();
        model.Legend.IsVisible.ShouldBeTrue();
    }

    // ── Bar / column grouping matrix ──────────────────────────────────────────────

    [
        Theory,
        InlineData("col", "clustered", ChartType.ColumnClustered),
        InlineData("col", "stacked", ChartType.ColumnStacked),
        InlineData("col", "percentStacked", ChartType.ColumnFullStacked),
        InlineData("bar", "clustered", ChartType.BarClustered),
        InlineData("bar", "stacked", ChartType.BarStacked),
        InlineData("bar", "percentStacked", ChartType.BarFullStacked),
        InlineData("col", "weird", ChartType.ColumnClustered)
    ]
    public void Parse_BarChart_MapsDirectionAndGrouping(
        string dir,
        string grouping,
        ChartType expected
    )
    {
        var bar = new XElement(
            CmlNames.BarChart,
            Val("barDir", dir),
            Val("grouping", grouping),
            SeriesWith(new XElement(CmlNames.Values, NumLit(1.0, 2.0)))
        );
        ParseChartSpace(ChartSpace(bar)).Type.ShouldBe(expected);
    }

    [Fact]
    public void Parse_BarChart_MissingDirAndGrouping_DefaultsToColumnClustered()
    {
        var bar = new XElement(CmlNames.BarChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(bar)).Type.ShouldBe(ChartType.ColumnClustered);
    }

    // ── Line grouping / marker matrix ─────────────────────────────────────────────

    [
        Theory,
        InlineData("standard", false, ChartType.Line),
        InlineData("standard", true, ChartType.LineWithMarkers),
        InlineData("stacked", false, ChartType.LineStacked),
        InlineData("stacked", true, ChartType.LineWithMarkersStacked),
        InlineData("percentStacked", false, ChartType.LineFullStacked),
        InlineData("percentStacked", true, ChartType.LineWithMarkersFullStacked)
    ]
    public void Parse_LineChart_MapsGroupingAndMarkers(
        string grouping,
        bool markers,
        ChartType expected
    )
    {
        var series = markers
            ? SeriesWith(new XElement(CmlNames.Marker), new XElement(CmlNames.Values, NumLit(1.0)))
            : SeriesWith(new XElement(CmlNames.Values, NumLit(1.0)));
        var line = new XElement(CmlNames.LineChart, Val("grouping", grouping), series);
        ParseChartSpace(ChartSpace(line)).Type.ShouldBe(expected);
    }

    [Fact]
    public void Parse_LineChart_MissingGrouping_DefaultsToLine()
    {
        var line = new XElement(CmlNames.LineChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(line)).Type.ShouldBe(ChartType.Line);
    }

    // ── Pie / doughnut ────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_PieChart_NoExplosion_IsPie()
    {
        var pie = new XElement(CmlNames.PieChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(pie)).Type.ShouldBe(ChartType.Pie);
    }

    [Fact]
    public void Parse_PieChart_WithExplosion_IsPieExploded()
    {
        var pie = new XElement(
            CmlNames.PieChart,
            SeriesWith(Val("explosion", 25), new XElement(CmlNames.Values, NumLit(1.0)))
        );
        ParseChartSpace(ChartSpace(pie)).Type.ShouldBe(ChartType.PieExploded);
    }

    [Fact]
    public void Parse_DoughnutChart_IsDoughnut()
    {
        var dough = new XElement(CmlNames.DoughnutChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(dough)).Type.ShouldBe(ChartType.Doughnut);
    }

    // ── Area ───────────────────────────────────────────────────────────────────────

    [
        Theory,
        InlineData("standard", ChartType.Area),
        InlineData("stacked", ChartType.AreaStacked),
        InlineData("percentStacked", ChartType.AreaFullStacked)
    ]
    public void Parse_AreaChart_MapsGrouping(string grouping, ChartType expected)
    {
        var area = new XElement(CmlNames.AreaChart, Val("grouping", grouping), SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(area)).Type.ShouldBe(expected);
    }

    [Fact]
    public void Parse_AreaChart_MissingGrouping_DefaultsToArea()
    {
        var area = new XElement(CmlNames.AreaChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(area)).Type.ShouldBe(ChartType.Area);
    }

    // ── Scatter ──────────────────────────────────────────────────────────────────

    [
        Theory,
        InlineData("marker", ChartType.ScatterWithMarkersOnly),
        InlineData("line", ChartType.ScatterWithStraightLines),
        InlineData("lineMarker", ChartType.ScatterWithStraightLinesAndMarkers),
        InlineData("smooth", ChartType.ScatterWithSmoothLines),
        InlineData("smoothMarker", ChartType.ScatterWithSmoothLinesAndMarkers)
    ]
    public void Parse_ScatterChart_MapsStyle(string style, ChartType expected)
    {
        var scatter = new XElement(
            CmlNames.ScatterChart,
            Val("scatterStyle", style),
            SeriesWith(new XElement(CmlNames.XValues, NumLit(1.0)), new XElement(CmlNames.YValues, NumLit(2.0)))
        );
        ParseChartSpace(ChartSpace(scatter)).Type.ShouldBe(expected);
    }

    [Fact]
    public void Parse_ScatterChart_MissingStyle_DefaultsToMarkers()
    {
        var scatter = new XElement(CmlNames.ScatterChart, SeriesWith(new XElement(CmlNames.YValues, NumLit(1.0))));
        ParseChartSpace(ChartSpace(scatter)).Type.ShouldBe(ChartType.ScatterWithMarkersOnly);
    }

    // ── Radar ────────────────────────────────────────────────────────────────────

    [
        Theory,
        InlineData("standard", ChartType.Radar),
        InlineData("marker", ChartType.RadarWithMarkers),
        InlineData("filled", ChartType.RadarFilled)
    ]
    public void Parse_RadarChart_MapsStyle(string style, ChartType expected)
    {
        var radar = new XElement(CmlNames.RadarChart, Val("radarStyle", style), SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(radar)).Type.ShouldBe(expected);
    }

    [Fact]
    public void Parse_RadarChart_MissingStyle_DefaultsToRadar()
    {
        var radar = new XElement(CmlNames.RadarChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(radar)).Type.ShouldBe(ChartType.Radar);
    }

    // ── Bubble & unknown ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_BubbleChart_IsBubble()
    {
        var bubble = new XElement(CmlNames.BubbleChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(bubble)).Type.ShouldBe(ChartType.Bubble);
    }

    [Fact]
    public void Parse_UnknownChartType_LeavesNoSeries()
    {
        var unknown = new XElement(CmlNames.StockChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        var model = ParseChartSpace(ChartSpace(unknown));
        model.Data.Series.Count.ShouldBe(0);
    }

    // ── Title variants ─────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Title_NullTitleElement_SetsHasTitleFalse()
    {
        var bar = new XElement(CmlNames.BarChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(bar)).HasTitle.ShouldBeFalse();
    }

    [Fact]
    public void Parse_Title_RichText_ConcatenatesRuns()
    {
        var title = new XElement(
            CmlNames.Title,
            new XElement(
                CmlNames.Text,
                new XElement(
                    CmlNames.Rich,
                    new XElement(A + "p", new XElement(A + "r", new XElement(A + "t", "Hello ")), new XElement(A + "r", new XElement(A + "t", "World")))
                )
            )
        );
        var bar = new XElement(CmlNames.BarChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        var model = ParseChartSpace(ChartSpace(bar, title));
        model.HasTitle.ShouldBeTrue();
        model.Title.ShouldBe("Hello World");
    }

    [Fact]
    public void Parse_Title_StringCacheReference_ReadsFirstPoint()
    {
        var title = new XElement(
            CmlNames.Title,
            new XElement(
                CmlNames.Text,
                new XElement(
                    CmlNames.StringReference,
                    new XElement(CmlNames.StringCache, Pt(0, "Cached Title"))
                )
            )
        );
        var bar = new XElement(CmlNames.BarChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(bar, title)).Title.ShouldBe("Cached Title");
    }

    [Fact]
    public void Parse_Title_EmptyTextNoRichNoCache_LeavesTitleEmpty()
    {
        var title = new XElement(CmlNames.Title, new XElement(CmlNames.Text));
        var bar = new XElement(CmlNames.BarChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        var model = ParseChartSpace(ChartSpace(bar, title));
        model.HasTitle.ShouldBeTrue();
        model.Title.ShouldBe(string.Empty);
    }

    // ── Legend positions ─────────────────────────────────────────────────────────

    [
        Theory,
        InlineData("t", ChartLegendPosition.Top),
        InlineData("l", ChartLegendPosition.Left),
        InlineData("r", ChartLegendPosition.Right),
        InlineData("tr", ChartLegendPosition.TopRight),
        InlineData("b", ChartLegendPosition.Bottom),
        InlineData("x", ChartLegendPosition.Bottom)
    ]
    public void Parse_Legend_MapsPosition(string pos, ChartLegendPosition expected)
    {
        var legend = new XElement(CmlNames.Legend, Val("legendPos", pos), Val("overlay", 1));
        var bar = new XElement(CmlNames.BarChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        var model = ParseChartSpace(ChartSpace(bar, null, legend));
        model.Legend.IsVisible.ShouldBeTrue();
        model.Legend.Position.ShouldBe(expected);
        model.Legend.IsOverlay.ShouldBeTrue();
    }

    [Fact]
    public void Parse_Legend_MissingPosition_DefaultsBottom()
    {
        var legend = new XElement(CmlNames.Legend);
        var bar = new XElement(CmlNames.BarChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(bar, null, legend)).Legend.Position.ShouldBe(ChartLegendPosition.Bottom);
    }

    [Fact]
    public void Parse_Legend_Absent_IsInvisible()
    {
        var bar = new XElement(CmlNames.BarChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(bar)).Legend.IsVisible.ShouldBeFalse();
    }

    // ── Axes ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Axes_FullProperties_Mapped()
    {
        var catAx = new XElement(
            CmlNames.CategoryAxis,
            Val("delete", 0),
            Val("axPos", "b"),
            new XElement(CmlNames.Scaling, Val("min", 0.0), Val("max", 100.0)),
            Val("majorUnit", 20.0),
            Val("minorUnit", 5.0),
            new XElement(C + "majorGridlines"),
            new XElement(C + "minorGridlines"),
            new XElement(C + "numFmt", new XAttribute("formatCode", "0.0")),
            new XElement(
                CmlNames.Title,
                new XElement(CmlNames.Text, new XElement(CmlNames.Rich, new XElement(A + "p", new XElement(A + "r", new XElement(A + "t", "Axis Title")))))
            )
        );
        var valAx = new XElement(CmlNames.ValueAxis, Val("delete", 1));
        var bar = new XElement(CmlNames.BarChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        var plotArea = new XElement(CmlNames.PlotArea, bar, catAx, valAx);
        var model = ParseChartSpace(new XElement(CmlNames.ChartSpace, new XElement(CmlNames.Chart, plotArea)));

        model.CategoryAxis.IsVisible.ShouldBeTrue();
        model.CategoryAxis.Minimum.ShouldBe(0.0);
        model.CategoryAxis.Maximum.ShouldBe(100.0);
        model.CategoryAxis.MajorUnit.ShouldBe(20.0);
        model.CategoryAxis.MinorUnit.ShouldBe(5.0);
        model.CategoryAxis.HasMajorGridlines.ShouldBeTrue();
        model.CategoryAxis.HasMinorGridlines.ShouldBeTrue();
        model.CategoryAxis.Position.ShouldBe("b");
        model.CategoryAxis.NumberFormat.ShouldBe("0.0");
        model.CategoryAxis.Title.ShouldBe("Axis Title");
        model.ValueAxis.IsVisible.ShouldBeFalse();
    }

    [Fact]
    public void Parse_Axis_NoScalingNoTitle_LeavesDefaults()
    {
        var catAx = new XElement(CmlNames.CategoryAxis);
        var bar = new XElement(CmlNames.BarChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        var plotArea = new XElement(CmlNames.PlotArea, bar, catAx);
        var model = ParseChartSpace(new XElement(CmlNames.ChartSpace, new XElement(CmlNames.Chart, plotArea)));
        model.CategoryAxis.Minimum.ShouldBeNull();
        model.CategoryAxis.Title.ShouldBeNull();
        model.CategoryAxis.IsVisible.ShouldBeTrue();
    }

    [Fact]
    public void Parse_Axis_EmptyTitleText_DoesNotSetTitle()
    {
        var catAx = new XElement(
            CmlNames.CategoryAxis,
            new XElement(
                CmlNames.Title,
                new XElement(CmlNames.Text, new XElement(CmlNames.Rich, new XElement(A + "p", new XElement(A + "r", new XElement(A + "t", "")))))
            )
        );
        var bar = new XElement(CmlNames.BarChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        var plotArea = new XElement(CmlNames.PlotArea, bar, catAx);
        var model = ParseChartSpace(new XElement(CmlNames.ChartSpace, new XElement(CmlNames.Chart, plotArea)));
        model.CategoryAxis.Title.ShouldBeNull();
    }

    // ── Series formatting: fill / data labels / trendline ──────────────────────────

    [Fact]
    public void Parse_Series_WithSolidFill_PopulatesFill()
    {
        var spPr = new XElement(A + "spPr", new XElement(DmlNames.SolidFill, new XElement(A + "srgbClr", new XAttribute(DmlNames.AttributeValue, "FF0000"))));
        var bar = new XElement(CmlNames.BarChart, SeriesWith(spPr, new XElement(CmlNames.Values, NumLit(1.0))));
        var model = ParseChartSpace(ChartSpace(bar));
        model.Data.Series[0].Fill.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_Series_SpPrWithoutSolidFill_LeavesFillNull()
    {
        var spPr = new XElement(A + "spPr", new XElement(DmlNames.NoFill));
        var bar = new XElement(CmlNames.BarChart, SeriesWith(spPr, new XElement(CmlNames.Values, NumLit(1.0))));
        var model = ParseChartSpace(ChartSpace(bar));
        model.Data.Series[0].Fill.ShouldBeNull();
    }

    [Fact]
    public void Parse_Series_DataLabels_AllFlags()
    {
        var dLbls = new XElement(
            C + "dLbls",
            Val("showVal", 1),
            Val("showCatName", 1),
            Val("showSerName", 1),
            Val("showPercent", 1),
            Val("showLegendKey", 1),
            new XElement(C + "dLblPos", new XAttribute(DmlNames.AttributeValue, "outEnd")),
            new XElement(C + "numFmt", new XAttribute("formatCode", "0%"))
        );
        var bar = new XElement(CmlNames.BarChart, SeriesWith(dLbls, new XElement(CmlNames.Values, NumLit(1.0))));
        var labels = ParseChartSpace(ChartSpace(bar)).Data.Series[0].DataLabels;
        labels.ShouldNotBeNull();
        labels.ShowValue.ShouldBeTrue();
        labels.ShowCategoryName.ShouldBeTrue();
        labels.ShowSeriesName.ShouldBeTrue();
        labels.ShowPercentage.ShouldBeTrue();
        labels.ShowLegendKey.ShouldBeTrue();
        labels.Position.ShouldBe("outEnd");
        labels.NumberFormat.ShouldBe("0%");
    }

    [Fact]
    public void Parse_Series_DataLabels_DefaultsWhenFlagsAbsent()
    {
        var dLbls = new XElement(C + "dLbls");
        var bar = new XElement(CmlNames.BarChart, SeriesWith(dLbls, new XElement(CmlNames.Values, NumLit(1.0))));
        var labels = ParseChartSpace(ChartSpace(bar)).Data.Series[0].DataLabels;
        labels.ShouldNotBeNull();
        labels.ShowValue.ShouldBeTrue();
        labels.ShowCategoryName.ShouldBeFalse();
        labels.Position.ShouldBeNull();
    }

    [Fact]
    public void Parse_Series_DataLabels_FlagZero_OverridesDefault()
    {
        var dLbls = new XElement(C + "dLbls", Val("showVal", 0));
        var bar = new XElement(CmlNames.BarChart, SeriesWith(dLbls, new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(bar)).Data.Series[0].DataLabels!.ShowValue.ShouldBeFalse();
    }

    [Fact]
    public void Parse_Series_NoDataLabels_LeavesNull()
    {
        var bar = new XElement(CmlNames.BarChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(bar)).Data.Series[0].DataLabels.ShouldBeNull();
    }

    [Fact]
    public void Parse_Series_Trendline_FullProperties()
    {
        var tl = new XElement(
            C + "trendline",
            Val("trendlineType", "exp"),
            Val("order", 2),
            Val("forward", 1.5),
            Val("backward", 0.5),
            Val("dispEq", 1),
            Val("dispRSqr", 1)
        );
        var bar = new XElement(CmlNames.BarChart, SeriesWith(tl, new XElement(CmlNames.Values, NumLit(1.0))));
        var trend = ParseChartSpace(ChartSpace(bar)).Data.Series[0].Trendline;
        trend.ShouldNotBeNull();
        trend.Type.ShouldBe("exp");
        trend.Order.ShouldBe(2);
        trend.Forward.ShouldBe(1.5);
        trend.Backward.ShouldBe(0.5);
        trend.DisplayEquation.ShouldBeTrue();
        trend.DisplayRSquared.ShouldBeTrue();
    }

    [Fact]
    public void Parse_Series_Trendline_Defaults()
    {
        var tl = new XElement(C + "trendline");
        var bar = new XElement(CmlNames.BarChart, SeriesWith(tl, new XElement(CmlNames.Values, NumLit(1.0))));
        var trend = ParseChartSpace(ChartSpace(bar)).Data.Series[0].Trendline;
        trend.ShouldNotBeNull();
        trend.Type.ShouldBe("linear");
        trend.DisplayEquation.ShouldBeFalse();
    }

    // ── Series name: literal vs reference vs none ──────────────────────────────────

    [Fact]
    public void Parse_SeriesName_StringLiteral()
    {
        var tx = new XElement(CmlNames.Text, StrLit("Revenue"));
        var bar = new XElement(CmlNames.BarChart, SeriesWith(tx, new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(bar)).Data.Series[0].Name.ShouldBe("Revenue");
    }

    [Fact]
    public void Parse_SeriesName_StringReferenceCache()
    {
        var tx = new XElement(
            CmlNames.Text,
            new XElement(CmlNames.StringReference, new XElement(CmlNames.StringCache, Pt(0, "Cached Series")))
        );
        var bar = new XElement(CmlNames.BarChart, SeriesWith(tx, new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(bar)).Data.Series[0].Name.ShouldBe("Cached Series");
    }

    [Fact]
    public void Parse_SeriesName_NoTxElement_IsEmpty()
    {
        var bar = new XElement(CmlNames.BarChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(bar)).Data.Series[0].Name.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_SeriesName_TxWithoutLitOrCache_IsEmpty()
    {
        var tx = new XElement(CmlNames.Text);
        var bar = new XElement(CmlNames.BarChart, SeriesWith(tx, new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(bar)).Data.Series[0].Name.ShouldBe(string.Empty);
    }

    // ── Categories: literal / numeric literal / string cache / numeric cache ───────

    [Fact]
    public void Parse_Categories_StringLiteral()
    {
        var cat = new XElement(CmlNames.Category, StrLit("Jan", "Feb"));
        var bar = new XElement(CmlNames.BarChart, SeriesWith(cat, new XElement(CmlNames.Values, NumLit(1.0, 2.0))));
        ParseChartSpace(ChartSpace(bar)).Data.Categories.ShouldBe(["Jan", "Feb"]);
    }

    [Fact]
    public void Parse_Categories_NumericLiteral_AsStrings()
    {
        var cat = new XElement(CmlNames.Category, NumLit(2021, 2022));
        var bar = new XElement(CmlNames.BarChart, SeriesWith(cat, new XElement(CmlNames.Values, NumLit(1.0, 2.0))));
        ParseChartSpace(ChartSpace(bar)).Data.Categories.Count.ShouldBe(2);
    }

    [Fact]
    public void Parse_Categories_StringCache()
    {
        var cat = new XElement(
            CmlNames.Category,
            new XElement(CmlNames.StringReference, new XElement(CmlNames.StringCache, Pt(0, "A"), Pt(1, "B")))
        );
        var bar = new XElement(CmlNames.BarChart, SeriesWith(cat, new XElement(CmlNames.Values, NumLit(1.0, 2.0))));
        ParseChartSpace(ChartSpace(bar)).Data.Categories.ShouldBe(["A", "B"]);
    }

    [Fact]
    public void Parse_Categories_NumericCache()
    {
        var cat = new XElement(
            CmlNames.Category,
            new XElement(CmlNames.NumberReference, new XElement(CmlNames.NumberCache, Pt(0, "10"), Pt(1, "20")))
        );
        var bar = new XElement(CmlNames.BarChart, SeriesWith(cat, new XElement(CmlNames.Values, NumLit(1.0, 2.0))));
        ParseChartSpace(ChartSpace(bar)).Data.Categories.Count.ShouldBe(2);
    }

    [Fact]
    public void Parse_Categories_Absent_LeavesEmpty()
    {
        var bar = new XElement(CmlNames.BarChart, SeriesWith(new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(bar)).Data.Categories.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_Categories_EmptyContainer_NoPoints()
    {
        var cat = new XElement(CmlNames.Category);
        var bar = new XElement(CmlNames.BarChart, SeriesWith(cat, new XElement(CmlNames.Values, NumLit(1.0))));
        ParseChartSpace(ChartSpace(bar)).Data.Categories.Count.ShouldBe(0);
    }

    // ── Values: literal / numeric cache / invalid number skip ──────────────────────

    [Fact]
    public void Parse_Values_NumberLiteral()
    {
        var bar = new XElement(CmlNames.BarChart, SeriesWith(new XElement(CmlNames.Values, NumLit(3.5, 4.5))));
        ParseChartSpace(ChartSpace(bar)).Data.Series[0].Values.ShouldBe([3.5, 4.5]);
    }

    [Fact]
    public void Parse_Values_NumberCache()
    {
        var val = new XElement(
            CmlNames.Values,
            new XElement(CmlNames.NumberReference, new XElement(CmlNames.NumberCache, Pt(0, "7"), Pt(1, "8")))
        );
        var bar = new XElement(CmlNames.BarChart, SeriesWith(val));
        ParseChartSpace(ChartSpace(bar)).Data.Series[0].Values.ShouldBe([7.0, 8.0]);
    }

    [Fact]
    public void Parse_Values_NumberLiteral_SkipsNonNumeric()
    {
        var val = new XElement(CmlNames.Values, new XElement(CmlNames.NumberLiteral, Pt(0, "notanumber"), Pt(1, "9")));
        var bar = new XElement(CmlNames.BarChart, SeriesWith(val));
        ParseChartSpace(ChartSpace(bar)).Data.Series[0].Values.ShouldBe([9.0]);
    }

    [Fact]
    public void Parse_Values_NumberCache_SkipsNonNumeric()
    {
        var val = new XElement(
            CmlNames.Values,
            new XElement(CmlNames.NumberReference, new XElement(CmlNames.NumberCache, Pt(0, "bad"), Pt(1, "11")))
        );
        var bar = new XElement(CmlNames.BarChart, SeriesWith(val));
        ParseChartSpace(ChartSpace(bar)).Data.Series[0].Values.ShouldBe([11.0]);
    }

    [Fact]
    public void Parse_Values_Absent_LeavesEmpty()
    {
        var bar = new XElement(CmlNames.BarChart, SeriesWith(new XElement(CmlNames.Category, StrLit("X"))));
        ParseChartSpace(ChartSpace(bar)).Data.Series[0].Values.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_YValues_UsedWhenNoVal()
    {
        var bar = new XElement(CmlNames.BarChart, SeriesWith(new XElement(CmlNames.YValues, NumLit(2.0, 3.0))));
        ParseChartSpace(ChartSpace(bar)).Data.Series[0].Values.ShouldBe([2.0, 3.0]);
    }

    // ── Multi-series: categories read from first series only ───────────────────────

    [Fact]
    public void Parse_MultipleSeries_CategoriesFromFirstOnly()
    {
        var s1 = SeriesWith(new XElement(CmlNames.Category, StrLit("A", "B")), new XElement(CmlNames.Values, NumLit(1.0, 2.0)));
        var s2 = SeriesWith(new XElement(CmlNames.Category, StrLit("C", "D")), new XElement(CmlNames.Values, NumLit(3.0, 4.0)));
        var bar = new XElement(CmlNames.BarChart, s1, s2);
        var model = ParseChartSpace(ChartSpace(bar));
        model.Data.Series.Count.ShouldBe(2);
        model.Data.Categories.ShouldBe(["A", "B"]);
    }

    [Fact]
    public void Parse_XValues_PopulatesSeriesXValues()
    {
        var scatter = new XElement(
            CmlNames.ScatterChart,
            Val("scatterStyle", "lineMarker"),
            SeriesWith(new XElement(CmlNames.XValues, NumLit(1.0, 2.0)), new XElement(CmlNames.YValues, NumLit(5.0, 6.0)))
        );
        var model = ParseChartSpace(ChartSpace(scatter));
        model.Data.Series[0].XValues.ShouldBe([1.0, 2.0]);
        model.Data.Series[0].Values.ShouldBe([5.0, 6.0]);
    }
}
