namespace Unchained.Pptx.Charts;

/// <summary>
/// One data series in a chart — a named sequence of numeric values.
/// </summary>
public sealed class ChartSeries
{
    /// <summary>Display name of the series shown in the legend and tooltips.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Numeric data values, one per category. The list may be empty when the series
    /// is created; populate it before saving.
    /// </summary>
    public List<double> Values { get; } = [];
}
