namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Drilldown configuration. Maps parent series to drilldown child series.
/// </summary>
public class Drilldown
{
    /// <summary>
    ///     Active-series style when a series is drilled into.
    /// </summary>
    public DrilldownSeriesStyle? ActiveSeriesStyle { get; set; }

    /// <summary>
    ///     Active-axis style when a series is drilled into.
    /// </summary>
    public DrilldownAxisStyle? ActiveAxisStyle { get; set; }

    /// <summary>
    ///     Drilldown series keyed by parent series name.
    /// </summary>
    public Dictionary<string, List<DrilldownSeries>> Series { get; set; } = new();

    /// <summary>Additional properties not covered by the typed API.</summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }

    public Dictionary<string, object>? GetAdditionalProperties() => AdditionalProperties;
}
