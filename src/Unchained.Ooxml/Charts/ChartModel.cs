namespace Unchained.Ooxml.Charts;

/// <summary>
/// Top-level model for a chart embedded in a presentation slide.
/// Exposes the chart type, title, data, and legend.
/// </summary>
public sealed class ChartModel
{
    /// <summary>
    /// The visual type of the chart. Determines which XML element is used in the
    /// chart part (<c>&lt;c:barChart&gt;</c>, <c>&lt;c:lineChart&gt;</c>, etc.).
    /// Default: <see cref="ChartType.ColumnClustered"/>.
    /// </summary>
    public ChartType Type { get; set; } = ChartType.ColumnClustered;

    /// <summary>
    /// The chart title text. Ignored when <see cref="HasTitle"/> is
    /// <see langword="false"/>. Default: empty string (no visible title).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Whether a title element is emitted in the chart XML.
    /// Set to <see langword="false"/> to suppress the title entirely.
    /// Default: <see langword="true"/>.
    /// </summary>
    public bool HasTitle { get; set; } = true;

    /// <summary>
    /// Whether a data table is shown below the chart.
    /// Default: <see langword="false"/>.
    /// </summary>
    public bool HasDataTable { get; set; }

    /// <summary>The series and category data for the chart.</summary>
    public ChartData Data { get; } = new();

    /// <summary>Legend visibility and placement settings.</summary>
    public ChartLegend Legend { get; } = new();
}
