namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     A plot line on an axis — a straight line spanning the axis at a specific value.
/// </summary>
public class PlotLine
{
    /// <summary>The value at which the line is drawn.</summary>
    public double? Value { get; set; }

    /// <summary>Line color as <c>#RRGGBB</c>.</summary>
    public string? Color { get; set; }

    /// <summary>Line width in pixels.</summary>
    public double? Width { get; set; }

    /// <summary>Line dash style (e.g. "solid", "dash", "dot").</summary>
    public string? DashStyle { get; set; }

    /// <summary>Unique identifier for the plot line.</summary>
    public string? Id { get; set; }

    /// <summary>Label text displayed on the plot line.</summary>
    public string? Label { get; set; }
}
