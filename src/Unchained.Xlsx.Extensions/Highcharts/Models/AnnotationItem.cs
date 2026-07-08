namespace Unchained.Xlsx.Extensions.Highcharts.Models;

/// <summary>
///     A single annotation item.
/// </summary>
public class AnnotationItem : IHasAdditionalProperties
{
    /// <summary>Unique identifier for the annotation.</summary>
    public string? Id { get; set; }

    /// <summary>Annotation title.</summary>
    public string? Title { get; set; }

    /// <summary>Shapes in this annotation.</summary>
    public List<ShapeItem> Shapes { get; set; } = new();

    /// <summary>Labels in this annotation.</summary>
    public List<LabelItem> Labels { get; set; } = new();

    /// <summary>
    ///     Points that the annotation is anchored to.
    ///     For data-point annotations, use the series index and data point index.
    /// </summary>
    public List<AnnotationPoint> Points { get; set; } = new();

    /// <summary>Additional properties not covered by the typed API.</summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }

    Dictionary<string, object>? IHasAdditionalProperties.GetAdditionalProperties() => AdditionalProperties;
}
