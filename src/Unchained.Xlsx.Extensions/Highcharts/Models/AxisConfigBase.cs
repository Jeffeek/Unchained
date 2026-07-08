namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Shared configuration common to both category/X axes (<see cref="AxisConfig" />) and
///     value/Y axes (<see cref="YAxisConfig" />). Holds the styling and scaling properties that
///     Highcharts treats identically across axis kinds.
/// </summary>
public abstract class AxisConfigBase : IHasAdditionalProperties
{
    /// <summary>Axis type: "category", "datetime", "linear", "logarithmic".</summary>
    public string? Type { get; set; }

    /// <summary>Axis title text.</summary>
    public string? Title { get; set; }

    /// <summary>Minimum axis value.</summary>
    public double? Min { get; set; }

    /// <summary>Maximum axis value.</summary>
    public double? Max { get; set; }

    /// <summary>Minimum gridline value.</summary>
    public double? MinGridline { get; set; }

    /// <summary>Maximum gridline value.</summary>
    public double? MaxGridline { get; set; }

    /// <summary>Axis line color.</summary>
    public string? LineColor { get; set; }

    /// <summary>Axis line width in pixels.</summary>
    public double? LineWidth { get; set; }

    /// <summary>Tick color.</summary>
    public string? TickColor { get; set; }

    /// <summary>Tick length in pixels.</summary>
    public double? TickLength { get; set; }

    /// <summary>Tick width in pixels.</summary>
    public double? TickWidth { get; set; }

    /// <summary>Minor tick length in pixels.</summary>
    public double? MinorTickLength { get; set; }

    /// <summary>Minor tick width in pixels.</summary>
    public double? MinorTickWidth { get; set; }

    /// <summary>Minor tick color.</summary>
    public string? MinorTickColor { get; set; }

    /// <summary>Minimum padding (fraction of axis range).</summary>
    public double? MinPadding { get; set; }

    /// <summary>Maximum padding (fraction of axis range).</summary>
    public double? MaxPadding { get; set; }

    /// <summary>Whether the axis starts on a tick mark.</summary>
    public bool? StartOnTick { get; set; }

    /// <summary>Whether the axis ends on a tick mark.</summary>
    public bool? EndOnTick { get; set; }

    /// <summary>Minor tick interval.</summary>
    public double? MinorTickInterval { get; set; }

    /// <summary>Labels configuration.</summary>
    public LabelConfig? Labels { get; set; }

    /// <summary>Plot lines on this axis.</summary>
    public List<PlotLine>? PlotLines { get; set; }

    /// <summary>Whether axis labels are scrollable.</summary>
    public bool? ScrollableAxisLabel { get; set; }

    /// <summary>Scrollbar configuration.</summary>
    public ScrollbarConfig? Scrollbar { get; set; }

    /// <summary>Additional properties not covered by the typed API.</summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }

    Dictionary<string, object>? IHasAdditionalProperties.GetAdditionalProperties() => AdditionalProperties;
}
