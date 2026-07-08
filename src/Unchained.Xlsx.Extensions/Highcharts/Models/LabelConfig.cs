namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Label configuration for axes and data labels.
/// </summary>
public class LabelConfig
{
    /// <summary>Format string for the label.</summary>
    public string? Format { get; set; }

    /// <summary>Label rotation in degrees.</summary>
    public double? Rotation { get; set; }

    /// <summary>Label alignment: "left", "center", "right".</summary>
    public string? Align { get; set; }

    /// <summary>Label vertical alignment.</summary>
    public string? VerticalAlign { get; set; }

    /// <summary>Label X offset in pixels.</summary>
    public double? X { get; set; }

    /// <summary>Label Y offset in pixels.</summary>
    public double? Y { get; set; }

    /// <summary>Label color.</summary>
    public string? Color { get; set; }

    /// <summary>Label font size in pixels.</summary>
    public double? FontSize { get; set; }

    /// <summary>Label rotation step (rotate every Nth label).</summary>
    public int? Step { get; set; }

    /// <summary>Label style (font family, size, color, etc.).</summary>
    public StyleConfig? Style { get; set; }
}
