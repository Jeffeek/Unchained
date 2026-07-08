namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     A drilldown series linked to a parent series.
/// </summary>
public class DrilldownSeries
{
    /// <summary>The name of the parent series to link to.</summary>
    public string? ParentSeriesName { get; set; }

    /// <summary>The name of the child worksheet containing drilldown data.</summary>
    public string? ChildSheetName { get; set; }

    /// <summary>The title shown when drilling into this series.</summary>
    public string? Title { get; set; }

    /// <summary>Data series for the drilldown level.</summary>
    public List<SeriesConfig> Data { get; set; } = new();
}
