namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Legend configuration.
/// </summary>
public class LegendConfig : IHasAdditionalProperties
{
    /// <summary>Whether the legend is displayed. Default <c>true</c>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Horizontal alignment: <c>"left"</c>, <c>"center"</c>, <c>"right"</c>.
    ///     Maps from the chart's legend position.
    /// </summary>
    public string? Align { get; set; }

    /// <summary>
    ///     Vertical alignment: <c>"top"</c>, <c>"middle"</c>, <c>"bottom"</c>.
    ///     Maps from the chart's legend position.
    /// </summary>
    public string? VerticalAlign { get; set; }

    /// <summary>
    ///     Layout direction: <c>"horizontal"</c> or <c>"vertical"</c>.
    ///     <c>"vertical"</c> is used when the legend is on the left or right.
    /// </summary>
    public string? LayoutAlign { get; set; }

    /// <summary>Legend border color.</summary>
    public string? BorderColor { get; set; }

    /// <summary>Legend border radius in pixels.</summary>
    public double? BorderRadius { get; set; }

    /// <summary>Margin around the legend in pixels.</summary>
    public double? Margin { get; set; }

    /// <summary>Padding inside the legend in pixels.</summary>
    public double? Padding { get; set; }

    /// <summary>Whether the legend floats above the plot area.</summary>
    public bool? Floating { get; set; }

    /// <summary>Distance between legend items in pixels.</summary>
    public double? ItemDistance { get; set; }

    /// <summary>Width of legend items in pixels.</summary>
    public double? ItemWidth { get; set; }

    /// <summary>Margin below each legend item in pixels.</summary>
    public double? ItemMarginBottom { get; set; }

    /// <summary>Legend item style (font family, size, color, etc.).</summary>
    public StyleConfig? ItemStyle { get; set; }

    /// <summary>Format string for legend items (e.g. "{percentage:.0f}% {name}").</summary>
    public string? LabelFormat { get; set; }

    /// <summary>Navigation for scrolling long legends.</summary>
    public LegendNavigation? Navigation { get; set; }

    /// <summary>Additional properties not covered by the typed API.</summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }

    Dictionary<string, object>? IHasAdditionalProperties.GetAdditionalProperties() => AdditionalProperties;
}
