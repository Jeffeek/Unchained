namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     A shape within an annotation.
/// </summary>
public class ShapeItem
{
    /// <summary>Shape type: "rect", "ellipse", "path", "marker".</summary>
    public string? Type { get; set; }

    /// <summary>Shape fill color.</summary>
    public string? Fill { get; set; }

    /// <summary>Shape stroke color.</summary>
    public string? Stroke { get; set; }

    /// <summary>Stroke width in pixels.</summary>
    public double? StrokeWidth { get; set; }

    /// <summary>Corner radius for rectangles.</summary>
    public double? BorderRadius { get; set; }

    /// <summary>
    ///     Path definition (M, L, C, Z commands).
    ///     Used when <see cref="Type" /> is "path".
    /// </summary>
    public string? Path { get; set; }

    /// <summary>Shape width in pixels.</summary>
    public double? Width { get; set; }

    /// <summary>Shape height in pixels.</summary>
    public double? Height { get; set; }

    /// <summary>X position (pixels or percentage).</summary>
    public double? X { get; set; }

    /// <summary>Y position (pixels or percentage).</summary>
    public double? Y { get; set; }
}
