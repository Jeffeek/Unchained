using Unchained.Drawing;
using Unchained.Ooxml.Charts;
using Unchained.Pptx.Shapes;

namespace Unchained.Pptx.Rendering.Engine;

// Chart rendering: plot-area dispatch (pie/line/bar), axes, gridlines, tick/category labels
// and legend. Uses the shared SeriesPalette and the text pipeline for labels.
internal sealed partial class SlideRasterizer
{
    private void RenderChart(
        RasterBuffer buffer,
        ChartShape shape,
        int x,
        int y,
        int width,
        int height,
        double dpi
    )
    {
        buffer.FillRect(x,
            y,
            width,
            height,
            255,
            255,
            255);
        DrawBorder(buffer,
            x,
            y,
            width,
            height,
            180,
            180,
            180);

        var model = shape.Chart;
        if (model.Data.Series.Count == 0) return;

        var titleH = 0;
        if (model.HasTitle && !string.IsNullOrWhiteSpace(model.Title))
        {
            RenderTextFrameText(buffer,
                model.Title,
                x + 6,
                y + 4,
                width - 12,
                14.0,
                dpi,
                60,
                60,
                60);
            titleH = (int)(18 * dpi / RenderingConstants.PointsPerInch);
        }

        // Reserve margins for axes: left for value labels, bottom for category labels.
        const int axisLeft = RenderingConstants.ChartAxisMarginLeft;
        const int axisBottom = RenderingConstants.ChartAxisMarginBottom;
        var plotX = x + axisLeft;
        var plotY = y + titleH + 6;
        var plotW = width - axisLeft - 8;
        var plotH = height - titleH - axisBottom - 8;
        if (plotW <= 4 || plotH <= 4) return;

        var series = model.Data.Series;
        var type = model.Type.ToString();
        if (type.StartsWith("Pie", StringComparison.Ordinal) || type.StartsWith("Doughnut", StringComparison.Ordinal))
        {
            RenderPieChart(buffer,
                series[0],
                plotX,
                plotY,
                plotW,
                plotH);
        }
        else if (type.StartsWith("Line", StringComparison.Ordinal) || type.StartsWith("Scatter", StringComparison.Ordinal))
        {
            var maxVal = series.SelectMany(static s => s.Values).DefaultIfEmpty(1).Max();
            var minVal = Math.Min(0.0, series.SelectMany(static s => s.Values).DefaultIfEmpty(0).Min());
            RenderChartAxes(buffer,
                plotX,
                plotY,
                plotW,
                plotH,
                minVal,
                maxVal,
                dpi,
                model.Data.Categories,
                false);
            RenderLineChart(buffer,
                series,
                plotX,
                plotY,
                plotW,
                plotH);
        }
        else
        {
            var isHorizontal = type.StartsWith("Bar", StringComparison.Ordinal);
            var maxVal = series.SelectMany(static s => s.Values).DefaultIfEmpty(1).Max();
            var minVal = Math.Min(0.0, series.SelectMany(static s => s.Values).DefaultIfEmpty(0).Min());
            RenderChartAxes(buffer,
                plotX,
                plotY,
                plotW,
                plotH,
                minVal,
                maxVal,
                dpi,
                model.Data.Categories,
                isHorizontal);
            RenderBarChart(buffer,
                series,
                plotX,
                plotY,
                plotW,
                plotH,
                isHorizontal);
        }

        // Legend
        if (model.Legend.IsVisible && series.Count > 0)
        {
            RenderChartLegend(buffer,
                series,
                x,
                y + height - 14,
                width,
                dpi);
        }
    }

    // Draws axis lines, grid lines, value tick labels, and category labels.
    private void RenderChartAxes(
        RasterBuffer buffer,
        int plotX,
        int plotY,
        int plotW,
        int plotH,
        double minVal,
        double maxVal,
        double dpi,
        IReadOnlyList<string> categories,
        bool horizontal
    )
    {
        if (Math.Abs(maxVal - minVal) < 0.0001) maxVal = minVal + 1;
        var range = maxVal - minVal;

        // Value axis: 4 evenly spaced ticks.
        const int tickCount = 4;
        for (var t = 0; t <= tickCount; t++)
        {
            var frac = (double)t / tickCount;
            var val = minVal + (range * frac);
            var label = FormatAxisValue(val);

            if (!horizontal)
            {
                var gy = plotY + plotH - (int)(frac * plotH);
                // Gridline
                buffer.FillRect(plotX,
                    gy,
                    plotW,
                    1,
                    230,
                    230,
                    230);
                // Tick label
                RenderTextFrameText(buffer,
                    label,
                    plotX - 38,
                    gy - 6,
                    36,
                    8.0,
                    dpi,
                    100,
                    100,
                    100);
            }
            else
            {
                var gx = plotX + (int)(frac * plotW);
                buffer.FillRect(gx,
                    plotY,
                    1,
                    plotH,
                    230,
                    230,
                    230);
                RenderTextFrameText(buffer,
                    label,
                    gx - 12,
                    plotY + plotH + 2,
                    36,
                    8.0,
                    dpi,
                    100,
                    100,
                    100);
            }
        }

        // Axis lines
        buffer.FillRect(plotX,
            plotY,
            1,
            plotH,
            160,
            160,
            160);
        buffer.FillRect(plotX,
            plotY + plotH,
            plotW,
            1,
            160,
            160,
            160);

        // Category labels (up to 8 to avoid crowding).
        // ReSharper disable once InvertIf
        if (categories.Count > 0)
        {
            var maxLabels = Math.Min(8, categories.Count);
            var step = Math.Max(1, categories.Count / maxLabels);
            for (var ci = 0; ci < categories.Count; ci += step)
            {
                var label = TruncateLabel(categories[ci], 8);
                if (!horizontal)
                {
                    var lx = plotX + (int)((ci + 0.5) / categories.Count * plotW) - 12;
                    RenderTextFrameText(buffer,
                        label,
                        lx,
                        plotY + plotH + 2,
                        28,
                        7.0,
                        dpi,
                        80,
                        80,
                        80);
                }
                else
                {
                    var ly = plotY + (int)((ci + 0.5) / categories.Count * plotH) - 5;
                    RenderTextFrameText(buffer,
                        label,
                        plotX - 38,
                        ly,
                        36,
                        7.0,
                        dpi,
                        80,
                        80,
                        80);
                }
            }
        }
    }

    // Draws a small legend strip at the bottom of the chart.
    private void RenderChartLegend(
        RasterBuffer buffer,
        IReadOnlyList<ChartSeries> series,
        int x,
        int y,
        int width,
        double dpi
    )
    {
        const int swatchSize = 8;
        var cursorX = x + 8;
        for (var si = 0; si < Math.Min(series.Count, 6); si++)
        {
            var color = SeriesPalette[si % SeriesPalette.Length];
            buffer.FillRect(cursorX,
                y + 3,
                swatchSize,
                swatchSize,
                color.R,
                color.G,
                color.B);
            cursorX += swatchSize + 2;
            var name = TruncateLabel(series[si].Name, 10);
            RenderTextFrameText(buffer,
                name,
                cursorX,
                y + 3,
                80,
                8.0,
                dpi,
                60,
                60,
                60);
            cursorX += 90;
            if (cursorX > x + width - 20) break;
        }
    }

    private static string FormatAxisValue(double val) =>
        Math.Abs(val) switch
        {
            >= 1_000_000 => $"{val / 1_000_000:G3}M",
            >= 1_000 => $"{val / 1_000:G3}K",
            _ => Math.Abs(val - Math.Floor(val)) < 0.05 ? ((int)val).ToString() : $"{val:G3}"
        };

    private static string TruncateLabel(string s, int maxChars) =>
        s.Length <= maxChars ? s : s[..maxChars];

    private static void RenderBarChart(
        RasterBuffer buffer,
        IReadOnlyList<ChartSeries> series,
        int x,
        int y,
        int w,
        int h,
        bool horizontal
    )
    {
        var maxVal = series.SelectMany(static s => s.Values).DefaultIfEmpty(0).Max();
        if (maxVal <= 0) maxVal = 1;
        var categories = series.Max(static s => s.Values.Count);
        if (categories == 0) return;

        const int groupGap = 4;
        var groupSpan = (horizontal ? h : w) / categories;
        var barSpan = Math.Max(1, (groupSpan - groupGap) / Math.Max(1, series.Count));

        for (var c = 0; c < categories; c++)
        {
            for (var s = 0; s < series.Count; s++)
            {
                if (c >= series[s].Values.Count) continue;

                var val = series[s].Values[c];
                var color = SeriesPalette[s % SeriesPalette.Length];

                if (horizontal)
                {
                    var barLen = (int)(val / maxVal * w);
                    var by = y + (c * groupSpan) + (s * barSpan);
                    buffer.FillRect(x,
                        by,
                        barLen,
                        barSpan - 1,
                        color.R,
                        color.G,
                        color.B);
                }
                else
                {
                    var barLen = (int)(val / maxVal * h);
                    var bx = x + (c * groupSpan) + (s * barSpan);
                    buffer.FillRect(bx,
                        y + h - barLen,
                        barSpan - 1,
                        barLen,
                        color.R,
                        color.G,
                        color.B);
                }
            }
        }
    }

    private static void RenderLineChart(
        RasterBuffer buffer,
        IReadOnlyList<ChartSeries> series,
        int x,
        int y,
        int w,
        int h
    )
    {
        var maxVal = series.SelectMany(static s => s.Values).DefaultIfEmpty(0).Max();
        if (maxVal <= 0) maxVal = 1;

        for (var s = 0; s < series.Count; s++)
        {
            var vals = series[s].Values;
            if (vals.Count < 2) continue;

            var color = SeriesPalette[s % SeriesPalette.Length];
            var stepX = (double)w / (vals.Count - 1);

            for (var i = 0; i < vals.Count - 1; i++)
            {
                var x0 = x + (int)(i * stepX);
                var x1 = x + (int)((i + 1) * stepX);
                var y0 = y + h - (int)(vals[i] / maxVal * h);
                var y1 = y + h - (int)(vals[i + 1] / maxVal * h);
                buffer.DrawLine(x0,
                    y0,
                    x1,
                    y1,
                    color.R,
                    color.G,
                    color.B,
                    2);
            }
        }
    }

    private static void RenderPieChart(
        RasterBuffer buffer,
        ChartSeries series,
        int x,
        int y,
        int w,
        int h
    )
    {
        var total = series.Values.Sum();
        if (total <= 0) return;

        var cx = x + (w / 2);
        var cy = y + (h / 2);
        var radius = (Math.Min(w, h) / 2) - 2;
        if (radius <= 0) return;

        var bounds = new double[series.Values.Count + 1];
        for (var i = 0; i < series.Values.Count; i++)
            bounds[i + 1] = bounds[i] + (series.Values[i] / total);

        for (var py = -radius; py <= radius; py++)
        for (var px = -radius; px <= radius; px++)
        {
            if ((px * px) + (py * py) > radius * radius) continue;

            var angle = Math.Atan2(py, px);
            var frac = (angle + Math.PI) / (2 * Math.PI);
            var slice = 0;
            for (var i = 0; i < series.Values.Count; i++)
            {
                if (!(frac >= bounds[i]) || !(frac < bounds[i + 1])) continue;

                slice = i;
                break;
            }

            var color = SeriesPalette[slice % SeriesPalette.Length];
            buffer.BlitImagePixel(cx + px, cy + py, color.R, color.G, color.B);
        }
    }
}
