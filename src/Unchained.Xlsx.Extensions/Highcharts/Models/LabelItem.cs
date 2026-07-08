namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     A label within an annotation.
/// </summary>
public class LabelItem
{
    /// <summary>Label text content.</summary>
    public string? Text { get; set; }

    /// <summary>Label background color.</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>Label text color.</summary>
    public string? Color { get; set; }

    /// <summary>Label font size in pixels.</summary>
    public double? FontSize { get; set; }

    /// <summary>Label horizontal alignment.</summary>
    public string? Align { get; set; }

    /// <summary>Label vertical alignment.</summary>
    public string? VerticalAlign { get; set; }

    /// <summary>Label X position offset.</summary>
    public double? X { get; set; }

    /// <summary>Label Y position offset.</summary>
    public double? Y { get; set; }

    /// <summary>Border radius for the label background.</summary>
    public double? BorderRadius { get; set; }

    /// <summary>Padding inside the label in pixels.</summary>
    public double? Padding { get; set; }
}
