using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Charts;
using Unchained.Pptx.Core.Xml;

namespace Unchained.Pptx.Parsing;

/// <summary>
/// Parses a <c>&lt;c:chartSpace&gt;</c> XML root into a <see cref="ChartModel"/>.
/// Only cached/literal data is read; workbook references are used as a fallback
/// label but raw data is read from the embedded cache.
/// </summary>
internal static class ChartParser
{
    /// <summary>
    /// Populates <paramref name="model"/> from the <c>&lt;c:chartSpace&gt;</c> root element.
    /// Fields not present in the XML are left at their default values.
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

    private static void ParseTitle(XElement? titleEl, ChartModel model)
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
        if (strCache != null)
        {
            var pt = strCache.Elements(CmlNames.Point).FirstOrDefault();
            model.Title = pt?.Element(CmlNames.PointValue)?.Value ?? string.Empty;
        }
    }

    // ── Plot area ─────────────────────────────────────────────────────────────

    private static void ParsePlotArea(XElement plotArea, ChartModel model)
    {
        // Find the first recognised chart type element
        foreach (var child in plotArea.Elements())
        {
            var (chartType, found) = MapElementToChartType(child);
            if (!found) continue;

            model.Type = chartType;
            ParseSeries(child, model.Data);
            return;
        }
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
                                      });
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

        if (element.Name == CmlNames.BubbleChart)
            return (ChartType.Bubble, true);

        return (ChartType.ColumnClustered, false);
    }

    // ── Series ────────────────────────────────────────────────────────────────

    private static void ParseSeries(XElement chartTypeEl, ChartData data)
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
            data.Series.Add(series);
        }
    }

    private static string ParseSeriesName(XElement serEl)
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
        if (strCache != null)
        {
            var pt = strCache.Elements(CmlNames.Point).FirstOrDefault(static p => p.GetAttrInt(CmlNames.AttributeIndex) == 0);
            return pt?.Element(CmlNames.PointValue)?.Value ?? string.Empty;
        }

        return string.Empty;
    }

    private static void ParseCategories(XElement serEl, ChartData data)
    {
        // <c:cat> for standard charts; <c:xVal> for scatter/bubble
        var catEl = serEl.Element(CmlNames.Category)
                  ?? serEl.Element(CmlNames.XValues);
        if (catEl == null) return;

        var points = ReadStringPoints(catEl);
        data.Categories.AddRange(points);
    }

    private static void ParseValues(XElement serEl, ChartSeries series)
    {
        // <c:val> for standard charts; <c:yVal> for scatter/bubble
        var valEl = serEl.Element(CmlNames.Values)
                 ?? serEl.Element(CmlNames.YValues);
        if (valEl == null) return;

        var points = ReadNumericPoints(valEl);
        series.Values.AddRange(points);
    }

    // ── Legend ────────────────────────────────────────────────────────────────

    private static void ParseLegend(XElement? legendEl, ChartLegend legend)
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

    private static List<string> ReadStringPoints(XElement containerEl)
    {
        var results = new List<string>();

        // Try literal first
        var strLit = containerEl.Element(CmlNames.StringLiteral);
        if (strLit != null)
        {
            foreach (var pt in strLit.Elements(CmlNames.Point))
                results.Add(pt.Element(CmlNames.PointValue)?.Value ?? string.Empty);
            return results;
        }

        // Try numeric literal as string (category numbers)
        var numLit = containerEl.Element(CmlNames.NumberLiteral);
        if (numLit != null)
        {
            foreach (var pt in numLit.Elements(CmlNames.Point))
                results.Add(pt.Element(CmlNames.PointValue)?.Value ?? string.Empty);
            return results;
        }

        // Try workbook-linked cached strings
        var strCache = containerEl.Element(CmlNames.StringReference)?.Element(CmlNames.StringCache);
        if (strCache != null)
        {
            foreach (var pt in strCache.Elements(CmlNames.Point))
                results.Add(pt.Element(CmlNames.PointValue)?.Value ?? string.Empty);
            return results;
        }

        // Try workbook-linked cached numbers
        var numCache = containerEl.Element(CmlNames.NumberReference)?.Element(CmlNames.NumberCache);
        if (numCache != null)
        {
            foreach (var pt in numCache.Elements(CmlNames.Point))
                results.Add(pt.Element(CmlNames.PointValue)?.Value ?? string.Empty);
        }

        return results;
    }

    private static List<double> ReadNumericPoints(XElement containerEl)
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
