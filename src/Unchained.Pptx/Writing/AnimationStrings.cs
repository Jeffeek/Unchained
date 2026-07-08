namespace Unchained.Pptx.Writing;

/// <summary>
///     OOXML animation string values used by <see cref="AnimationWriter" /> and
///     <see cref="Parsing.AnimationParser" />.
/// </summary>
internal static class AnimationStrings
{
    /// <summary>
    ///     The <c>p:fill="hold"</c> value — keeps the animated element visible after animation completes.
    ///     ISO/IEC 29500-1 §20.1.10.18.
    /// </summary>
    public const string FillHold = "hold";

    /// <summary>
    ///     The <c>dur="indefinite"</c> / <c>delay="indefinite"</c> / <c>repeatCount="indefinite"</c> value —
    ///     repeat indefinitely (e.g. click-triggered groups). ISO/IEC 29500-1 §20.1.10.25.
    /// </summary>
    public const string RepeatIndefinite = "indefinite";
}
