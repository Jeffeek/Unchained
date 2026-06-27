namespace Unchained.Xlsx.Drawings;

/// <summary>
///     Base type for anything anchored onto a worksheet's drawing layer (pictures, charts).
///     Each drawing carries its grid <see cref="Anchor" />, a display <see cref="Name" />, and the
///     relationship id linking it from <c>drawing*.xml</c> to its backing part.
/// </summary>
public abstract class WorksheetDrawing
{
    /// <summary>The position/size of the drawing on the worksheet grid.</summary>
    public DrawingAnchor Anchor { get; set; } = new();

    /// <summary>The display name of the drawing (shown in the application's selection pane).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The shape id within the drawing part. Assigned on write when unset.</summary>
    internal int ShapeId { get; set; }

    /// <summary>The relationship id (<c>rId*</c>) from the drawing part to this drawing's backing part.</summary>
    internal string RelationshipId { get; set; } = string.Empty;
}
