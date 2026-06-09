namespace Unchained.Ooxml.Charts;

/// <summary>
/// Data-label settings for a chart or series (<c>&lt;c:dLbls&gt;</c>): which fields to show and
/// where to place them.
/// </summary>
public sealed class ChartDataLabels
{
    /// <summary>Whether data labels are shown at all.</summary>
    public bool IsVisible { get; set; }

    /// <summary>Show the data point value.</summary>
    public bool ShowValue { get; set; } = true;

    /// <summary>Show the series name.</summary>
    public bool ShowSeriesName { get; set; }

    /// <summary>Show the category name.</summary>
    public bool ShowCategoryName { get; set; }

    /// <summary>Show the percentage (pie/doughnut).</summary>
    public bool ShowPercentage { get; set; }

    /// <summary>Show the legend key swatch next to the label.</summary>
    public bool ShowLegendKey { get; set; }

    /// <summary>
    /// Label position token (e.g. <c>ctr</c>, <c>inEnd</c>, <c>outEnd</c>, <c>bestFit</c>).
    /// <see langword="null"/> = chart default.
    /// </summary>
    public string? Position { get; set; }

    /// <summary>Number format for the label value. <see langword="null"/> = general.</summary>
    public string? NumberFormat { get; set; }
}

/// <summary>
/// A trendline fitted to a series (<c>&lt;c:trendline&gt;</c>): linear, exponential, etc.
/// </summary>
public sealed class ChartTrendline
{
    /// <summary>Trendline type token (e.g. <c>linear</c>, <c>exp</c>, <c>log</c>, <c>poly</c>, <c>power</c>, <c>movingAvg</c>).</summary>
    public string Type { get; set; } = "linear";

    /// <summary>Polynomial order or moving-average period, when applicable.</summary>
    public int? Order { get; set; }

    /// <summary>Show the trendline equation on the chart.</summary>
    public bool DisplayEquation { get; set; }

    /// <summary>Show the R-squared value on the chart.</summary>
    public bool DisplayRSquared { get; set; }

    /// <summary>Forward forecast period.</summary>
    public double? Forward { get; set; }

    /// <summary>Backward forecast period.</summary>
    public double? Backward { get; set; }
}
