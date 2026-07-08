using Unchained.Ooxml.Charts;
using Unchained.Xlsx.Abstractions;

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

    /// <summary>
    ///     The <see cref="ISpreadsheetDocument" /> that owns this drawing is anchored to.
    ///     Provided so callers can trigger formula evaluation (e.g. <c>chart.Workbook?.Recalculate()</c>)
    ///     before reading evaluated data values.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public ISpreadsheetDocument? Workbook { get; set; }

    /// <summary>
    ///     Loosely-typed annotation payload attached by extension packages (e.g. the Highcharts converter in
    ///     <c>Unchained.Xlsx.Extensions</c>). Stored as <see cref="object" /> because the core assembly cannot
    ///     reference the extension assembly that defines the concrete annotation type; consumers cast it back.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public object? Annotations { get; set; }
}
