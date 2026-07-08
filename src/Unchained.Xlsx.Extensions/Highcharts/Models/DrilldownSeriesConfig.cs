namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Configuration for a drilldown series on a chart.
///     Drilldown is a Highcharts concept — OpenXML charts have no native drilldown support.
///     This type exists solely for the Xlsx.Extensions drilldown auto-detection pipeline.
/// </summary>
public class DrilldownSeriesConfig
{
    /// <summary>The name of the parent series to link to.</summary>
    public string? ParentSeriesName { get; set; }

    /// <summary>The name of the child worksheet containing drilldown data.</summary>
    public string? ChildSheetName { get; set; }

    /// <summary>The title shown when drilling into this series.</summary>
    public string? Title { get; set; }
}
