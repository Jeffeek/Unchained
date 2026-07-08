namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Global plot options applied to all series.
/// </summary>
public class PlotOptions : IHasAdditionalProperties
{
    /// <summary>Per-series defaults.</summary>
    public PlotOptionsSeries? Series { get; set; }

    /// <summary>Area chart defaults.</summary>
    public PlotOptionsArea? Area { get; set; }

    /// <summary>Bar chart defaults.</summary>
    public PlotOptionsBar? Bar { get; set; }

    /// <summary>Line chart defaults.</summary>
    public PlotOptionsLine? Line { get; set; }

    /// <summary>Pie chart defaults.</summary>
    public PlotOptionsPie? Pie { get; set; }

    /// <summary>Bubble chart defaults.</summary>
    public PlotOptionsBubble? Bubble { get; set; }

    /// <summary>Radar chart defaults.</summary>
    public PlotOptionsRadar? Radar { get; set; }

    /// <summary>Scatter chart defaults.</summary>
    public PlotOptionsScatter? Scatter { get; set; }

    /// <summary>Additional properties not covered by the typed API.</summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }

    Dictionary<string, object>? IHasAdditionalProperties.GetAdditionalProperties() => AdditionalProperties;
}
