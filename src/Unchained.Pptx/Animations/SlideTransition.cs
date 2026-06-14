namespace Unchained.Pptx.Animations;

/// <summary>
///     Controls the animated effect that plays when advancing to a slide during a slide show.
/// </summary>
public sealed class SlideTransition
{
    /// <summary>
    ///     The visual transition effect. Use <see cref="TransitionEffect.None" /> to remove the
    ///     transition entirely.
    ///     Default: <see cref="TransitionEffect.None" />.
    /// </summary>
    public TransitionEffect Effect { get; set; } = TransitionEffect.None;

    /// <summary>
    ///     The duration of the transition animation in seconds.
    ///     <c>0.0</c> lets the presenter application choose its default.
    ///     Default: <c>0.5</c> seconds.
    /// </summary>
    public double DurationSeconds { get; set; } = 0.5;

    /// <summary>
    ///     When <see langword="true" />, clicking the mouse or pressing a key advances the slide
    ///     (the default PowerPoint behaviour).
    ///     Default: <see langword="true" />.
    /// </summary>
    public bool AdvanceOnClick { get; set; } = true;

    /// <summary>
    ///     Number of seconds after which the slide automatically advances, regardless of clicks.
    ///     <see langword="null" /> disables automatic advance.
    ///     Default: <see langword="null" />.
    /// </summary>
    public double? AutoAdvanceSeconds { get; set; }
}
