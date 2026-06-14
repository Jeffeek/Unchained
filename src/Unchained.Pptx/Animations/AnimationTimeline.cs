namespace Unchained.Pptx.Animations;

/// <summary>
///     The complete animation timeline for a slide, containing the main click-driven sequence
///     and any interactive sequences triggered by clicking specific shapes.
/// </summary>
public sealed class AnimationTimeline
{
    private readonly List<InteractiveSequence> _interactiveSequences = [];

    /// <summary>
    ///     The main animation sequence — effects that play in order as the presenter clicks
    ///     through the slide.
    /// </summary>
    public AnimationSequence MainSequence { get; } = new();

    /// <summary>
    ///     Sequences that play only when a specific shape is clicked during the slide show.
    /// </summary>
    public IReadOnlyList<InteractiveSequence> InteractiveSequences => _interactiveSequences;

    /// <summary>
    ///     <see langword="true" /> when the timeline contains at least one effect in any sequence.
    /// </summary>
    public bool HasAnimations =>
        MainSequence.Effects.Count > 0 || _interactiveSequences.Count > 0;

    /// <summary>
    ///     Adds a new interactive sequence triggered by the shape with the given ID.
    /// </summary>
    public InteractiveSequence AddInteractiveSequence(uint triggerShapeId)
    {
        var seq = new InteractiveSequence { TriggerShapeId = triggerShapeId };
        _interactiveSequences.Add(seq);
        return seq;
    }

    /// <summary>Removes the given interactive sequence.</summary>
    public void RemoveInteractiveSequence(InteractiveSequence sequence)
    {
        if (!_interactiveSequences.Remove(sequence))
        {
            throw new ArgumentException(
                "The sequence does not belong to this timeline.",
                nameof(sequence)
            );
        }
    }
}
