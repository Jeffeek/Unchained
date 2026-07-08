namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Chart configuration (type, colors, plot options, etc.).
/// </summary>
public class ChartConfig : IHasAdditionalProperties
{
    /// <summary>The chart type: "column", "line", "bar", "pie", etc.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Background color of the plot area.</summary>
    public string? PlotBackgroundColor { get; set; }

    /// <summary>Width of the plot area border in pixels.</summary>
    public double? PlotBorderWidth { get; set; }

    /// <summary>Whether the plot area is rendered as a shadow.</summary>
    public bool PlotShadow { get; set; }

    /// <summary>Spacing between the chart and the plot area (top, bottom, left, right).</summary>
    public double? SpacingTop { get; set; }

    public double? SpacingBottom { get; set; }
    public double? SpacingLeft { get; set; }
    public double? SpacingRight { get; set; }

    /// <summary>Overall chart background color.</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>Chart border radius in pixels.</summary>
    public double? BorderRadius { get; set; }

    /// <summary>Chart border width in pixels.</summary>
    public double? BorderWidth { get; set; }

    /// <summary>Chart margin from top in pixels.</summary>
    public double? MarginTop { get; set; }

    /// <summary>Chart margin from left in pixels.</summary>
    public double? MarginLeft { get; set; }

    /// <summary>Chart padding in pixels.</summary>
    public double? Padding { get; set; }

    /// <summary>Maximum chart width in pixels.</summary>
    public int? MaxWidth { get; set; }

    /// <summary>Chart height in pixels.</summary>
    public int? Height { get; set; }

    /// <summary>Chart width in pixels.</summary>
    public int? Width { get; set; }

    /// <summary>Overall chart style (font family, size, color, etc.).</summary>
    public StyleConfig? Style { get; set; }

    /// <summary>Additional properties not covered by the typed API.</summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }

    Dictionary<string, object>? IHasAdditionalProperties.GetAdditionalProperties() => AdditionalProperties;
}
