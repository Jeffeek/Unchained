namespace Unchained.Ooxml.Charts;

/// <summary>
/// Controls the visibility and placement of the chart legend.
/// </summary>
public sealed class ChartLegend
{
    /// <summary>Whether the legend is rendered. Default: <see langword="true"/>.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Where the legend appears relative to the plot area.
    /// Default: <see cref="ChartLegendPosition.Bottom"/>.
    /// </summary>
    public ChartLegendPosition Position { get; set; } = ChartLegendPosition.Bottom;

    /// <summary>
    /// When <see langword="true"/>, the legend overlays the plot area rather than
    /// resizing it.
    /// </summary>
    public bool IsOverlay { get; set; }
}
