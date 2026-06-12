using Unchained.Ooxml;

namespace Unchained.Pptx.Shapes;

/// <summary>
///     A container shape that groups one or more child shapes so they can be
///     moved, resized, and formatted together as a unit.
/// </summary>
public sealed class GroupShape : Shape
{
    /// <summary>The child shapes that belong to this group.</summary>
    public ShapeCollection Children { get; } = new();

    /// <summary>
    ///     The group's child coordinate-space origin (<c>a:chOff</c>). Child shape coordinates
    ///     are expressed in this space and mapped onto the group's own rectangle on the slide.
    /// </summary>
    public Emu ChildOffsetX { get; set; }

    /// <inheritdoc cref="ChildOffsetX" />
    public Emu ChildOffsetY { get; set; }

    /// <summary>
    ///     The group's child coordinate-space extent (<c>a:chExt</c>). Combined with
    ///     <see cref="ChildOffsetX" />/<see cref="ChildOffsetY" /> and the group's own offset/extent
    ///     this defines the affine map from child space to slide space.
    /// </summary>
    public Emu ChildExtentWidth { get; set; }

    /// <inheritdoc cref="ChildExtentWidth" />
    public Emu ChildExtentHeight { get; set; }
}
