namespace Unchained.Pptx.Charts;

/// <summary>
/// The data model for a chart: one shared category axis and one or more series.
/// </summary>
public sealed class ChartData
{
    /// <summary>
    /// Category labels shared across all series.
    /// For pie/doughnut charts, each label corresponds to one slice.
    /// </summary>
    public List<string> Categories { get; } = [];

    /// <summary>
    /// The data series. Each series contains one value per category.
    /// At least one series is required to produce a valid chart.
    /// </summary>
    public List<ChartSeries> Series { get; } = [];
}
