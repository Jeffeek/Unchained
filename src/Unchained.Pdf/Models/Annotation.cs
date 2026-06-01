namespace Unchained.Pdf.Models;

/// <summary>
/// A PDF annotation attached to a page (ISO 32000-1 §12.5).
/// Coordinates are in PDF user space (points, origin bottom-left).
/// </summary>
public sealed record Annotation(
    /// <summary>The annotation subtype.</summary>
    AnnotationSubtype Subtype,
    /// <summary>Left edge of the annotation rectangle, in points.</summary>
    float X,
    /// <summary>Bottom edge of the annotation rectangle, in points.</summary>
    float Y,
    /// <summary>Width of the annotation rectangle, in points.</summary>
    float Width,
    /// <summary>Height of the annotation rectangle, in points.</summary>
    float Height,
    /// <summary>Optional text content of the annotation (maps to <c>/Contents</c>).</summary>
    string? Contents = null,
    /// <summary>
    /// Optional RGB colour as a three-element array [R, G, B] with components in [0, 1].
    /// Maps to the <c>/C</c> entry.
    /// </summary>
    float[]? Color = null
);

/// <summary>PDF annotation subtypes (ISO 32000-1 Table 169).</summary>
public enum AnnotationSubtype
{
    /// <summary>Sticky-note comment.</summary>
    Text,
    /// <summary>Text highlight.</summary>
    Highlight,
    /// <summary>Hyperlink or action.</summary>
    Link,
    /// <summary>Free-text box.</summary>
    FreeText,
    /// <summary>Rectangle outline.</summary>
    Square,
    /// <summary>Ellipse outline.</summary>
    Circle
}
