namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     A point that an annotation is anchored to.
/// </summary>
public class AnnotationPoint
{
    /// <summary>Series index in the chart.</summary>
    public int Series { get; set; }

    /// <summary>Data point index within the series.</summary>
    public int X { get; set; }

    /// <summary>Y value of the point.</summary>
    public double? Y { get; set; }

    /// <summary>Category name of the point (for category axes).</summary>
    public string? Category { get; set; }
}
