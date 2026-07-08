namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>Per-series defaults for pie charts.</summary>
public class PlotOptionsPie : IHasAdditionalProperties
{
    /// <summary>Whether slices are pulled apart (exploded) on hover.</summary>
    public bool AllowPointSelect { get; set; } = true;

    /// <summary>Mouse cursor style.</summary>
    public string? Cursor { get; set; }

    /// <summary>Show percentage in labels.</summary>
    public bool ShowInLegend { get; set; }

    /// <summary>Series states (hover, select, inactive).</summary>
    public PlotOptionsStates? States { get; set; }

    /// <summary>Data labels for this series.</summary>
    public PlotOptionsDataLabels? DataLabels { get; set; }

    /// <summary>Pie chart size in pixels.</summary>
    public double? Size { get; set; }

    /// <summary>Inner pie size in pixels (for donut charts).</summary>
    public double? InnerSize { get; set; }

    /// <summary>Border width in pixels.</summary>
    public double? BorderWidth { get; set; }

    /// <summary>Center coordinates [x, y].</summary>
    public List<double>? Center { get; set; }

    /// <summary>Additional properties not covered by the typed API.</summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }

    Dictionary<string, object>? IHasAdditionalProperties.GetAdditionalProperties() => AdditionalProperties;
}
