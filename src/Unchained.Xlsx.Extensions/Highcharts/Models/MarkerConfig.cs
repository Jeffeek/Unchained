namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Marker configuration for data points.
/// </summary>
public class MarkerConfig
{
    /// <summary>Whether to show markers.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Marker radius in pixels.</summary>
    public double? Radius { get; set; }

    /// <summary>Marker fill color.</summary>
    public string? FillColor { get; set; }

    /// <summary>Marker border color.</summary>
    public string? LineColor { get; set; }

    /// <summary>Marker line width.</summary>
    public double? LineWidth { get; set; }

    /// <summary>Marker shape: "circle", "square", "diamond", "triangle".</summary>
    public string? Symbol { get; set; }
}
