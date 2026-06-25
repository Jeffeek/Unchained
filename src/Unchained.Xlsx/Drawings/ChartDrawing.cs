using Unchained.Ooxml.Charts;

namespace Unchained.Xlsx.Drawings;

/// <summary>A chart anchored on a worksheet's drawing layer, backed by an <c>xl/charts/chart*.xml</c> part.</summary>
public sealed class ChartDrawing : WorksheetDrawing
{
    /// <summary>The chart model — type, title, series data, legend, axes.</summary>
    public ChartModel Chart { get; set; } = new();

    /// <summary>
    ///     The raw bytes of the backing <c>chart.xml</c> part when the chart was loaded from a file
    ///     (preserved verbatim). <see langword="null" /> for charts created programmatically.
    /// </summary>
    internal byte[]? ChartPartData { get; set; }

    /// <summary>The OPC part URI of the backing chart (assigned on write).</summary>
    internal string ChartPartUri { get; set; } = string.Empty;
}
