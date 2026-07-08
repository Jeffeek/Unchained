namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>Per-series defaults for bubble charts.</summary>
public class PlotOptionsBubble : IHasAdditionalProperties
{
    /// <summary>Minimum bubble size in pixels.</summary>
    public double? MinSize { get; set; }

    /// <summary>Maximum bubble size in pixels.</summary>
    public double? MaxSize { get; set; }

    /// <summary>Additional properties not covered by the typed API.</summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }

    Dictionary<string, object>? IHasAdditionalProperties.GetAdditionalProperties() => AdditionalProperties;
}
