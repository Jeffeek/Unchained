namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>A single data series within a chart.</summary>
public class SeriesConfig : IHasAdditionalProperties
{
    /// <summary>Series display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Series chart type (overrides chart default for this series).</summary>
    public string? Type { get; set; }

    /// <summary>Whether each point uses its own color from the data array.</summary>
    public bool? ColorByPoint { get; set; }

    /// <summary>Explicit RGB colour as <c>#RRGGBB</c>. Null = let the frontend theme decide.</summary>
    public string? Color { get; set; }

    /// <summary>Data points (simple scalar values). <see langword="null" /> entries represent missing data.</summary>
    public List<double?> Data { get; set; } = new();

    /// <summary>Advanced data points with per-point properties (colour, sliced, etc.).</summary>
    public List<DataPoint?> DataPoints { get; set; } = new();

    /// <summary>
    ///     Index of the Y-axis this series renders on.
    ///     <c>0</c> = primary axis, <c>1</c> = secondary axis.
    /// </summary>
    public int YAxis { get; set; }

    /// <summary>Marker configuration for this series.</summary>
    public MarkerConfig? Marker { get; set; }

    /// <summary>Additional properties not covered by the typed API.</summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }

    Dictionary<string, object>? IHasAdditionalProperties.GetAdditionalProperties() => AdditionalProperties;
}
