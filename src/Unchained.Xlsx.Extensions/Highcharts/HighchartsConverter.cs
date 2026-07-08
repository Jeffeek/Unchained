using System.Text.Json;
using System.Text.Json.Serialization;
using Unchained.Drawing.Primitives;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Drawing;
using Unchained.Xlsx.Drawings;
using Unchained.Xlsx.Extensions.Highcharts.Models;

namespace Unchained.Xlsx.Extensions.Highcharts;

/// <summary>
///     Maps an <see cref="ChartDrawing" /> to <see cref="HighchartsOptions" />.
/// </summary>
public class HighchartsConverter
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    ///     Converts the given chart drawing into a Highcharts-compatible options model.
    ///     Formula values are read from the chart's underlying data (call
    ///     <c>chart.Workbook?.Recalculate()</c> first to force a recalculation pass).
    /// </summary>
    /// <param name="excelChart">The chart drawing to convert.</param>
    /// <returns>A <see cref="HighchartsOptions" /> ready for JSON serialisation.</returns>
    public HighchartsOptions Convert(ChartDrawing excelChart)
    {
        var model = excelChart.Chart;
        var seriesType = MapChartType(model.Type);

        var options = new HighchartsOptions
        {
            Series = [],
            Chart = new ChartConfig
            {
                Type = seriesType,
                SpacingTop = 15,
                SpacingBottom = 10,
                SpacingLeft = 10,
                SpacingRight = 10
            }
        };

        PopulateTitle(model, options);
        PopulateAxes(model, options);
        PopulateSeries(model, options);
        PopulatePlotOptions(model, options);
        PopulateLegend(model, options);
        PopulateTooltip(model, options);
        PopulateColors(model, options);

        return options;
    }

    // ── Chart type mapping ────────────────────────────────────────────────

    /// <summary>Maps Excel chart types to their Highcharts string equivalents.</summary>
    internal static string MapChartType(ChartType type) =>
        type switch
        {
            // Column charts
            ChartType.ColumnClustered => HighchartsStrings.ChartColumn,
            // ReSharper disable DuplicatedSwitchExpressionArms
            ChartType.ColumnStacked => HighchartsStrings.ChartColumn,
            ChartType.ColumnFullStacked => HighchartsStrings.ChartColumn,

            // Bar charts (horizontal)
            ChartType.BarClustered => HighchartsStrings.ChartBar,
            ChartType.BarStacked => HighchartsStrings.ChartBar,
            ChartType.BarFullStacked => HighchartsStrings.ChartBar,

            // Line charts
            ChartType.Line => HighchartsStrings.ChartLine,
            ChartType.LineStacked => HighchartsStrings.ChartLine,
            ChartType.LineFullStacked => HighchartsStrings.ChartLine,
            ChartType.LineWithMarkers => HighchartsStrings.ChartLine,
            ChartType.LineWithMarkersStacked => HighchartsStrings.ChartLine,
            ChartType.LineWithMarkersFullStacked => HighchartsStrings.ChartLine,

            // Pie / Doughnut
            ChartType.Pie => HighchartsStrings.ChartPie,
            ChartType.PieExploded => HighchartsStrings.ChartPie,
            ChartType.Doughnut => HighchartsStrings.ChartDoughnut,
            ChartType.DoughnutExploded => HighchartsStrings.ChartDoughnut,

            // Area charts
            ChartType.Area => HighchartsStrings.ChartArea,
            ChartType.AreaStacked => HighchartsStrings.ChartArea,
            ChartType.AreaFullStacked => HighchartsStrings.ChartArea,

            // Scatter
            ChartType.ScatterWithMarkersOnly => HighchartsStrings.ChartScatter,
            ChartType.ScatterWithStraightLines => HighchartsStrings.ChartScatter,
            ChartType.ScatterWithSmoothLines => HighchartsStrings.ChartScatter,
            ChartType.ScatterWithStraightLinesAndMarkers => HighchartsStrings.ChartScatter,
            ChartType.ScatterWithSmoothLinesAndMarkers => HighchartsStrings.ChartScatter,

            // Bubble
            ChartType.Bubble => HighchartsStrings.ChartBubble,

            // Radar (rendered as polar line in Highcharts)
            ChartType.Radar => HighchartsStrings.ChartRadar,
            ChartType.RadarWithMarkers => HighchartsStrings.ChartRadar,
            ChartType.RadarFilled => HighchartsStrings.ChartRadar,

            _ => HighchartsStrings.ChartLine
            // ReSharper restore DuplicatedSwitchExpressionArms
        };

    /// <summary>Extracts a solid fill colour from a series.</summary>
    private static string? ExtractColor(FillFormat? fill)
    {
        if (fill is null || fill.Type != FillType.Solid || fill.Solid is null)
            return null;

        var argb = fill.Solid.Color.Resolve(null!);
        var (_, r, g, b) = ColorMath.UnpackArgb(argb);

        return $"#{r:X2}{g:X2}{b:X2}";
    }

    /// <summary>Detects whether the chart has a secondary axis by inspecting series data labels.</summary>
    private static bool DetectSecondaryAxis(ChartModel model) =>
        model.Data.Series.Any(static s => s.DataLabels?.Position == "secondary");

    // ── Populate helpers ──────────────────────────────────────────────────

    private static void PopulateTitle(ChartModel model, HighchartsOptions options) =>
        options.Title = new TitleConfig
        {
            Text = string.IsNullOrEmpty(model.Title) ? "Untitled Chart" : model.Title
        };

    private static void PopulateAxes(ChartModel model, HighchartsOptions options)
    {
        var hasCategories = model.Data.Categories.Count > 0;
        var catAxis = model.CategoryAxis;
        var isDatetime = catAxis.NumberFormat is not null &&
                         (catAxis.NumberFormat.Contains("yyyy")
                          || catAxis.NumberFormat.Contains("mm/dd")
                          || catAxis.NumberFormat.Contains("dd/mm"));

        var labelsRotation = catAxis.Position switch { "r" => 45, "b" => 0, _ => (int?)null };

        options.XAxis = new AxisConfig
        {
            Categories = hasCategories ? [.. model.Data.Categories] : null,
            Type = isDatetime ? "datetime" : null,
            Title = string.IsNullOrEmpty(catAxis.Title) ? string.Empty : catAxis.Title,
            Min = catAxis.Minimum,
            Max = catAxis.Maximum,
            MinGridline = catAxis.Minimum,
            MaxGridline = catAxis.Maximum,
            LineColor = "#000",
            TickColor = "#000",
            LineWidth = 1,
            TickLength = 8,
            TickWidth = 1,
            MinorTickLength = 8,
            MinorTickWidth = 1,
            MinorTickColor = "#000",
            MinPadding = 0,
            MaxPadding = 0,
            StartOnTick = false,
            EndOnTick = null,
            MinorTickInterval = null,
            ScrollableAxisLabel = null,
            Labels = new LabelConfig
            {
                Format = catAxis.NumberFormat,
                Rotation = labelsRotation,
                Style = new StyleConfig
                {
                    Color = "#000",
                    FontFamily = "sans-serif",
                    FontSize = "12px",
                    FontWeight = "400",
                    LineHeight = "14px"
                }
            },
            PlotLines = null,
            Scrollbar = null
        };

        options.YAxis = [];

        var valAxis = model.ValueAxis;
        options.YAxis.Add(
            new YAxisConfig
            {
                Title = string.IsNullOrEmpty(valAxis.Title) ? null : valAxis.Title,
                Min = valAxis.Minimum,
                Max = valAxis.Maximum
            }
        );

        var hasSecondary = DetectSecondaryAxis(model);
        if (hasSecondary)
            options.YAxis.Add(new YAxisConfig { Index = 1, Opposite = true });
    }

    private static void PopulateSeries(ChartModel model, HighchartsOptions options)
    {
        var hasSecondary = DetectSecondaryAxis(model);
        var isBubble = model.Type == ChartType.Bubble;
        var isScatter = model.Type is >= ChartType.ScatterWithMarkersOnly and <= ChartType.ScatterWithSmoothLinesAndMarkers;
        var xCount = model.Data.Series.Count > 0 ? model.Data.Series[0].XValues.Count : 0;

        foreach (var series in model.Data.Series)
        {
            var cfg = new SeriesConfig
            {
                Name = string.IsNullOrEmpty(series.Name) ? "Series" : series.Name,
                Type = MapChartType(model.Type),
                Color = ExtractColor(series.Fill),
                YAxis = hasSecondary && series.DataLabels?.Position == "secondary" ? 1 : 0
            };

            for (var i = 0; i < series.Values.Count; i++)
            {
                var value = series.Values[i];
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    cfg.Data.Add(null);
                    continue;
                }

                if (isBubble || isScatter)
                {
                    var dp = new DataPoint { Y = value, X = isBubble ? null : i < xCount ? series.XValues[i] : null };
                    if (isBubble && i < xCount)
                        dp.Size = series.XValues[i];
                    cfg.DataPoints.Add(dp);
                }
                else
                    cfg.Data.Add(value);
            }

            options.Series.Add(cfg);
        }
    }

    private static void PopulatePlotOptions(ChartModel model, HighchartsOptions options)
    {
        var stacking = model.Type switch
        {
            ChartType.ColumnStacked or ChartType.BarStacked or ChartType.LineStacked
                or ChartType.LineWithMarkersStacked or ChartType.AreaStacked => "normal",
            ChartType.ColumnFullStacked or ChartType.BarFullStacked or ChartType.LineFullStacked
                or ChartType.LineWithMarkersFullStacked or ChartType.AreaFullStacked => "percent",
            _ => null
        };

        var baseOpts = new PlotOptionsSeries
        {
            Stacking = stacking,
            Cursor = "pointer",
            ShowInLegend = true,
            States = new()
            {
                Hover = new() { Enabled = true },
                Select = new() { Enabled = true },
                Inactive = new() { Enabled = true }
            },
            DataLabels = new() { Enabled = false }
        };

        options.PlotOptions = new PlotOptions
        {
            Series = baseOpts,
            Area = model.Type is ChartType.Area or ChartType.AreaStacked or ChartType.AreaFullStacked
                ? new PlotOptionsArea { Stacking = stacking, Cursor = "pointer", ShowInLegend = true, States = baseOpts.States, DataLabels = baseOpts.DataLabels }
                : null,
            Bar = model.Type is ChartType.BarClustered or ChartType.BarStacked or ChartType.BarFullStacked
                ? new PlotOptionsBar { Stacking = stacking, Cursor = "pointer", ShowInLegend = true, States = baseOpts.States, DataLabels = baseOpts.DataLabels }
                : null,
            Line = model.Type is ChartType.Line or ChartType.LineStacked or ChartType.LineFullStacked
                or ChartType.LineWithMarkers or ChartType.LineWithMarkersStacked or ChartType.LineWithMarkersFullStacked
                ? new PlotOptionsLine { Stacking = stacking, Cursor = "pointer", ShowInLegend = true, States = baseOpts.States, DataLabels = baseOpts.DataLabels }
                : null,
            Pie = model.Type is ChartType.Pie or ChartType.PieExploded
                ? new PlotOptionsPie { AllowPointSelect = true, ShowInLegend = true, States = baseOpts.States, DataLabels = baseOpts.DataLabels }
                : null,
            Bubble = model.Type == ChartType.Bubble ? new PlotOptionsBubble() : null,
            Radar = model.Type is ChartType.Radar or ChartType.RadarWithMarkers or ChartType.RadarFilled
                ? new PlotOptionsRadar { Cursor = "pointer", ShowInLegend = true, States = baseOpts.States, DataLabels = baseOpts.DataLabels }
                : null,
            Scatter = model.Type is >= ChartType.ScatterWithMarkersOnly and <= ChartType.ScatterWithSmoothLinesAndMarkers
                ? new PlotOptionsScatter { Cursor = "pointer", ShowInLegend = true, States = baseOpts.States, DataLabels = baseOpts.DataLabels }
                : null
        };
    }

    private static void PopulateLegend(ChartModel model, HighchartsOptions options)
    {
        var leg = model.Legend;

        options.Legend = new LegendConfig
        {
            Enabled = leg.IsVisible,
            Align = leg.Position switch
            {
                ChartLegendPosition.Left => HighchartsStrings.LegAlignLeft,
                ChartLegendPosition.Right => HighchartsStrings.LegAlignRight,
                ChartLegendPosition.Top => HighchartsStrings.LegAlignCenter,
                ChartLegendPosition.TopRight => HighchartsStrings.LegAlignRight,
                _ => HighchartsStrings.LegAlignCenter
            },
            VerticalAlign = leg.Position switch
            {
                ChartLegendPosition.Top => HighchartsStrings.LegVertTop,
                ChartLegendPosition.Bottom => HighchartsStrings.LegVertBottom,
                ChartLegendPosition.Left or ChartLegendPosition.Right or ChartLegendPosition.TopRight => HighchartsStrings.LegVertMiddle,
                _ => null
            },
            LayoutAlign = leg.Position switch
            {
                ChartLegendPosition.Left or ChartLegendPosition.Right => HighchartsStrings.LegLayoutVertical,
                _ => HighchartsStrings.LegLayoutHorizontal
            },
            Floating = leg.IsOverlay
        };
    }

    private static void PopulateTooltip(ChartModel model, HighchartsOptions options)
    {
        var dataLabels = model.DataLabels;

        options.Tooltip = new Tooltip
        {
            Crosshairs = true,
            Snap = true,
            HeaderFormat = dataLabels.ShowSeriesName ? "<b>{series.name}</b><br/>" : null,
            PointFormat = dataLabels.ShowValue ? "{point.y}" : null,
            Shared = true,
            Outside = true,
            Formatter = null,
            Style = new StyleConfig
            {
                Color = "#000",
                FontFamily = "sans-serif",
                FontSize = "14px",
                FontWeight = "400",
                LineHeight = "18px",
                ZIndex = 1000
            }
        };
    }

    private static void PopulateColors(ChartModel model, HighchartsOptions options)
    {
        var colors = model.Data.Series
            .Select(static s => ExtractColor(s.Fill))
            .Where(static c => c is not null)
            .Cast<string>()
            .ToList();

        // Only override Highcharts' built-in palette when the workbook actually
        // defines explicit series colours; otherwise leave it null so Highcharts
        // applies its own defaults.
        options.Colors = colors.Count > 0 ? colors : null;
    }
}
