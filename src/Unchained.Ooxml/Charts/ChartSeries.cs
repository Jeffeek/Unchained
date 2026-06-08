namespace Unchained.Ooxml.Charts;

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

    /// <summary>
    /// Explicit X-axis values for scatter and bubble charts, one per <see cref="Values"/> entry.
    /// Ignored for category-based chart types (column, bar, line, pie, etc.), which use
    /// <see cref="ChartData.Categories"/> instead. When empty, scatter/bubble charts fall back
    /// to sequential indices (1, 2, 3, …).
    /// </summary>
    public List<double> XValues { get; } = [];
}
