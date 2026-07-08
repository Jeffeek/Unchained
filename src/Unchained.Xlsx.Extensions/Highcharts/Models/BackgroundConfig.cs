namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Background configuration for panes, legends, and tooltips.
/// </summary>
public class BackgroundConfig
{
    /// <summary>Background color (hex, rgba, or named).</summary>
    public string? Fill { get; set; }

    /// <summary>Border color.</summary>
    public string? BorderColor { get; set; }

    /// <summary>Border width in pixels.</summary>
    public double? BorderWidth { get; set; }

    /// <summary>Corner radius in pixels.</summary>
    public double? BorderRadius { get; set; }

    /// <summary>Background shape: "rectangle", "circle", "arc".</summary>
    public string? Shape { get; set; }
}
