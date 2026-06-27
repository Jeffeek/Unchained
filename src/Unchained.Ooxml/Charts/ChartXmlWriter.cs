using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;

namespace Unchained.Ooxml.Charts;

/// <summary>
///     Serializes a <see cref="ChartModel" /> to a <c>c:chartSpace</c> XML tree. The chart part XML
///     is identical across formats (PPTX, XLSX), so this is the single shared implementation. Data is
///     written as inline literals (<c>c:numLit</c> / <c>c:strLit</c>) so no embedded workbook is
///     required. A caller-supplied hook (passed to <see cref="Write" />)
///     injects format-specific per-series fill (<c>c:spPr</c>) serialization; pass <see langword="null" />
///     to omit per-series fills.
/// </summary>
public static class ChartXmlWriter
{
    // Stable axis IDs — the same values office applications use for new charts.
    private const long CategoryAxisId = 2_094_986_368;
    private const long ValueAxisId = 2_094_991_872;

    /// <summary>Writes <paramref name="model" /> and returns the UTF-8 chart XML bytes.</summary>
    /// <param name="model">The chart to serialize.</param>
    /// <param name="fillWriter">
    ///     Optional hook that appends a fill to a <c>c:spPr</c> element for a series; when null,
    ///     per-series fills are not written.
    /// </param>
    public static byte[] Write(ChartModel model, Action<XElement, FillFormat>? fillWriter = null) =>
        new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), WriteChartSpace(model, fillWriter)).ToUtf8Bytes();

    /// <summary>Builds the <c>c:chartSpace</c> element for <paramref name="model" />.</summary>
    public static XElement WriteChartSpace(ChartModel model, Action<XElement, FillFormat>? fillWriter = null)
    {
        var c = CmlNames.Cml;
        var a = DmlNames.Dml;
        var r = XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships");

        var chartSpace = new XElement(
            CmlNames.ChartSpace,
            new XAttribute(XNamespace.Xmlns + "c", c.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "a", a.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "r", r.NamespaceName)
        );

        chartSpace.Add(new XElement(CmlNames.Language, new XAttribute(DmlNames.AttributeValue, "en-US")));

        var chartEl = new XElement(CmlNames.Chart);

        if (model.HasTitle)
            chartEl.Add(WriteTitle(model.Title, a));

        chartEl.Add(new XElement(CmlNames.AutoTitleDeleted, new XAttribute(DmlNames.AttributeValue, model.HasTitle ? "0" : "1")));
        chartEl.Add(WritePlotArea(model, fillWriter));

        if (model.Legend.IsVisible)
            chartEl.Add(WriteLegend(model.Legend));

        chartEl.Add(new XElement(CmlNames.PlotVisibleOnly, new XAttribute(DmlNames.AttributeValue, "1")));
        chartSpace.Add(chartEl);
        return chartSpace;
    }

    // ── Title ─────────────────────────────────────────────────────────────────

    private static XElement WriteTitle(string titleText, XNamespace a)
    {
        var rich = new XElement(
            CmlNames.Rich,
            new XElement(a + "bodyPr"),
            new XElement(a + "lstStyle"),
            new XElement(
                a + "p",
                new XElement(
                    a + "r",
                    new XElement(a + "rPr", new XAttribute("lang", "en-US"), new XAttribute("dirty", "0")),
                    new XElement(a + "t", titleText)
                )
            )
        );

        return new XElement(
            CmlNames.Title,
            new XElement(CmlNames.Text, rich),
            new XElement(
                CmlNames.Overlay,
                new XAttribute(DmlNames.AttributeValue, "0")
            )
        );
    }

    // ── Plot area ─────────────────────────────────────────────────────────────

    private static XElement WritePlotArea(ChartModel model, Action<XElement, FillFormat>? fillWriter)
    {
        var plotArea = new XElement(CmlNames.PlotArea);
        plotArea.Add(new XElement(CmlNames.Layout));

        var (chartTypeEl, needsCatValAxes) = WriteChartTypeElement(model, fillWriter);
        plotArea.Add(chartTypeEl);

        if (!needsCatValAxes)
            return plotArea;

        plotArea.Add(WriteCategoryAxis(model.Type, model.CategoryAxis));
        plotArea.Add(WriteValueAxis(model.Type, model.ValueAxis));
        return plotArea;
    }

    private static (XElement element, bool needsAxes) WriteChartTypeElement(ChartModel model, Action<XElement, FillFormat>? fw) =>
        model.Type switch
        {
            ChartType.ColumnClustered or ChartType.ColumnStacked or ChartType.ColumnFullStacked
                or ChartType.BarClustered or ChartType.BarStacked or ChartType.BarFullStacked
                => (WriteBarChart(model, fw), true),
            ChartType.Line or ChartType.LineStacked or ChartType.LineFullStacked
                or ChartType.LineWithMarkers or ChartType.LineWithMarkersStacked or ChartType.LineWithMarkersFullStacked
                => (WriteLineChart(model, fw), true),
            ChartType.Pie or ChartType.PieExploded => (WritePieChart(model, fw), false),
            ChartType.Doughnut or ChartType.DoughnutExploded => (WriteDoughnutChart(model, fw), false),
            ChartType.Area or ChartType.AreaStacked or ChartType.AreaFullStacked => (WriteAreaChart(model, fw), true),
            ChartType.ScatterWithMarkersOnly or ChartType.ScatterWithStraightLines or ChartType.ScatterWithSmoothLines
                or ChartType.ScatterWithStraightLinesAndMarkers or ChartType.ScatterWithSmoothLinesAndMarkers
                => (WriteScatterChart(model), true),
            ChartType.Radar or ChartType.RadarWithMarkers or ChartType.RadarFilled => (WriteRadarChart(model, fw), true),
            ChartType.Bubble => (WriteBubbleChart(model), true),
            _ => (WriteBarChart(model, fw), true)
        };

    private static XElement WriteBarChart(ChartModel model, Action<XElement, FillFormat>? fw)
    {
        var el = new XElement(CmlNames.BarChart);
        var (dir, grouping) = model.Type switch
        {
            ChartType.BarClustered => ("bar", "clustered"),
            ChartType.BarStacked => ("bar", "stacked"),
            ChartType.BarFullStacked => ("bar", "percentStacked"),
            ChartType.ColumnStacked => ("col", "stacked"),
            ChartType.ColumnFullStacked => ("col", "percentStacked"),
            _ => ("col", "clustered")
        };

        el.Add(new XElement(CmlNames.BarDirection, new XAttribute(DmlNames.AttributeValue, dir)));
        el.Add(new XElement(CmlNames.Grouping, new XAttribute(DmlNames.AttributeValue, grouping)));
        el.Add(new XElement(CmlNames.VaryColors, new XAttribute(DmlNames.AttributeValue, "0")));
        for (var i = 0; i < model.Data.Series.Count; i++)
            el.Add(WriteStandardSeries(model.Data.Series[i], i, model.Data.Categories, false, fw));
        AddAxisIds(el);
        return el;
    }

    private static XElement WriteLineChart(ChartModel model, Action<XElement, FillFormat>? fw)
    {
        var el = new XElement(CmlNames.LineChart);
        var grouping = model.Type switch
        {
            ChartType.LineStacked or ChartType.LineWithMarkersStacked => "stacked",
            ChartType.LineFullStacked or ChartType.LineWithMarkersFullStacked => "percentStacked",
            _ => "standard"
        };
        el.Add(new XElement(CmlNames.Grouping, new XAttribute(DmlNames.AttributeValue, grouping)));
        el.Add(new XElement(CmlNames.VaryColors, new XAttribute(DmlNames.AttributeValue, "0")));

        var includeMarker = model.Type is ChartType.LineWithMarkers or ChartType.LineWithMarkersStacked
            or ChartType.LineWithMarkersFullStacked;
        for (var i = 0; i < model.Data.Series.Count; i++)
            el.Add(WriteStandardSeries(model.Data.Series[i], i, model.Data.Categories, includeMarker, fw));
        AddAxisIds(el);
        return el;
    }

    private static XElement WritePieChart(ChartModel model, Action<XElement, FillFormat>? fw)
    {
        var el = new XElement(CmlNames.PieChart);
        el.Add(new XElement(CmlNames.VaryColors, new XAttribute(DmlNames.AttributeValue, "1")));
        var series = model.Data.Series.FirstOrDefault() ?? new ChartSeries();
        el.Add(WriteStandardSeries(series, 0, model.Data.Categories, false, fw));
        return el;
    }

    private static XElement WriteDoughnutChart(ChartModel model, Action<XElement, FillFormat>? fw)
    {
        var el = new XElement(CmlNames.DoughnutChart);
        el.Add(new XElement(CmlNames.VaryColors, new XAttribute(DmlNames.AttributeValue, "1")));
        var series = model.Data.Series.FirstOrDefault() ?? new ChartSeries();
        el.Add(WriteStandardSeries(series, 0, model.Data.Categories, false, fw));
        el.Add(new XElement(CmlNames.HoleSize, new XAttribute(DmlNames.AttributeValue, "50")));
        return el;
    }

    private static XElement WriteAreaChart(ChartModel model, Action<XElement, FillFormat>? fw)
    {
        var el = new XElement(CmlNames.AreaChart);
        var grouping = model.Type switch
        {
            ChartType.AreaStacked => "stacked",
            ChartType.AreaFullStacked => "percentStacked",
            _ => "standard"
        };
        el.Add(new XElement(CmlNames.Grouping, new XAttribute(DmlNames.AttributeValue, grouping)));
        el.Add(new XElement(CmlNames.VaryColors, new XAttribute(DmlNames.AttributeValue, "0")));
        for (var i = 0; i < model.Data.Series.Count; i++)
            el.Add(WriteStandardSeries(model.Data.Series[i], i, model.Data.Categories, false, fw));
        AddAxisIds(el);
        return el;
    }

    private static XElement WriteScatterChart(ChartModel model)
    {
        var el = new XElement(CmlNames.ScatterChart);
        var style = model.Type switch
        {
            ChartType.ScatterWithStraightLines => "line",
            ChartType.ScatterWithStraightLinesAndMarkers => "lineMarker",
            ChartType.ScatterWithSmoothLines => "smooth",
            ChartType.ScatterWithSmoothLinesAndMarkers => "smoothMarker",
            _ => "marker"
        };
        el.Add(new XElement(CmlNames.ScatterStyle, new XAttribute(DmlNames.AttributeValue, style)));
        el.Add(new XElement(CmlNames.VaryColors, new XAttribute(DmlNames.AttributeValue, "0")));
        for (var i = 0; i < model.Data.Series.Count; i++)
            el.Add(WriteScatterSeries(model.Data.Series[i], i));
        AddAxisIds(el);
        return el;
    }

    private static XElement WriteRadarChart(ChartModel model, Action<XElement, FillFormat>? fw)
    {
        var el = new XElement(CmlNames.RadarChart);
        var style = model.Type switch
        {
            ChartType.RadarWithMarkers => "marker",
            ChartType.RadarFilled => "filled",
            _ => "standard"
        };
        el.Add(new XElement(CmlNames.RadarStyle, new XAttribute(DmlNames.AttributeValue, style)));
        el.Add(new XElement(CmlNames.VaryColors, new XAttribute(DmlNames.AttributeValue, "0")));
        for (var i = 0; i < model.Data.Series.Count; i++)
            el.Add(WriteStandardSeries(model.Data.Series[i], i, model.Data.Categories, false, fw));
        AddAxisIds(el);
        return el;
    }

    private static XElement WriteBubbleChart(ChartModel model)
    {
        var el = new XElement(CmlNames.BubbleChart);
        el.Add(new XElement(CmlNames.VaryColors, new XAttribute(DmlNames.AttributeValue, "0")));
        for (var i = 0; i < model.Data.Series.Count; i++)
            el.Add(WriteScatterSeries(model.Data.Series[i], i));
        AddAxisIds(el);
        return el;
    }

    private static void AddAxisIds(XContainer parent)
    {
        parent.Add(new XElement(CmlNames.AxisId, new XAttribute(DmlNames.AttributeValue, CategoryAxisId)));
        parent.Add(new XElement(CmlNames.AxisId, new XAttribute(DmlNames.AttributeValue, ValueAxisId)));
    }

    // ── Series ────────────────────────────────────────────────────────────────

    private static XElement WriteStandardSeries(
        ChartSeries series,
        int index,
        IReadOnlyList<string> categories,
        bool includeMarker,
        Action<XElement, FillFormat>? fillWriter
    )
    {
        var ser = new XElement(CmlNames.Series);
        ser.Add(new XElement(CmlNames.Index, new XAttribute(DmlNames.AttributeValue, index)));
        ser.Add(new XElement(CmlNames.Order, new XAttribute(DmlNames.AttributeValue, index)));
        ser.Add(WriteSeriesName(series.Name));

        if (series.Fill is { } fill && fillWriter != null)
        {
            var spPr = new XElement(DmlNames.Dml + "spPr");
            fillWriter(spPr, fill);
            ser.Add(spPr);
        }

        if (includeMarker)
            ser.Add(new XElement(CmlNames.Marker, new XElement(CmlNames.MarkerSymbol, new XAttribute(DmlNames.AttributeValue, "circle"))));

        if (series.DataLabels is { } dl)
            ser.Add(WriteDataLabels(dl));
        if (series.Trendline is { } tl)
            ser.Add(WriteTrendline(tl));

        if (categories.Count > 0)
            ser.Add(new XElement(CmlNames.Category, WriteStringLiteral(categories)));

        ser.Add(new XElement(CmlNames.Values, WriteNumberLiteral(series.Values)));
        return ser;
    }

    private static XElement WriteScatterSeries(ChartSeries series, int index)
    {
        var ser = new XElement(CmlNames.Series);
        ser.Add(new XElement(CmlNames.Index, new XAttribute(DmlNames.AttributeValue, index)));
        ser.Add(new XElement(CmlNames.Order, new XAttribute(DmlNames.AttributeValue, index)));
        ser.Add(WriteSeriesName(series.Name));

        var xValues = series.XValues.Count > 0
            ? series.XValues
            : Enumerable.Range(1, series.Values.Count).Select(static n => (double)n).ToList();
        ser.Add(new XElement(CmlNames.XValues, WriteNumberLiteral(xValues)));
        ser.Add(new XElement(CmlNames.YValues, WriteNumberLiteral(series.Values)));
        return ser;
    }

    private static XElement WriteDataLabels(ChartDataLabels labels)
    {
        var c = CmlNames.Cml;
        var dLbls = new XElement(c + "dLbls");
        if (!string.IsNullOrEmpty(labels.NumberFormat))
            dLbls.Add(new XElement(c + "numFmt", new XAttribute("formatCode", labels.NumberFormat), new XAttribute("sourceLinked", "0")));

        if (!string.IsNullOrEmpty(labels.Position))
            dLbls.Add(new XElement(c + "dLblPos", new XAttribute(DmlNames.AttributeValue, labels.Position)));
        dLbls.Add(new XElement(c + "showLegendKey", new XAttribute(DmlNames.AttributeValue, labels.ShowLegendKey ? "1" : "0")));
        dLbls.Add(new XElement(c + "showVal", new XAttribute(DmlNames.AttributeValue, labels.ShowValue ? "1" : "0")));
        dLbls.Add(new XElement(c + "showCatName", new XAttribute(DmlNames.AttributeValue, labels.ShowCategoryName ? "1" : "0")));
        dLbls.Add(new XElement(c + "showSerName", new XAttribute(DmlNames.AttributeValue, labels.ShowSeriesName ? "1" : "0")));
        dLbls.Add(new XElement(c + "showPercent", new XAttribute(DmlNames.AttributeValue, labels.ShowPercentage ? "1" : "0")));
        return dLbls;
    }

    private static XElement WriteTrendline(ChartTrendline trend)
    {
        var c = CmlNames.Cml;
        var tl = new XElement(c + "trendline");
        tl.Add(new XElement(c + "trendlineType", new XAttribute(DmlNames.AttributeValue, trend.Type)));
        if (trend.Order is { } order)
            tl.Add(new XElement(c + "order", new XAttribute(DmlNames.AttributeValue, order)));
        if (trend.Forward is { } fwd)
            tl.Add(new XElement(c + "forward", new XAttribute(DmlNames.AttributeValue, Num(fwd))));
        if (trend.Backward is { } bwd)
            tl.Add(new XElement(c + "backward", new XAttribute(DmlNames.AttributeValue, Num(bwd))));
        tl.Add(new XElement(c + "dispRSqr", new XAttribute(DmlNames.AttributeValue, trend.DisplayRSquared ? "1" : "0")));
        tl.Add(new XElement(c + "dispEq", new XAttribute(DmlNames.AttributeValue, trend.DisplayEquation ? "1" : "0")));
        return tl;
    }

    // A series-name <c:tx> (CT_SerTx) accepts <c:strRef> or a plain <c:v>, NOT <c:strLit>.
    private static XElement WriteSeriesName(string name) =>
        new(CmlNames.Text, new XElement(CmlNames.PointValue, name));

    private static XElement WriteStringLiteral(IReadOnlyList<string> values)
    {
        var strLit = new XElement(CmlNames.StringLiteral);
        strLit.Add(new XElement(CmlNames.PointCount, new XAttribute(DmlNames.AttributeValue, values.Count)));
        for (var i = 0; i < values.Count; i++)
        {
            strLit.Add(
                new XElement(
                    CmlNames.Point,
                    new XAttribute(CmlNames.AttributeIndex, i),
                    new XElement(CmlNames.PointValue, values[i])
                )
            );
        }

        return strLit;
    }

    private static XElement WriteNumberLiteral(IReadOnlyList<double> values)
    {
        var numLit = new XElement(CmlNames.NumberLiteral);
        numLit.Add(new XElement(CmlNames.FormatCode, "General"));
        numLit.Add(new XElement(CmlNames.PointCount, new XAttribute(DmlNames.AttributeValue, values.Count)));
        for (var i = 0; i < values.Count; i++)
        {
            numLit.Add(
                new XElement(
                    CmlNames.Point,
                    new XAttribute(CmlNames.AttributeIndex, i),
                    new XElement(CmlNames.PointValue, values[i].ToString("G", CultureInfo.InvariantCulture))
                )
            );
        }

        return numLit;
    }

    // ── Axes ──────────────────────────────────────────────────────────────────

    private static XElement WriteCategoryAxis(ChartType type, ChartAxis axis)
    {
        var catPos = type is ChartType.BarClustered or ChartType.BarStacked or ChartType.BarFullStacked ? "l" : "b";
        var ax = new XElement(CmlNames.CategoryAxis);
        ax.Add(new XElement(CmlNames.AxisId, new XAttribute(DmlNames.AttributeValue, CategoryAxisId)));
        ax.Add(WriteScaling(axis));
        ax.Add(new XElement(CmlNames.Delete, new XAttribute(DmlNames.AttributeValue, axis.IsVisible ? "0" : "1")));
        ax.Add(new XElement(CmlNames.AxisPosition, new XAttribute(DmlNames.AttributeValue, axis.Position ?? catPos)));
        if (axis.HasMajorGridlines) ax.Add(new XElement(CmlNames.Cml + "majorGridlines"));
        if (axis.HasMinorGridlines) ax.Add(new XElement(CmlNames.Cml + "minorGridlines"));
        if (!string.IsNullOrEmpty(axis.Title)) ax.Add(WriteAxisTitle(axis.Title));
        if (!string.IsNullOrEmpty(axis.NumberFormat))
            ax.Add(new XElement(CmlNames.Cml + "numFmt", new XAttribute("formatCode", axis.NumberFormat), new XAttribute("sourceLinked", "0")));
        ax.Add(new XElement(CmlNames.CrossAxis, new XAttribute(DmlNames.AttributeValue, ValueAxisId)));
        return ax;
    }

    private static XElement WriteValueAxis(ChartType type, ChartAxis axis)
    {
        var valPos = type is ChartType.BarClustered or ChartType.BarStacked or ChartType.BarFullStacked ? "b" : "l";
        var ax = new XElement(CmlNames.ValueAxis);
        ax.Add(new XElement(CmlNames.AxisId, new XAttribute(DmlNames.AttributeValue, ValueAxisId)));
        ax.Add(WriteScaling(axis));
        ax.Add(new XElement(CmlNames.Delete, new XAttribute(DmlNames.AttributeValue, axis.IsVisible ? "0" : "1")));
        ax.Add(new XElement(CmlNames.AxisPosition, new XAttribute(DmlNames.AttributeValue, axis.Position ?? valPos)));
        if (axis.HasMajorGridlines) ax.Add(new XElement(CmlNames.Cml + "majorGridlines"));
        if (axis.HasMinorGridlines) ax.Add(new XElement(CmlNames.Cml + "minorGridlines"));
        if (!string.IsNullOrEmpty(axis.Title)) ax.Add(WriteAxisTitle(axis.Title));
        if (!string.IsNullOrEmpty(axis.NumberFormat))
            ax.Add(new XElement(CmlNames.Cml + "numFmt", new XAttribute("formatCode", axis.NumberFormat), new XAttribute("sourceLinked", "0")));
        ax.Add(new XElement(CmlNames.CrossAxis, new XAttribute(DmlNames.AttributeValue, CategoryAxisId)));
        if (axis.MajorUnit is { } mu)
            ax.Add(new XElement(CmlNames.Cml + "majorUnit", new XAttribute(DmlNames.AttributeValue, Num(mu))));
        if (axis.MinorUnit is { } nu)
            ax.Add(new XElement(CmlNames.Cml + "minorUnit", new XAttribute(DmlNames.AttributeValue, Num(nu))));
        return ax;
    }

    private static XElement WriteScaling(ChartAxis axis)
    {
        var scaling = new XElement(
            CmlNames.Scaling,
            new XElement(
                CmlNames.Orientation,
                new XAttribute(DmlNames.AttributeValue, "minMax")
            )
        );
        if (axis.Maximum is { } max)
            scaling.Add(new XElement(CmlNames.Cml + "max", new XAttribute(DmlNames.AttributeValue, Num(max))));
        if (axis.Minimum is { } min)
            scaling.Add(new XElement(CmlNames.Cml + "min", new XAttribute(DmlNames.AttributeValue, Num(min))));
        return scaling;
    }

    private static XElement WriteAxisTitle(string title)
    {
        var a = DmlNames.Dml;
        return new XElement(
            CmlNames.Title,
            new XElement(
                CmlNames.Text,
                new XElement(
                    CmlNames.Rich,
                    new XElement(a + "bodyPr"),
                    new XElement(a + "lstStyle"),
                    new XElement(a + "p", new XElement(a + "r", new XElement(a + "t", title)))
                )
            ),
            new XElement(
                CmlNames.Cml + "overlay",
                new XAttribute(DmlNames.AttributeValue, "0")
            )
        );
    }

    private static XElement WriteLegend(ChartLegend legend)
    {
        var posVal = legend.Position switch
        {
            ChartLegendPosition.Top => "t",
            ChartLegendPosition.Left => "l",
            ChartLegendPosition.Right => "r",
            ChartLegendPosition.TopRight => "tr",
            _ => "b"
        };
        return new XElement(
            CmlNames.Legend,
            new XElement(CmlNames.LegendPosition, new XAttribute(DmlNames.AttributeValue, posVal)),
            new XElement(
                CmlNames.Overlay,
                new XAttribute(DmlNames.AttributeValue, legend.IsOverlay ? "1" : "0")
            )
        );
    }

    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);
}
