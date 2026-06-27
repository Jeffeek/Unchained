using Unchained.Ooxml;
using Unchained.Pptx.Animations;
using Unchained.Pptx.Shapes;

namespace Unchained.Studio.Studio.Pptx;

/// <summary>
///     Tracks which resize handle (if any) is currently active for a shape.
/// </summary>
public sealed record ResizeHandle(
    ResizeHandleKind Kind,
    SlidePlayboardState Playboard
);

/// <summary>
///     Represents which resize handle is active on a shape.
/// </summary>
public enum ResizeHandleKind
{
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left
}

/// <summary>
///     The interactive state for the PPTX slide playboard — shape selection, drag, resize.
/// </summary>
public sealed class SlidePlayboardState
{
    /// <summary>Currently selected shape, or <see langword="null"/> when nothing is selected.</summary>
    public Shape? SelectedShape { get; set; }

    /// <summary>Whether the user is currently dragging a shape body.</summary>
    public bool IsDragging { get; set; }

    /// <summary>Whether the user is currently resizing a shape via a handle.</summary>
    public bool IsResizing { get; set; }

    /// <summary>The resize handle being dragged, if resizing.</summary>
    public ResizeHandle? ActiveHandle { get; set; }

    /// <summary>Starting position (in overlay pixels) for drag/resize calculations.</summary>
    public (double MouseX, double MouseY)? DragStart { get; set; }

    /// <summary>Shape's original position before the current drag started.</summary>
    public (Emu OriginalX, Emu OriginalY)? DragOrigPos { get; set; }

    /// <summary>Shape's original size before the current resize started.</summary>
    public (Emu OrigWidth, Emu OrigHeight)? DragOrigSize { get; set; }

    /// <summary>
    ///     Zoom level for the playboard (1 = 100%, 2 = 200%, 0.5 = 50%).
    ///     Affects the size of the rendered slide and the overlay.
    /// </summary>
    public double Zoom { get; set; } = 0.75;

    /// <summary>
    ///     Raised when a shape is added to the slide (after the shape has been created).
    /// </summary>
    public event Action<Shape>? ShapeAdded;

    /// <summary>
    ///     Raised when the document is dirty (shapes were modified).
    /// </summary>
    public event Action? Dirty;

    /// <summary>
    ///     Invokes <see cref="Dirty"/>. Call from outside the type since events can only be
    ///     raised directly from within the declaring type.
    /// </summary>
    public void InvokeDirty() => Dirty?.Invoke();

    /// <summary>
    ///     Raises <see cref="ShapeAdded"/> and <see cref="Dirty"/>.
    /// </summary>
    public void OnShapeAdded(Shape shape)
    {
        SelectedShape = shape;
        ShapeAdded?.Invoke(shape);
        Dirty?.Invoke();
    }

    /// <summary>
    ///     Clears selection and resets drag/resize state.
    /// </summary>
    public void ClearSelection()
    {
        SelectedShape = null;
        IsDragging = false;
        IsResizing = false;
        ActiveHandle = null;
        DragStart = null;
        DragOrigPos = null;
        DragOrigSize = null;
    }
}
