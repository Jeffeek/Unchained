using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Core.Xml;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Parses a <c>&lt;c:chartSpace&gt;</c> XML root into a <see cref="ChartModel" />.
///     Only cached/literal data is read; workbook references are used as a fallback
///     label but raw data is read from the embedded cache.
/// </summary>
internal static class ChartParser
{
    /// <summary>
    ///     Populates <paramref name="model" /> from the <c>&lt;c:chartSpace&gt;</c> root element.
    ///     Fields not present in the XML are left at their default values.
    /// </summary>
    public static void Parse(XElement chartSpaceRoot, ChartModel model)
    {
        var chartEl = chartSpaceRoot.Element(CmlNames.Chart);
        if (chartEl == null) return;

        ParseTitle(chartEl.Element(CmlNames.Title), model);

        var plotArea = chartEl.Element(CmlNames.PlotArea);
        if (plotArea != null)
            ParsePlotArea(plotArea, model);

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

        // <c:tx><c:rich>...<a:t>text</a:t>...
        var richEl = titleEl.Element(CmlNames.Text)?.Element(CmlNames.Rich);
        if (richEl != null)
        {
            var texts = richEl.Descendants(DmlNames.Text)
                .Select(static t => t.Value)
                .ToList();
            model.Title = string.Concat(texts);
            return;
        }

        // <c:tx><c:strRef><c:strCache><c:pt idx="0"><c:v>...</c:v>
        var strCache = titleEl.Element(CmlNames.Text)
            ?.Element(CmlNames.StringReference)
            ?.Element(CmlNames.StringCache);

        if (strCache == null) return;

        var pt = strCache.Elements(CmlNames.Point).FirstOrDefault();
        model.Title = pt?.Element(CmlNames.PointValue)?.Value ?? string.Empty;
    }

    // ── Plot area ─────────────────────────────────────────────────────────────

    private static void ParsePlotArea(XContainer plotArea, ChartModel model)
    {
        // Find the first recognised chart type element and parse its series.
        foreach (var child in plotArea.Elements())
        {
            var (chartType, found) = MapElementToChartType(child);
            if (!found) continue;

            model.Type = chartType;
            ParseSeries(child, model.Data);
            break;
        }

        // Axes (catAx / valAx) live as siblings of the chart-type element in the plot area.
        foreach (var catAx in plotArea.Elements(CmlNames.CategoryAxis))
            ParseAxis(catAx, model.CategoryAxis);
        foreach (var valAx in plotArea.Elements(CmlNames.ValueAxis))
            ParseAxis(valAx, model.ValueAxis);
    }

    private static void ParseAxis(XContainer axEl, ChartAxis axis)
    {
        var delete = axEl.Element(CmlNames.Cml + "delete")?.GetAttrInt(CmlNames.AttributeValue);
        axis.IsVisible = delete is not 1;

        var scaling = axEl.Element(CmlNames.Scaling);
        if (scaling is not null)
        {
            axis.Minimum = scaling.Element(CmlNames.Cml + "min")?.GetAttrDouble(CmlNames.AttributeValue);
            axis.Maximum = scaling.Element(CmlNames.Cml + "max")?.GetAttrDouble(CmlNames.AttributeValue);
        }

        axis.MajorUnit = axEl.Element(CmlNames.Cml + "majorUnit")?.GetAttrDouble(CmlNames.AttributeValue);
        axis.MinorUnit = axEl.Element(CmlNames.Cml + "minorUnit")?.GetAttrDouble(CmlNames.AttributeValue);
        axis.HasMajorGridlines = axEl.Element(CmlNames.Cml + "majorGridlines") is not null;
        axis.HasMinorGridlines = axEl.Element(CmlNames.Cml + "minorGridlines") is not null;
        axis.Position = axEl.Element(CmlNames.Cml + "axPos")?.GetAttr(CmlNames.AttributeValue);
        axis.NumberFormat = axEl.Element(CmlNames.Cml + "numFmt")?.GetAttr("formatCode");

        // Axis title (c:title/c:tx/c:rich text runs).
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
            var dir = element.Element(CmlNames.BarDirection)?.GetAttr(CmlNames.AttributeValue, "col") ?? "col";
            var grouping = element.Element(CmlNames.Grouping)?.GetAttr(CmlNames.AttributeValue, "clustered") ?? "clustered";
            var type = (dir, grouping) switch
            {
                ("col", "clustered") => ChartType.ColumnClustered,
                ("col", "stacked") => ChartType.ColumnStacked,
                ("col", "percentStacked") => ChartType.ColumnFullStacked,
                ("bar", "clustered") => ChartType.BarClustered,
                ("bar", "stacked") => ChartType.BarStacked,
                ("bar", "percentStacked") => ChartType.BarFullStacked,
                _ => ChartType.ColumnClustered
            };
            return (type, true);
        }

        if (element.Name == CmlNames.LineChart)
        {
            var grouping = element.Element(CmlNames.Grouping)?.GetAttr(CmlNames.AttributeValue, "standard") ?? "standard";
            var hasMarkers = element.Elements(CmlNames.Series)
                .Any(static s => s.Element(CmlNames.Marker) != null);
            var type = (grouping, hasMarkers) switch
            {
                ("stacked", false) => ChartType.LineStacked,
                ("stacked", true) => ChartType.LineWithMarkersStacked,
                ("percentStacked", false) => ChartType.LineFullStacked,
                ("percentStacked", true) => ChartType.LineWithMarkersFullStacked,
                (_, true) => ChartType.LineWithMarkers,
                _ => ChartType.Line
            };
            return (type, true);
        }

        if (element.Name == CmlNames.PieChart)
        {
            var hasExplosion = element.Elements(CmlNames.Series)
                .Any(static s =>
                    {
                        var exp = s.Element(CmlNames.Cml + "explosion");
                        return exp?.GetAttrInt(CmlNames.AttributeValue) > 0;
                    }
                );
            return (hasExplosion ? ChartType.PieExploded : ChartType.Pie, true);
        }

        if (element.Name == CmlNames.DoughnutChart)
            return (ChartType.Doughnut, true);

        if (element.Name == CmlNames.AreaChart)
        {
            var grouping = element.Element(CmlNames.Grouping)?.GetAttr(CmlNames.AttributeValue, "standard") ?? "standard";
            var type = grouping switch
            {
                "stacked" => ChartType.AreaStacked,
                "percentStacked" => ChartType.AreaFullStacked,
                _ => ChartType.Area
            };
            return (type, true);
        }

        if (element.Name == CmlNames.ScatterChart)
        {
            var style = element.Element(CmlNames.ScatterStyle)?.GetAttr(CmlNames.AttributeValue, "marker") ?? "marker";
            var type = style switch
            {
                "line" => ChartType.ScatterWithStraightLines,
                "lineMarker" => ChartType.ScatterWithStraightLinesAndMarkers,
                "smooth" => ChartType.ScatterWithSmoothLines,
                "smoothMarker" => ChartType.ScatterWithSmoothLinesAndMarkers,
                _ => ChartType.ScatterWithMarkersOnly
            };
            return (type, true);
        }

        // ReSharper disable once InvertIf
        if (element.Name == CmlNames.RadarChart)
        {
            var style = element.Element(CmlNames.RadarStyle)?.GetAttr(CmlNames.AttributeValue, "standard") ?? "standard";
            var type = style switch
            {
                "marker" => ChartType.RadarWithMarkers,
                "filled" => ChartType.RadarFilled,
                _ => ChartType.Radar
            };
            return (type, true);
        }

        return element.Name == CmlNames.BubbleChart ? (ChartType.Bubble, true) : (ChartType.ColumnClustered, false);
    }

    // ── Series ────────────────────────────────────────────────────────────────

    private static void ParseSeries(XContainer chartTypeEl, ChartData data)
    {
        var isFirstSeries = true;
        foreach (var serEl in chartTypeEl.Elements(CmlNames.Series))
        {
            var series = new ChartSeries { Name = ParseSeriesName(serEl) };

            // Read categories from the first series only (shared by all series)
            if (isFirstSeries)
            {
                ParseCategories(serEl, data);
                isFirstSeries = false;
            }

            ParseValues(serEl, series);
            ParseXValues(serEl, series);
            ParseSeriesFormatting(serEl, series);
            data.Series.Add(series);
        }
    }

    private static void ParseSeriesFormatting(XContainer serEl, ChartSeries series)
    {
        // Per-series fill (<c:spPr> with a fill child).
        var spPr = serEl.Element(DmlNames.Dml + "spPr");
        if (spPr?.Element(DmlNames.SolidFill) != null)
        {
            var fill = new FillFormat();
            FillParser.Parse(spPr, fill);
            series.Fill = fill;
        }

        // Data labels (<c:dLbls>).
        var dLbls = serEl.Element(CmlNames.Cml + "dLbls");
        if (dLbls is not null)
            series.DataLabels = ParseDataLabels(dLbls);

        // Trendline (<c:trendline>).
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
            Position = dLbls.Element(c + "dLblPos")?.GetAttr(CmlNames.AttributeValue),
            NumberFormat = dLbls.Element(c + "numFmt")?.GetAttr("formatCode")
        };

        bool Show(string name, bool dflt) =>
            dLbls.Element(c + name)?.GetAttrInt(CmlNames.AttributeValue) is { } v ? v == 1 : dflt;
    }

    private static ChartTrendline ParseTrendline(XContainer tl)
    {
        var c = CmlNames.Cml;
        return new ChartTrendline
        {
            Type = tl.Element(c + "trendlineType")?.GetAttr(CmlNames.AttributeValue, "linear") ?? "linear",
            Order = tl.Element(c + "order")?.GetAttrInt(CmlNames.AttributeValue),
            Forward = tl.Element(c + "forward")?.GetAttrDouble(CmlNames.AttributeValue),
            Backward = tl.Element(c + "backward")?.GetAttrDouble(CmlNames.AttributeValue),
            DisplayEquation = tl.Element(c + "dispEq")?.GetAttrInt(CmlNames.AttributeValue) == 1,
            DisplayRSquared = tl.Element(c + "dispRSqr")?.GetAttrInt(CmlNames.AttributeValue) == 1
        };
    }

    private static string ParseSeriesName(XContainer serEl)
    {
        var txEl = serEl.Element(CmlNames.Text);
        if (txEl == null) return string.Empty;

        // Literal: <c:tx><c:strLit><c:pt idx="0"><c:v>name</c:v>
        var strLit = txEl.Element(CmlNames.StringLiteral);
        if (strLit != null)
        {
            var pt = strLit.Elements(CmlNames.Point).FirstOrDefault(static p => p.GetAttrInt(CmlNames.AttributeIndex) == 0);
            return pt?.Element(CmlNames.PointValue)?.Value ?? string.Empty;
        }

        // Reference with cache: <c:tx><c:strRef><c:strCache>...
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
        // <c:cat> for category-based charts; <c:xVal> for scatter/bubble holds numeric X values,
        // which are parsed separately into ChartSeries.XValues (see ParseXValues).
        var catEl = serEl.Element(CmlNames.Category);
        if (catEl == null) return;

        var points = ReadStringPoints(catEl);
        data.Categories.AddRange(points);
    }

    private static void ParseXValues(XContainer serEl, ChartSeries series)
    {
        // <c:xVal> holds numeric X-axis values for scatter/bubble charts.
        var xValEl = serEl.Element(CmlNames.XValues);
        if (xValEl == null) return;

        var points = ReadNumericPoints(xValEl);
        series.XValues.AddRange(points);
    }

    private static void ParseValues(XContainer serEl, ChartSeries series)
    {
        // <c:val> for standard charts; <c:yVal> for scatter/bubble
        var valEl = serEl.Element(CmlNames.Values)
                    ?? serEl.Element(CmlNames.YValues);
        if (valEl == null) return;

        var points = ReadNumericPoints(valEl);
        series.Values.AddRange(points);
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
        legend.IsOverlay = legendEl.Element(CmlNames.Overlay)?.GetAttrBool(CmlNames.AttributeValue) ?? false;

        var posVal = legendEl.Element(CmlNames.LegendPosition)?.GetAttr(CmlNames.AttributeValue, "b") ?? "b";
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

        // Try literal first
        var strLit = containerEl.Element(CmlNames.StringLiteral);
        if (strLit != null)
        {
            results.AddRange(strLit.Elements(CmlNames.Point).Select(static pt => pt.Element(CmlNames.PointValue)?.Value ?? string.Empty));
            return results;
        }

        // Try numeric literal as string (category numbers)
        var numLit = containerEl.Element(CmlNames.NumberLiteral);
        if (numLit != null)
        {
            results.AddRange(numLit.Elements(CmlNames.Point).Select(static pt => pt.Element(CmlNames.PointValue)?.Value ?? string.Empty));
            return results;
        }

        // Try workbook-linked cached strings
        var strCache = containerEl.Element(CmlNames.StringReference)?.Element(CmlNames.StringCache);
        if (strCache != null)
        {
            results.AddRange(strCache.Elements(CmlNames.Point).Select(static pt => pt.Element(CmlNames.PointValue)?.Value ?? string.Empty));
            return results;
        }

        // Try workbook-linked cached numbers
        var numCache = containerEl.Element(CmlNames.NumberReference)?.Element(CmlNames.NumberCache);
        if (numCache != null)
            results.AddRange(numCache.Elements(CmlNames.Point).Select(static pt => pt.Element(CmlNames.PointValue)?.Value ?? string.Empty));

        return results;
    }

    private static IEnumerable<double> ReadNumericPoints(XContainer containerEl)
    {
        var results = new List<double>();

        // Try literal first
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

        // Try workbook-linked cached numbers
        var numCache = containerEl.Element(CmlNames.NumberReference)?.Element(CmlNames.NumberCache);
        // ReSharper disable once InvertIf
        if (numCache != null)
        {
            foreach (var pt in numCache.Elements(CmlNames.Point))
            {
                if (double.TryParse(pt.Element(CmlNames.PointValue)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    results.Add(v);
            }
        }

        return results;
    }
}
