namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     Annotations for a chart — custom shapes, lines, and labels overlaid on the plot area.
/// </summary>
public class Annotations
{
    /// <summary>List of annotation items.</summary>
    public List<AnnotationItem> Items { get; set; } = new();
}
