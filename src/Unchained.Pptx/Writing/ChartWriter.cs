using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Core.Xml;

namespace Unchained.Pptx.Writing;

/// <summary>
///     Serializes a <see cref="ChartModel" /> to the bytes of a <c>chart.xml</c> OPC part.
///     All data is written as inline literals (<c>c:numLit</c> / <c>c:strLit</c>) so no
///     embedded workbook is required.
/// </summary>
internal static class ChartWriter
{
    // Stable axis IDs — same values PowerPoint uses for new charts.
    private const long CategoryAxisId = 2_094_986_368;
    private const long ValueAxisId = 2_094_991_872;

    /// <summary>
    ///     Writes <paramref name="model" /> and returns the UTF-8 encoded chart XML bytes.
    /// </summary>
    public static byte[] Write(ChartModel model)
    {
        var c = CmlNames.Cml;
        var a = DmlNames.Dml;
        var r = XNamespace.Get(
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

        var chartSpace = new XElement(CmlNames.ChartSpace,
            new XAttribute(XNamespace.Xmlns + "c", c.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "a", a.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "r", r.NamespaceName));

        chartSpace.Add(new XElement(CmlNames.Language,
            new XAttribute(CmlNames.AttributeValue, "en-US")));

        var chartEl = new XElement(CmlNames.Chart);

        if (model.HasTitle)
            chartEl.Add(WriteTitle(model.Title, a));

        chartEl.Add(new XElement(CmlNames.AutoTitleDeleted,
            new XAttribute(CmlNames.AttributeValue, model.HasTitle ? "0" : "1")));

        chartEl.Add(WritePlotArea(model));

        if (model.Legend.IsVisible)
            chartEl.Add(WriteLegend(model.Legend));

        chartEl.Add(new XElement(CmlNames.PlotVisibleOnly,
            new XAttribute(CmlNames.AttributeValue, "1")));

        chartSpace.Add(chartEl);

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), chartSpace);
        return doc.ToUtf8Bytes();
    }

    // ── Title ─────────────────────────────────────────────────────────────────

    private static XElement WriteTitle(string titleText, XNamespace a)
    {
        var title = new XElement(CmlNames.Title);

        var rich = new XElement(CmlNames.Rich);
        rich.Add(new XElement(a + "bodyPr"));
        rich.Add(new XElement(a + "lstStyle"));
        var para = new XElement(a + "p");
        var run = new XElement(a + "r");
        run.Add(new XElement(a + "rPr", new XAttribute("lang", "en-US"), new XAttribute("dirty", "0")));
        run.Add(new XElement(a + "t", titleText));
        para.Add(run);
        rich.Add(para);

        var tx = new XElement(CmlNames.Text);
        tx.Add(rich);
        title.Add(tx);
        title.Add(new XElement(CmlNames.Overlay, new XAttribute(CmlNames.AttributeValue, "0")));
        return title;
    }

    // ── Plot area ─────────────────────────────────────────────────────────────

    private static XElement WritePlotArea(ChartModel model)
    {
        var plotArea = new XElement(CmlNames.PlotArea);
        plotArea.Add(new XElement(CmlNames.Layout));

        var (chartTypeEl, needsCatValAxes) = WriteChartTypeElement(model);
        plotArea.Add(chartTypeEl);

        if (!needsCatValAxes) return plotArea;

        plotArea.Add(WriteCategoryAxis(model.Type, model.CategoryAxis));
        plotArea.Add(WriteValueAxis(model.Type, model.ValueAxis));

        return plotArea;
    }

    private static (XElement element, bool needsAxes) WriteChartTypeElement(ChartModel model) =>
        model.Type switch
        {
            ChartType.ColumnClustered or ChartType.ColumnStacked or ChartType.ColumnFullStacked
                or ChartType.BarClustered or ChartType.BarStacked or ChartType.BarFullStacked
                => (WriteBarChart(model), true),

            ChartType.Line or ChartType.LineStacked or ChartType.LineFullStacked
                or ChartType.LineWithMarkers or ChartType.LineWithMarkersStacked or ChartType.LineWithMarkersFullStacked
                => (WriteLineChart(model), true),

            ChartType.Pie or ChartType.PieExploded
                => (WritePieChart(model), false),

            ChartType.Doughnut or ChartType.DoughnutExploded
                => (WriteDoughnutChart(model), false),

            ChartType.Area or ChartType.AreaStacked or ChartType.AreaFullStacked
                => (WriteAreaChart(model), true),

            ChartType.ScatterWithMarkersOnly or ChartType.ScatterWithStraightLines
                or ChartType.ScatterWithSmoothLines or ChartType.ScatterWithStraightLinesAndMarkers
                or ChartType.ScatterWithSmoothLinesAndMarkers
                => (WriteScatterChart(model), true),

            ChartType.Radar or ChartType.RadarWithMarkers or ChartType.RadarFilled
                => (WriteRadarChart(model), true),

            ChartType.Bubble => (WriteBubbleChart(model), true),

            _ => (WriteBarChart(model), true)
        };

    // ── Bar / Column ──────────────────────────────────────────────────────────

    private static XElement WriteBarChart(ChartModel model)
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

        el.Add(new XElement(CmlNames.BarDirection, new XAttribute(CmlNames.AttributeValue, dir)));
        el.Add(new XElement(CmlNames.Grouping, new XAttribute(CmlNames.AttributeValue, grouping)));
        el.Add(new XElement(CmlNames.VaryColors, new XAttribute(CmlNames.AttributeValue, "0")));

        for (var i = 0; i < model.Data.Series.Count; i++)
            el.Add(WriteStandardSeries(model.Data.Series[i], i, model.Data.Categories, false));

        el.Add(new XElement(CmlNames.AxisId, new XAttribute(CmlNames.AttributeValue, CategoryAxisId)));
        el.Add(new XElement(CmlNames.AxisId, new XAttribute(CmlNames.AttributeValue, ValueAxisId)));
        return el;
    }

    // ── Line ──────────────────────────────────────────────────────────────────

    private static XElement WriteLineChart(ChartModel model)
    {
        var el = new XElement(CmlNames.LineChart);

        var grouping = model.Type switch
        {
            ChartType.LineStacked or ChartType.LineWithMarkersStacked => "stacked",
            ChartType.LineFullStacked or ChartType.LineWithMarkersFullStacked => "percentStacked",
            _ => "standard"
        };
        el.Add(new XElement(CmlNames.Grouping, new XAttribute(CmlNames.AttributeValue, grouping)));
        el.Add(new XElement(CmlNames.VaryColors, new XAttribute(CmlNames.AttributeValue, "0")));

        var includeMarker = model.Type is ChartType.LineWithMarkers or ChartType.LineWithMarkersStacked
            or ChartType.LineWithMarkersFullStacked;

        for (var i = 0; i < model.Data.Series.Count; i++)
            el.Add(WriteStandardSeries(model.Data.Series[i], i, model.Data.Categories, includeMarker));

        el.Add(new XElement(CmlNames.AxisId, new XAttribute(CmlNames.AttributeValue, CategoryAxisId)));
        el.Add(new XElement(CmlNames.AxisId, new XAttribute(CmlNames.AttributeValue, ValueAxisId)));
        return el;
    }

    // ── Pie ───────────────────────────────────────────────────────────────────

    private static XElement WritePieChart(ChartModel model)
    {
        var el = new XElement(CmlNames.PieChart);
        el.Add(new XElement(CmlNames.VaryColors, new XAttribute(CmlNames.AttributeValue, "1")));

        var series = model.Data.Series.FirstOrDefault() ?? new ChartSeries();
        el.Add(WriteStandardSeries(series, 0, model.Data.Categories, false));
        return el;
    }

    // ── Doughnut ──────────────────────────────────────────────────────────────

    private static XElement WriteDoughnutChart(ChartModel model)
    {
        var el = new XElement(CmlNames.DoughnutChart);
        el.Add(new XElement(CmlNames.VaryColors, new XAttribute(CmlNames.AttributeValue, "1")));

        var series = model.Data.Series.FirstOrDefault() ?? new ChartSeries();
        el.Add(WriteStandardSeries(series, 0, model.Data.Categories, false));
        el.Add(new XElement(CmlNames.HoleSize, new XAttribute(CmlNames.AttributeValue, "50")));
        return el;
    }

    // ── Area ──────────────────────────────────────────────────────────────────

    private static XElement WriteAreaChart(ChartModel model)
    {
        var el = new XElement(CmlNames.AreaChart);

        var grouping = model.Type switch
        {
            ChartType.AreaStacked => "stacked",
            ChartType.AreaFullStacked => "percentStacked",
            _ => "standard"
        };
        el.Add(new XElement(CmlNames.Grouping, new XAttribute(CmlNames.AttributeValue, grouping)));
        el.Add(new XElement(CmlNames.VaryColors, new XAttribute(CmlNames.AttributeValue, "0")));

        for (var i = 0; i < model.Data.Series.Count; i++)
            el.Add(WriteStandardSeries(model.Data.Series[i], i, model.Data.Categories, false));

        el.Add(new XElement(CmlNames.AxisId, new XAttribute(CmlNames.AttributeValue, CategoryAxisId)));
        el.Add(new XElement(CmlNames.AxisId, new XAttribute(CmlNames.AttributeValue, ValueAxisId)));
        return el;
    }

    // ── Scatter ───────────────────────────────────────────────────────────────

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
        el.Add(new XElement(CmlNames.ScatterStyle, new XAttribute(CmlNames.AttributeValue, style)));
        el.Add(new XElement(CmlNames.VaryColors, new XAttribute(CmlNames.AttributeValue, "0")));

        for (var i = 0; i < model.Data.Series.Count; i++)
            el.Add(WriteScatterSeries(model.Data.Series[i], i));

        el.Add(new XElement(CmlNames.AxisId, new XAttribute(CmlNames.AttributeValue, CategoryAxisId)));
        el.Add(new XElement(CmlNames.AxisId, new XAttribute(CmlNames.AttributeValue, ValueAxisId)));
        return el;
    }

    // ── Radar ─────────────────────────────────────────────────────────────────

    private static XElement WriteRadarChart(ChartModel model)
    {
        var el = new XElement(CmlNames.RadarChart);

        var style = model.Type switch
        {
            ChartType.RadarWithMarkers => "marker",
            ChartType.RadarFilled => "filled",
            _ => "standard"
        };
        el.Add(new XElement(CmlNames.RadarStyle, new XAttribute(CmlNames.AttributeValue, style)));
        el.Add(new XElement(CmlNames.VaryColors, new XAttribute(CmlNames.AttributeValue, "0")));

        for (var i = 0; i < model.Data.Series.Count; i++)
            el.Add(WriteStandardSeries(model.Data.Series[i], i, model.Data.Categories, false));

        el.Add(new XElement(CmlNames.AxisId, new XAttribute(CmlNames.AttributeValue, CategoryAxisId)));
        el.Add(new XElement(CmlNames.AxisId, new XAttribute(CmlNames.AttributeValue, ValueAxisId)));
        return el;
    }

    // ── Bubble ────────────────────────────────────────────────────────────────

    private static XElement WriteBubbleChart(ChartModel model)
    {
        var el = new XElement(CmlNames.BubbleChart);
        el.Add(new XElement(CmlNames.VaryColors, new XAttribute(CmlNames.AttributeValue, "0")));

        for (var i = 0; i < model.Data.Series.Count; i++)
            el.Add(WriteScatterSeries(model.Data.Series[i], i));

        el.Add(new XElement(CmlNames.AxisId, new XAttribute(CmlNames.AttributeValue, CategoryAxisId)));
        el.Add(new XElement(CmlNames.AxisId, new XAttribute(CmlNames.AttributeValue, ValueAxisId)));
        return el;
    }

    // ── Series builders ───────────────────────────────────────────────────────

    private static XElement WriteStandardSeries(
        ChartSeries series,
        int index,
        IReadOnlyList<string> categories,
        bool includeMarker
    )
    {
        var ser = new XElement(CmlNames.Series);
        ser.Add(new XElement(CmlNames.Index, new XAttribute(CmlNames.AttributeValue, index)));
        ser.Add(new XElement(CmlNames.Order, new XAttribute(CmlNames.AttributeValue, index)));

        // Series name
        ser.Add(WriteSeriesName(series.Name));

        // Per-series fill (<c:spPr>) — emitted right after the series name per the schema.
        if (series.Fill is { } fill)
        {
            var spPr = new XElement(DmlNames.Dml + "spPr");
            FillWriter.Write(spPr, fill);
            ser.Add(spPr);
        }

        if (includeMarker)
        {
            ser.Add(new XElement(CmlNames.Marker,
                new XElement(CmlNames.MarkerSymbol, new XAttribute(CmlNames.AttributeValue, "circle"))));
        }

        // Data labels and trendline precede the category/value data per the c:ser schema.
        if (series.DataLabels is { } dl)
            ser.Add(WriteDataLabels(dl));
        if (series.Trendline is { } tl)
            ser.Add(WriteTrendline(tl));

        // Categories
        if (categories.Count > 0)
        {
            var cat = new XElement(CmlNames.Category);
            cat.Add(WriteStringLiteral(categories));
            ser.Add(cat);
        }

        // Values
        var val = new XElement(CmlNames.Values);
        val.Add(WriteNumberLiteral(series.Values));
        ser.Add(val);

        return ser;
    }

    private static XElement WriteDataLabels(ChartDataLabels labels)
    {
        var c = CmlNames.Cml;
        var dLbls = new XElement(CmlNames.Cml + "dLbls");
        if (!string.IsNullOrEmpty(labels.NumberFormat))
        {
            dLbls.Add(new XElement(c + "numFmt",
                new XAttribute("formatCode", labels.NumberFormat),
                new XAttribute("sourceLinked", "0")));
        }

        if (!string.IsNullOrEmpty(labels.Position))
            dLbls.Add(new XElement(c + "dLblPos", new XAttribute(CmlNames.AttributeValue, labels.Position)));
        dLbls.Add(new XElement(c + "showLegendKey", new XAttribute(CmlNames.AttributeValue, labels.ShowLegendKey ? "1" : "0")));
        dLbls.Add(new XElement(c + "showVal", new XAttribute(CmlNames.AttributeValue, labels.ShowValue ? "1" : "0")));
        dLbls.Add(new XElement(c + "showCatName", new XAttribute(CmlNames.AttributeValue, labels.ShowCategoryName ? "1" : "0")));
        dLbls.Add(new XElement(c + "showSerName", new XAttribute(CmlNames.AttributeValue, labels.ShowSeriesName ? "1" : "0")));
        dLbls.Add(new XElement(c + "showPercent", new XAttribute(CmlNames.AttributeValue, labels.ShowPercentage ? "1" : "0")));
        return dLbls;
    }

    private static XElement WriteTrendline(ChartTrendline trend)
    {
        var c = CmlNames.Cml;
        var tl = new XElement(c + "trendline");
        tl.Add(new XElement(c + "trendlineType", new XAttribute(CmlNames.AttributeValue, trend.Type)));
        if (trend.Order is { } order)
            tl.Add(new XElement(c + "order", new XAttribute(CmlNames.AttributeValue, order)));
        if (trend.Forward is { } fwd)
            tl.Add(new XElement(c + "forward", new XAttribute(CmlNames.AttributeValue, Num(fwd))));
        if (trend.Backward is { } bwd)
            tl.Add(new XElement(c + "backward", new XAttribute(CmlNames.AttributeValue, Num(bwd))));
        tl.Add(new XElement(c + "dispRSqr", new XAttribute(CmlNames.AttributeValue, trend.DisplayRSquared ? "1" : "0")));
        tl.Add(new XElement(c + "dispEq", new XAttribute(CmlNames.AttributeValue, trend.DisplayEquation ? "1" : "0")));
        return tl;
    }

    private static XElement WriteScatterSeries(
        ChartSeries series,
        int index
    )
    {
        var ser = new XElement(CmlNames.Series);
        ser.Add(new XElement(CmlNames.Index, new XAttribute(CmlNames.AttributeValue, index)));
        ser.Add(new XElement(CmlNames.Order, new XAttribute(CmlNames.AttributeValue, index)));
        ser.Add(WriteSeriesName(series.Name));

        // X values — use explicit XValues when present, else fall back to indices (1, 2, 3, …)
        var xValues = series.XValues.Count > 0
            ? series.XValues
            : Enumerable.Range(1, series.Values.Count)
                .Select(static n => (double)n)
                .ToList();
        var xVal = new XElement(CmlNames.XValues);
        xVal.Add(WriteNumberLiteral(xValues));
        ser.Add(xVal);

        // Y values
        var yVal = new XElement(CmlNames.YValues);
        yVal.Add(WriteNumberLiteral(series.Values));
        ser.Add(yVal);

        return ser;
    }

    private static XElement WriteSeriesName(string name)
    {
        var tx = new XElement(CmlNames.Text);
        var strLit = new XElement(CmlNames.StringLiteral);
        strLit.Add(new XElement(CmlNames.PointCount, new XAttribute(CmlNames.AttributeValue, "1")));
        strLit.Add(new XElement(CmlNames.Point,
            new XAttribute(CmlNames.AttributeIndex, "0"),
            new XElement(CmlNames.PointValue, name)));
        tx.Add(strLit);
        return tx;
    }

    private static XElement WriteStringLiteral(IReadOnlyList<string> values)
    {
        var strLit = new XElement(CmlNames.StringLiteral);
        strLit.Add(new XElement(CmlNames.PointCount, new XAttribute(CmlNames.AttributeValue, values.Count)));
        for (var i = 0; i < values.Count; i++)
        {
            strLit.Add(new XElement(CmlNames.Point,
                new XAttribute(CmlNames.AttributeIndex, i),
                new XElement(CmlNames.PointValue, values[i])));
        }

        return strLit;
    }

    private static XElement WriteNumberLiteral(IReadOnlyList<double> values)
    {
        var numLit = new XElement(CmlNames.NumberLiteral);
        numLit.Add(new XElement(CmlNames.FormatCode, "General"));
        numLit.Add(new XElement(CmlNames.PointCount, new XAttribute(CmlNames.AttributeValue, values.Count)));
        for (var i = 0; i < values.Count; i++)
        {
            numLit.Add(new XElement(CmlNames.Point,
                new XAttribute(CmlNames.AttributeIndex, i),
                new XElement(CmlNames.PointValue,
                    values[i].ToString("G", CultureInfo.InvariantCulture))));
        }

        return numLit;
    }

    // ── Axes ──────────────────────────────────────────────────────────────────

    private static XElement WriteCategoryAxis(ChartType type, ChartAxis axis)
    {
        // Horizontal bar charts swap the axis positions
        var (catPos, _) = type is ChartType.BarClustered or ChartType.BarStacked or ChartType.BarFullStacked
            ? ("l", "b")
            : ("b", "l");

        var ax = new XElement(CmlNames.CategoryAxis);
        ax.Add(new XElement(CmlNames.AxisId, new XAttribute(CmlNames.AttributeValue, CategoryAxisId)));
        ax.Add(WriteScaling(axis));
        ax.Add(new XElement(CmlNames.Delete, new XAttribute(CmlNames.AttributeValue, axis.IsVisible ? "0" : "1")));
        ax.Add(new XElement(CmlNames.AxisPosition, new XAttribute(CmlNames.AttributeValue, axis.Position ?? catPos)));
        if (axis.HasMajorGridlines) ax.Add(new XElement(CmlNames.Cml + "majorGridlines"));
        if (axis.HasMinorGridlines) ax.Add(new XElement(CmlNames.Cml + "minorGridlines"));
        if (!string.IsNullOrEmpty(axis.Title)) ax.Add(WriteAxisTitle(axis.Title));
        if (!string.IsNullOrEmpty(axis.NumberFormat))
        {
            ax.Add(new XElement(CmlNames.Cml + "numFmt",
                new XAttribute("formatCode", axis.NumberFormat),
                new XAttribute("sourceLinked", "0")));
        }

        ax.Add(new XElement(CmlNames.CrossAxis, new XAttribute(CmlNames.AttributeValue, ValueAxisId)));
        return ax;
    }

    private static XElement WriteValueAxis(ChartType type, ChartAxis axis)
    {
        var valPos = type is ChartType.BarClustered or ChartType.BarStacked or ChartType.BarFullStacked
            ? "b"
            : "l";

        var ax = new XElement(CmlNames.ValueAxis);
        ax.Add(new XElement(CmlNames.AxisId, new XAttribute(CmlNames.AttributeValue, ValueAxisId)));
        ax.Add(WriteScaling(axis));
        ax.Add(new XElement(CmlNames.Delete, new XAttribute(CmlNames.AttributeValue, axis.IsVisible ? "0" : "1")));
        ax.Add(new XElement(CmlNames.AxisPosition, new XAttribute(CmlNames.AttributeValue, axis.Position ?? valPos)));
        if (axis.HasMajorGridlines) ax.Add(new XElement(CmlNames.Cml + "majorGridlines"));
        if (axis.HasMinorGridlines) ax.Add(new XElement(CmlNames.Cml + "minorGridlines"));
        if (!string.IsNullOrEmpty(axis.Title)) ax.Add(WriteAxisTitle(axis.Title));
        if (!string.IsNullOrEmpty(axis.NumberFormat))
        {
            ax.Add(new XElement(CmlNames.Cml + "numFmt",
                new XAttribute("formatCode", axis.NumberFormat),
                new XAttribute("sourceLinked", "0")));
        }

        ax.Add(new XElement(CmlNames.CrossAxis, new XAttribute(CmlNames.AttributeValue, CategoryAxisId)));
        if (axis.MajorUnit is { } mu)
            ax.Add(new XElement(CmlNames.Cml + "majorUnit", new XAttribute(CmlNames.AttributeValue, Num(mu))));
        if (axis.MinorUnit is { } nu)
            ax.Add(new XElement(CmlNames.Cml + "minorUnit", new XAttribute(CmlNames.AttributeValue, Num(nu))));
        return ax;
    }

    private static XElement WriteScaling(ChartAxis axis)
    {
        var scaling = new XElement(CmlNames.Scaling,
            new XElement(CmlNames.Orientation, new XAttribute(CmlNames.AttributeValue, "minMax")));
        if (axis.Maximum is { } max)
            scaling.Add(new XElement(CmlNames.Cml + "max", new XAttribute(CmlNames.AttributeValue, Num(max))));
        if (axis.Minimum is { } min)
            scaling.Add(new XElement(CmlNames.Cml + "min", new XAttribute(CmlNames.AttributeValue, Num(min))));
        return scaling;
    }

    private static XElement WriteAxisTitle(string title)
    {
        var a = DmlNames.Dml;
        return new XElement(CmlNames.Title,
            new XElement(CmlNames.Text,
                new XElement(CmlNames.Rich,
                    new XElement(a + "bodyPr"),
                    new XElement(a + "lstStyle"),
                    new XElement(a + "p",
                        new XElement(a + "r",
                            new XElement(a + "t", title))))),
            new XElement(CmlNames.Cml + "overlay", new XAttribute(CmlNames.AttributeValue, "0")));
    }

    private static string Num(double value) =>
        value.ToString(CultureInfo.InvariantCulture);

    // ── Legend ────────────────────────────────────────────────────────────────

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

        var legendEl = new XElement(CmlNames.Legend);
        legendEl.Add(new XElement(CmlNames.LegendPosition, new XAttribute(CmlNames.AttributeValue, posVal)));
        legendEl.Add(new XElement(CmlNames.Overlay, new XAttribute(CmlNames.AttributeValue, legend.IsOverlay ? "1" : "0")));
        return legendEl;
    }
}
