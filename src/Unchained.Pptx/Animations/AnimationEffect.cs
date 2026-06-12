namespace Unchained.Pptx.Animations;

/// <summary>
///     A single animation effect applied to one shape on a slide.
/// </summary>
public sealed class AnimationEffect
{
    /// <summary>
    ///     The <c>id</c> attribute of the target shape (<see cref="Shapes.Shape.ShapeId" />).
    /// </summary>
    public uint TargetShapeId { get; set; }

    /// <summary>
    ///     The visual preset that defines how the shape animates.
    ///     Default: <see cref="AnimationPreset.Fade" />.
    /// </summary>
    public AnimationPreset Preset { get; set; } = AnimationPreset.Fade;

    /// <summary>
    ///     Whether the shape enters, exits, draws attention, or moves.
    ///     Default: <see cref="EffectCategory.Entrance" />.
    /// </summary>
    public EffectCategory Category { get; set; } = EffectCategory.Entrance;

    /// <summary>
    ///     When the effect starts relative to the preceding event.
    ///     Default: <see cref="EffectTrigger.OnClick" />.
    /// </summary>
    public EffectTrigger Trigger { get; set; } = EffectTrigger.OnClick;

    /// <summary>Timing settings: delay, duration, repeat, auto-reverse.</summary>
    public EffectTiming Timing { get; } = new();
}
