namespace Unchained.Pptx.Animations;

/// <summary>
/// An animation sequence triggered by clicking a specific shape, rather than advancing
/// the slide normally. Used for interactive slide-show navigation and click-to-animate shapes.
/// </summary>
public sealed class InteractiveSequence
{
    /// <summary>
    /// The <see cref="Shapes.Shape.ShapeId"/> of the shape whose click triggers this sequence.
    /// </summary>
    public uint TriggerShapeId { get; set; }

    /// <summary>The animation effects that play when the trigger shape is clicked.</summary>
    public AnimationSequence Sequence { get; } = new();
}
