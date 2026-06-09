using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Models.Shapes;
using Unchained.Ooxml.Text;

namespace Unchained.Pptx.Shapes;

/// <summary>
/// A shape with a preset or custom geometry, optionally containing a text body.
/// This covers rectangular boxes, ellipses, polygons, stars, callouts, flowchart
/// symbols, and all other shapes in the OOXML preset geometry catalogue.
/// Text boxes are also represented as <see cref="AutoShape"/> with
/// <see cref="IsTextBox"/> set to <see langword="true"/>.
/// </summary>
public sealed class AutoShape : Shape
{
    /// <summary>
    /// The preset shape geometry type. Use <see cref="AutoShapeType.Custom"/> when the
    /// shape has a custom geometry defined in the OOXML <c>&lt;a:custGeom&gt;</c> element.
    /// </summary>
    public AutoShapeType ShapeType { get; set; } = AutoShapeType.Rectangle;

    /// <summary>
    /// <see langword="true"/> when this shape was originally created as a text box
    /// rather than a drawn shape.
    /// </summary>
    public bool IsTextBox { get; set; }

    /// <summary>The text body of this shape. Always present, even when the shape contains no text.</summary>
    public TextFrame TextFrame { get; } = new();

    /// <summary>
    /// Convenience shortcut for the shape's plain text. Getting concatenates all paragraph text
    /// (newline-separated); setting replaces the content with a single paragraph/run. Equivalent
    /// to <c>TextFrame.PlainText</c>.
    /// </summary>
    public string Text
    {
        get => TextFrame.PlainText;
        set => TextFrame.PlainText = value;
    }
}
