namespace Unchained.Pdf.Models;

/// <summary>
/// A PDF annotation attached to a page (ISO 32000-1 §12.5).
/// Coordinates are in PDF user space (points, origin bottom-left).
/// <param name="Subtype">The annotation subtype.</param>
/// <param name="X">Left edge of the annotation rectangle, in points.</param>
/// <param name="Y">Bottom edge of the annotation rectangle, in points.</param>
/// <param name="Width">Width of the annotation rectangle, in points.</param>
/// <param name="Height">Height of the annotation rectangle, in points.</param>
/// <param name="Contents">Optional text content of the annotation (maps to <c>/Contents</c>).</param>
/// <param name="Color">Optional RGB colour as a three-element array [R, G, B] with components in [0, 1]. Maps to the <c>/C</c> entry.</param>
/// </summary>
public sealed record Annotation(
    AnnotationSubtype Subtype,
    float X,
    float Y,
    float Width,
    float Height,
    string? Contents = null,
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
