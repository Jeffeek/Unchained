namespace Unchained.Xlsx.Extensions.Highcharts;

/// <summary>
///     Literal values used in Highcharts JSON output — chart types, legend positions, etc.
/// </summary>
internal static class HighchartsStrings
{
    // ── Chart types (Highcharts series.type values) ─────────────────────────

    internal const string ChartBar = "bar";
    internal const string ChartColumn = "column";
    internal const string ChartLine = "line";
    internal const string ChartPie = "pie";
    internal const string ChartDoughnut = "doughnut";
    internal const string ChartArea = "area";
    internal const string ChartScatter = "scatter";
    internal const string ChartBubble = "bubble";
    internal const string ChartRadar = "line"; // Highcharts renders radar as polar line

    // ── Legend alignment ─────────────────────────────────────────────────────

    internal const string LegAlignLeft = "left";
    internal const string LegAlignCenter = "center";
    internal const string LegAlignRight = "right";
    internal const string LegVertTop = "top";
    internal const string LegVertBottom = "bottom";
    internal const string LegVertMiddle = "middle";
    internal const string LegLayoutHorizontal = "horizontal";
    internal const string LegLayoutVertical = "vertical";
}
