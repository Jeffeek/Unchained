namespace Unchained.Ooxml.Charts;

/// <summary>
///     Identifies the visual type of an OOXML chart (§21.2).
/// </summary>
public enum ChartType
{
    // ── Bar / Column ────────────────────────────────────────────────────────

    /// <summary>Vertical columns, grouped side by side. Default type.</summary>
    ColumnClustered,
    /// <summary>Vertical columns stacked on top of each other.</summary>
    ColumnStacked,
    /// <summary>Vertical columns stacked to 100%.</summary>
    ColumnFullStacked,

    /// <summary>Horizontal bars, grouped side by side.</summary>
    BarClustered,
    /// <summary>Horizontal bars stacked.</summary>
    BarStacked,
    /// <summary>Horizontal bars stacked to 100%.</summary>
    BarFullStacked,

    // ── Line ────────────────────────────────────────────────────────────────

    /// <summary>Line chart without data point markers.</summary>
    Line,
    /// <summary>Line chart with stacked series.</summary>
    LineStacked,
    /// <summary>Line chart stacked to 100%.</summary>
    LineFullStacked,

    /// <summary>Line chart with data point markers.</summary>
    LineWithMarkers,
    /// <summary>Stacked line chart with markers.</summary>
    LineWithMarkersStacked,
    /// <summary>100%-stacked line chart with markers.</summary>
    LineWithMarkersFullStacked,

    // ── Pie / Doughnut ───────────────────────────────────────────────────────

    /// <summary>Standard pie chart.</summary>
    Pie,
    /// <summary>Pie chart with slices pulled apart.</summary>
    PieExploded,

    /// <summary>Doughnut chart.</summary>
    Doughnut,
    /// <summary>Doughnut chart with segments pulled apart.</summary>
    DoughnutExploded,

    // ── Area ─────────────────────────────────────────────────────────────────

    /// <summary>Area chart with overlapping fills.</summary>
    Area,
    /// <summary>Stacked area chart.</summary>
    AreaStacked,
    /// <summary>100%-stacked area chart.</summary>
    AreaFullStacked,

    // ── Scatter ──────────────────────────────────────────────────────────────

    /// <summary>Scatter chart showing only data point markers.</summary>
    ScatterWithMarkersOnly,
    /// <summary>Scatter chart with straight lines between data points.</summary>
    ScatterWithStraightLines,
    /// <summary>Scatter chart with smooth curves between data points.</summary>
    ScatterWithSmoothLines,
    /// <summary>Scatter chart with straight lines and data point markers.</summary>
    ScatterWithStraightLinesAndMarkers,
    /// <summary>Scatter chart with smooth curves and data point markers.</summary>
    ScatterWithSmoothLinesAndMarkers,

    // ── Bubble ───────────────────────────────────────────────────────────────

    /// <summary>Bubble chart (scatter with a third size dimension).</summary>
    Bubble,

    // ── Radar ────────────────────────────────────────────────────────────────

    /// <summary>Radar (spider) chart without markers.</summary>
    Radar,
    /// <summary>Radar chart with data point markers.</summary>
    RadarWithMarkers,
    /// <summary>Filled radar chart.</summary>
    RadarFilled
}
