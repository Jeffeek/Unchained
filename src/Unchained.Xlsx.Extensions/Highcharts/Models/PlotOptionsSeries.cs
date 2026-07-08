namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>Per-series defaults within <see cref="PlotOptions" />.</summary>
public class PlotOptionsSeries : IHasAdditionalProperties
{
    /// <summary>
    ///     Stacking mode: <c>null</c> = no stacking, <c>"normal"</c> = stacked,
    ///     <c>"percent"</c> = 100% stacked.
    /// </summary>
    public string? Stacking { get; set; }

    /// <summary>Mouse cursor style.</summary>
    public string? Cursor { get; set; }

    /// <summary>Whether to show this series in the legend.</summary>
    public bool? ShowInLegend { get; set; }

    /// <summary>Series states (hover, select, inactive).</summary>
    public PlotOptionsStates? States { get; set; }

    /// <summary>Data labels for this series.</summary>
    public PlotOptionsDataLabels? DataLabels { get; set; }

    /// <summary>Marker configuration for line-type series.</summary>
    public PlotOptionsMarker? Marker { get; set; }

    /// <summary>Additional properties not covered by the typed API.</summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }

    Dictionary<string, object>? IHasAdditionalProperties.GetAdditionalProperties() => AdditionalProperties;
}
