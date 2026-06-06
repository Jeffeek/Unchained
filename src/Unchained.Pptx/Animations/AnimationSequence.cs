namespace Unchained.Pptx.Animations;

/// <summary>
/// An ordered collection of <see cref="AnimationEffect"/> objects that play sequentially
/// or simultaneously according to their <see cref="AnimationEffect.Trigger"/> settings.
/// </summary>
public sealed class AnimationSequence
{
    private readonly List<AnimationEffect> _effects = [];

    /// <summary>The effects in this sequence, in their presentation order.</summary>
    public IReadOnlyList<AnimationEffect> Effects => _effects;

    /// <summary>
    /// Adds a new animation effect targeting the shape with the given ID.
    /// </summary>
    /// <param name="targetShapeId">The <see cref="Shapes.Shape.ShapeId"/> of the shape to animate.</param>
    /// <param name="preset">The animation preset.</param>
    /// <param name="category">The effect category (Entrance, Exit, Emphasis, Motion).</param>
    /// <param name="trigger">When the effect starts.</param>
    /// <param name="delaySeconds">Additional delay before the effect starts, in seconds.</param>
    public AnimationEffect AddEffect(
        uint targetShapeId,
        AnimationPreset preset = AnimationPreset.Fade,
        EffectCategory category = EffectCategory.Entrance,
        EffectTrigger trigger = EffectTrigger.OnClick,
        double delaySeconds = 0.0)
    {
        var effect = new AnimationEffect
        {
            TargetShapeId = targetShapeId,
            Preset = preset,
            Category = category,
            Trigger = trigger,
        };
        effect.Timing.DelaySeconds = delaySeconds;
        _effects.Add(effect);
        return effect;
    }

    /// <summary>Removes the given effect from the sequence.</summary>
    /// <exception cref="ArgumentException">Thrown when the effect is not in this sequence.</exception>
    public void Remove(AnimationEffect effect)
    {
        if (!_effects.Remove(effect))
            throw new ArgumentException("The effect does not belong to this sequence.", nameof(effect));
    }

    /// <summary>Removes all effects from the sequence.</summary>
    public void Clear() => _effects.Clear();
}
