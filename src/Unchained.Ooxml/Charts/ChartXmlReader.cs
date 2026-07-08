using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;

namespace Unchained.Ooxml.Charts;

/// <summary>
///     Parses a <c>c:chartSpace</c> XML root into a <see cref="ChartModel" />. Only cached / literal
///     data is read (workbook references are read from their embedded cache). Shared by every format
///     that embeds charts. A caller-supplied hook (passed to
///     Parse) injects format-specific per-series fill parsing; pass <see langword="null" />
///     to skip per-series fills.
/// </summary>
public static class ChartXmlReader
{
    /// <summary>Reads <paramref name="chartSpaceRoot" /> into a new <see cref="ChartModel" />.</summary>
    public static ChartModel Parse(XElement chartSpaceRoot, Action<XElement, FillFormat>? fillReader = null)
    {
        var model = new ChartModel();
        Parse(chartSpaceRoot, model, fillReader);
        return model;
    }

    /// <summary>Populates <paramref name="model" /> from <paramref name="chartSpaceRoot" />.</summary>
    public static void Parse(XElement chartSpaceRoot, ChartModel model, Action<XElement, FillFormat>? fillReader = null)
    {
        var chartEl = chartSpaceRoot.Element(CmlNames.Chart);
        if (chartEl == null) return;

        ParseTitle(chartEl.Element(CmlNames.Title), model);

        var plotArea = chartEl.Element(CmlNames.PlotArea);
        if (plotArea != null)
            ParsePlotArea(plotArea, model, fillReader);

        ParseLegend(chartEl.Element(CmlNames.Legend), model.Legend);
    }

    // ── Title ─────────────────────────────────────────────────────────────────

    private static void ParseTitle(XContainer? titleEl, ChartModel model)
    {
        if (titleEl == null)
        {
            model.HasTitle = false;
            return;
        }

        model.HasTitle = true;

        var richEl = titleEl.Element(CmlNames.Text)?.Element(CmlNames.Rich);
        if (richEl != null)
        {
            model.Title = string.Concat(richEl.Descendants(DmlNames.Text).Select(static t => t.Value));
            return;
        }

        var strCache = titleEl.Element(CmlNames.Text)?.Element(CmlNames.StringReference)?.Element(CmlNames.StringCache);
        if (strCache == null) return;

        var pt = strCache.Elements(CmlNames.Point).FirstOrDefault();
        model.Title = pt?.Element(CmlNames.PointValue)?.Value ?? string.Empty;
    }

    // ── Plot area ─────────────────────────────────────────────────────────────

    private static void ParsePlotArea(XContainer plotArea, ChartModel model, Action<XElement, FillFormat>? fillReader)
    {
        foreach (var child in plotArea.Elements())
        {
            var (chartType, found) = MapElementToChartType(child);
            if (!found) continue;

            model.Type = chartType;
            ParseSeries(child, model.Data, fillReader);
            break;
        }

        foreach (var catAx in plotArea.Elements(CmlNames.CategoryAxis))
            ParseAxis(catAx, model.CategoryAxis);
        foreach (var valAx in plotArea.Elements(CmlNames.ValueAxis))
            ParseAxis(valAx, model.ValueAxis);
    }

    private static void ParseAxis(XContainer axEl, ChartAxis axis)
    {
        var delete = axEl.Element(CmlNames.Cml + "delete")?.GetAttrInt(DmlNames.AttributeValue);
        axis.IsVisible = delete is not 1;

        var scaling = axEl.Element(CmlNames.Scaling);
        if (scaling is not null)
        {
            axis.Minimum = scaling.Element(CmlNames.Cml + "min")?.GetAttrDouble(DmlNames.AttributeValue);
            axis.Maximum = scaling.Element(CmlNames.Cml + "max")?.GetAttrDouble(DmlNames.AttributeValue);
        }

        axis.MajorUnit = axEl.Element(CmlNames.Cml + "majorUnit")?.GetAttrDouble(DmlNames.AttributeValue);
        axis.MinorUnit = axEl.Element(CmlNames.Cml + "minorUnit")?.GetAttrDouble(DmlNames.AttributeValue);
        axis.HasMajorGridlines = axEl.Element(CmlNames.Cml + "majorGridlines") is not null;
        axis.HasMinorGridlines = axEl.Element(CmlNames.Cml + "minorGridlines") is not null;
        axis.Position = axEl.Element(CmlNames.Cml + "axPos")?.GetAttr(DmlNames.AttributeValue);
        axis.NumberFormat = axEl.Element(CmlNames.Cml + "numFmt")?.GetAttr("formatCode");

        var titleRuns = axEl.Element(CmlNames.Title)
            ?.Element(CmlNames.Text)
            ?.Element(CmlNames.Rich)
            ?.Descendants(DmlNames.Dml + "t")
            .Select(static t => t.Value);
        if (titleRuns is null) return;

        var title = string.Concat(titleRuns);
        if (!string.IsNullOrEmpty(title)) axis.Title = title;
    }

    private static (ChartType type, bool found) MapElementToChartType(XElement element)
    {
        if (element.Name == CmlNames.BarChart)
        {
            var dir = element.Element(CmlNames.BarDirection)?.GetAttr(DmlNames.AttributeValue, CmlNames.BarDirColumn) ?? CmlNames.BarDirColumn;
            var grouping = element.Element(CmlNames.Grouping)?.GetAttr(DmlNames.AttributeValue, CmlNames.GroupingClustered) ?? CmlNames.GroupingClustered;
            return ((dir, grouping) switch
            {
                (CmlNames.BarDirColumn, CmlNames.GroupingClustered) => ChartType.ColumnClustered,
                (CmlNames.BarDirColumn, CmlNames.GroupingStacked) => ChartType.ColumnStacked,
                (CmlNames.BarDirColumn, CmlNames.GroupingPercentStacked) => ChartType.ColumnFullStacked,
                (CmlNames.BarDirBar, CmlNames.GroupingClustered) => ChartType.BarClustered,
                (CmlNames.BarDirBar, CmlNames.GroupingStacked) => ChartType.BarStacked,
                (CmlNames.BarDirBar, CmlNames.GroupingPercentStacked) => ChartType.BarFullStacked,
                _ => ChartType.ColumnClustered
            }, true);
        }

        if (element.Name == CmlNames.LineChart)
        {
            var grouping = element.Element(CmlNames.Grouping)?.GetAttr(DmlNames.AttributeValue, CmlNames.GroupingStandard) ?? CmlNames.GroupingStandard;
            var hasMarkers = element.Elements(CmlNames.Series).Any(static s => s.Element(CmlNames.Marker) != null);
            return ((grouping, hasMarkers) switch
            {
                (CmlNames.GroupingStacked, false) => ChartType.LineStacked,
                (CmlNames.GroupingStacked, true) => ChartType.LineWithMarkersStacked,
                (CmlNames.GroupingPercentStacked, false) => ChartType.LineFullStacked,
                (CmlNames.GroupingPercentStacked, true) => ChartType.LineWithMarkersFullStacked,
                (_, true) => ChartType.LineWithMarkers,
                _ => ChartType.Line
            }, true);
        }

        if (element.Name == CmlNames.PieChart)
        {
            var hasExplosion = element.Elements(CmlNames.Series)
                .Any(static s => s.Element(CmlNames.Cml + "explosion")?.GetAttrInt(DmlNames.AttributeValue) > 0);
            return (hasExplosion ? ChartType.PieExploded : ChartType.Pie, true);
        }

        if (element.Name == CmlNames.DoughnutChart)
            return (ChartType.Doughnut, true);

        if (element.Name == CmlNames.AreaChart)
        {
            var grouping = element.Element(CmlNames.Grouping)?.GetAttr(DmlNames.AttributeValue, CmlNames.GroupingStandard) ?? CmlNames.GroupingStandard;
            return (grouping switch
            {
                CmlNames.GroupingStacked => ChartType.AreaStacked,
                CmlNames.GroupingPercentStacked => ChartType.AreaFullStacked,
                _ => ChartType.Area
            }, true);
        }

        if (element.Name == CmlNames.ScatterChart)
        {
            var style = element.Element(CmlNames.ScatterStyle)?.GetAttr(DmlNames.AttributeValue, CmlNames.ScatterStyleMarker) ?? CmlNames.ScatterStyleMarker;
            return (style switch
            {
                CmlNames.ScatterStyleLine => ChartType.ScatterWithStraightLines,
                CmlNames.ScatterStyleLineMarker => ChartType.ScatterWithStraightLinesAndMarkers,
                CmlNames.ScatterStyleSmooth => ChartType.ScatterWithSmoothLines,
                CmlNames.ScatterStyleSmoothMarker => ChartType.ScatterWithSmoothLinesAndMarkers,
                _ => ChartType.ScatterWithMarkersOnly
            }, true);
        }

        // ReSharper disable once InvertIf
        if (element.Name == CmlNames.RadarChart)
        {
            var style = element.Element(CmlNames.RadarStyle)?.GetAttr(DmlNames.AttributeValue, CmlNames.GroupingStandard) ?? CmlNames.GroupingStandard;
            return (style switch
            {
                CmlNames.RadarStyleMarker => ChartType.RadarWithMarkers,
                CmlNames.RadarStyleFilled => ChartType.RadarFilled,
                _ => ChartType.Radar
            }, true);
        }

        return element.Name == CmlNames.BubbleChart ? (ChartType.Bubble, true) : (ChartType.ColumnClustered, false);
    }

    // ── Series ────────────────────────────────────────────────────────────────

    private static void ParseSeries(XContainer chartTypeEl, ChartData data, Action<XElement, FillFormat>? fillReader)
    {
        var isFirstSeries = true;
        foreach (var serEl in chartTypeEl.Elements(CmlNames.Series))
        {
            var series = new ChartSeries { Name = ParseSeriesName(serEl) };

            if (isFirstSeries)
            {
                ParseCategories(serEl, data);
                isFirstSeries = false;
            }

            ParseValues(serEl, series);
            ParseXValues(serEl, series);
            ParseSeriesFormatting(serEl, series, fillReader);
            data.Series.Add(series);
        }
    }

    private static void ParseSeriesFormatting(XContainer serEl, ChartSeries series, Action<XElement, FillFormat>? fillReader)
    {
        var spPr = serEl.Element(DmlNames.Dml + "spPr");
        if (fillReader != null && spPr?.Element(DmlNames.SolidFill) != null)
        {
            var fill = new FillFormat();
            fillReader(spPr, fill);
            series.Fill = fill;
        }

        var dLbls = serEl.Element(CmlNames.Cml + "dLbls");
        if (dLbls is not null)
            series.DataLabels = ParseDataLabels(dLbls);

        var tl = serEl.Element(CmlNames.Cml + "trendline");
        if (tl is not null)
            series.Trendline = ParseTrendline(tl);
    }

    private static ChartDataLabels ParseDataLabels(XContainer dLbls)
    {
        var c = CmlNames.Cml;
        return new ChartDataLabels
        {
            IsVisible = true,
            ShowValue = Show("showVal", true),
            ShowCategoryName = Show("showCatName", false),
            ShowSeriesName = Show("showSerName", false),
            ShowPercentage = Show("showPercent", false),
            ShowLegendKey = Show("showLegendKey", false),
            Position = dLbls.Element(c + "dLblPos")?.GetAttr(DmlNames.AttributeValue),
            NumberFormat = dLbls.Element(c + "numFmt")?.GetAttr("formatCode")
        };

        bool Show(string name, bool dflt) =>
            dLbls.Element(c + name)?.GetAttrInt(DmlNames.AttributeValue) is { } v ? v == 1 : dflt;
    }

    private static ChartTrendline ParseTrendline(XContainer tl)
    {
        var c = CmlNames.Cml;
        return new ChartTrendline
        {
            Type = tl.Element(c + "trendlineType")?.GetAttr(DmlNames.AttributeValue, CmlNames.TrendlineTypeLinear) ?? CmlNames.TrendlineTypeLinear,
            Order = tl.Element(c + "order")?.GetAttrInt(DmlNames.AttributeValue),
            Forward = tl.Element(c + "forward")?.GetAttrDouble(DmlNames.AttributeValue),
            Backward = tl.Element(c + "backward")?.GetAttrDouble(DmlNames.AttributeValue),
            DisplayEquation = tl.Element(c + "dispEq")?.GetAttrInt(DmlNames.AttributeValue) == 1,
            DisplayRSquared = tl.Element(c + "dispRSqr")?.GetAttrInt(DmlNames.AttributeValue) == 1
        };
    }

    private static string ParseSeriesName(XContainer serEl)
    {
        var txEl = serEl.Element(CmlNames.Text);
        if (txEl == null) return string.Empty;

        // Plain <c:v> form (CT_SerTx) — what we now write.
        var direct = txEl.Element(CmlNames.PointValue);
        if (direct != null)
            return direct.Value;

        var strLit = txEl.Element(CmlNames.StringLiteral);
        if (strLit != null)
        {
            var pt = strLit.Elements(CmlNames.Point).FirstOrDefault(static p => p.GetAttrInt(CmlNames.AttributeIndex) == 0);
            return pt?.Element(CmlNames.PointValue)?.Value ?? string.Empty;
        }

        var strCache = txEl.Element(CmlNames.StringReference)?.Element(CmlNames.StringCache);
        // ReSharper disable once InvertIf
        if (strCache != null)
        {
            var pt = strCache.Elements(CmlNames.Point).FirstOrDefault(static p => p.GetAttrInt(CmlNames.AttributeIndex) == 0);
            return pt?.Element(CmlNames.PointValue)?.Value ?? string.Empty;
        }

        return string.Empty;
    }

    private static void ParseCategories(XContainer serEl, ChartData data)
    {
        var catEl = serEl.Element(CmlNames.Category);
        if (catEl == null) return;

        data.Categories.AddRange(ReadStringPoints(catEl));
    }

    private static void ParseXValues(XContainer serEl, ChartSeries series)
    {
        var xValEl = serEl.Element(CmlNames.XValues);
        if (xValEl == null) return;

        series.XValues.AddRange(ReadNumericPoints(xValEl));
    }

    private static void ParseValues(XContainer serEl, ChartSeries series)
    {
        var valEl = serEl.Element(CmlNames.Values) ?? serEl.Element(CmlNames.YValues);
        if (valEl == null) return;

        series.Values.AddRange(ReadNumericPoints(valEl));
    }

    // ── Legend ────────────────────────────────────────────────────────────────

    private static void ParseLegend(XContainer? legendEl, ChartLegend legend)
    {
        if (legendEl == null)
        {
            legend.IsVisible = false;
            return;
        }

        legend.IsVisible = true;
        legend.IsOverlay = legendEl.Element(CmlNames.Overlay)?.GetAttrBool(DmlNames.AttributeValue) ?? false;
        var posVal = legendEl.Element(CmlNames.LegendPosition)?.GetAttr(DmlNames.AttributeValue, "b") ?? "b";
        legend.Position = posVal switch
        {
            "t" => ChartLegendPosition.Top,
            "l" => ChartLegendPosition.Left,
            "r" => ChartLegendPosition.Right,
            "tr" => ChartLegendPosition.TopRight,
            _ => ChartLegendPosition.Bottom
        };
    }

    // ── Data point readers ────────────────────────────────────────────────────

    private static IEnumerable<string> ReadStringPoints(XContainer containerEl)
    {
        var results = new List<string>();

        var strLit = containerEl.Element(CmlNames.StringLiteral);
        if (strLit != null)
        {
            results.AddRange(strLit.Elements(CmlNames.Point).Select(static pt => pt.Element(CmlNames.PointValue)?.Value ?? string.Empty));
            return results;
        }

        var numLit = containerEl.Element(CmlNames.NumberLiteral);
        if (numLit != null)
        {
            results.AddRange(numLit.Elements(CmlNames.Point).Select(static pt => pt.Element(CmlNames.PointValue)?.Value ?? string.Empty));
            return results;
        }

        var strCache = containerEl.Element(CmlNames.StringReference)?.Element(CmlNames.StringCache);
        if (strCache != null)
        {
            results.AddRange(strCache.Elements(CmlNames.Point).Select(static pt => pt.Element(CmlNames.PointValue)?.Value ?? string.Empty));
            return results;
        }

        var numCache = containerEl.Element(CmlNames.NumberReference)?.Element(CmlNames.NumberCache);
        if (numCache != null)
            results.AddRange(numCache.Elements(CmlNames.Point).Select(static pt => pt.Element(CmlNames.PointValue)?.Value ?? string.Empty));

        return results;
    }

    private static IEnumerable<double> ReadNumericPoints(XContainer containerEl)
    {
        var results = new List<double>();

        var numLit = containerEl.Element(CmlNames.NumberLiteral);
        if (numLit != null)
        {
            foreach (var pt in numLit.Elements(CmlNames.Point))
            {
                if (double.TryParse(pt.Element(CmlNames.PointValue)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    results.Add(v);
            }

            return results;
        }

        var numCache = containerEl.Element(CmlNames.NumberReference)?.Element(CmlNames.NumberCache);
        if (numCache == null) return results;

        foreach (var pt in numCache.Elements(CmlNames.Point))
        {
            if (double.TryParse(pt.Element(CmlNames.PointValue)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                results.Add(v);
        }

        return results;
    }
}
