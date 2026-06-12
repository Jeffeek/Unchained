namespace Unchained.Pptx.Animations;

/// <summary>
///     Determines when an animation effect starts playing relative to the preceding event.
/// </summary>
public enum EffectTrigger
{
    /// <summary>
    ///     The effect starts on the next mouse click or key press.
    ///     In the OOXML timing tree this creates a new click group
    ///     (<c>nodeType="clickEffect"</c>).
    /// </summary>
    OnClick,

    /// <summary>
    ///     The effect starts at the same time as the preceding effect.
    ///     OOXML: <c>nodeType="withEffect"</c>.
    /// </summary>
    WithPrevious,

    /// <summary>
    ///     The effect starts immediately after the preceding effect finishes.
    ///     OOXML: <c>nodeType="afterEffect"</c>.
    /// </summary>
    AfterPrevious
}
