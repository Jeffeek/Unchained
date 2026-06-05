namespace Unchained.Pptx.Shapes;

/// <summary>
/// A container shape that groups one or more child shapes so they can be
/// moved, resized, and formatted together as a unit.
/// </summary>
public sealed class GroupShape : Shape
{
    /// <summary>The child shapes that belong to this group.</summary>
    public ShapeCollection Children { get; } = new();
}
