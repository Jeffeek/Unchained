namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>Tooltip configuration.</summary>
public class Tooltip : IHasAdditionalProperties
{
    /// <summary>Whether crosshairs are shown.</summary>
    public bool? Crosshairs { get; set; }

    /// <summary>Whether the tooltip snaps to the data point.</summary>
    public bool? Snap { get; set; }

    /// <summary>Header format string for the tooltip.</summary>
    public string? HeaderFormat { get; set; }

    /// <summary>Point format string for the tooltip.</summary>
    public string? PointFormat { get; set; }

    /// <summary>Whether the tooltip is shared across series.</summary>
    public bool? Shared { get; set; }

    /// <summary>Whether the tooltip is positioned outside the plot area.</summary>
    public bool? Outside { get; set; }

    /// <summary>JavaScript formatter function string.</summary>
    public string? Formatter { get; set; }

    /// <summary>Tooltip style (font family, size, color, etc.).</summary>
    public StyleConfig? Style { get; set; }

    /// <summary>Additional properties not covered by the typed API.</summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }

    Dictionary<string, object>? IHasAdditionalProperties.GetAdditionalProperties() => AdditionalProperties;
}
