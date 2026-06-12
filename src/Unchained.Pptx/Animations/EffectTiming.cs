namespace Unchained.Pptx.Animations;

/// <summary>
///     Timing parameters for a single animation effect.
/// </summary>
public sealed class EffectTiming
{
    /// <summary>
    ///     Delay before the effect starts, in seconds.
    ///     Default: <c>0.0</c> (starts immediately relative to its trigger).
    /// </summary>
    public double DelaySeconds { get; set; }

    /// <summary>
    ///     How long the animation plays, in seconds.
    ///     Default: <c>0.5</c>.
    /// </summary>
    public double DurationSeconds { get; set; } = 0.5;

    /// <summary>
    ///     Number of times the animation repeats. <c>0</c> means the animation plays once
    ///     (no repeat). Set to a negative value to repeat indefinitely.
    ///     Default: <c>0</c>.
    /// </summary>
    public int RepeatCount { get; set; }

    /// <summary>
    ///     When <see langword="true" />, the animation plays forward then reverses at the end.
    ///     Default: <see langword="false" />.
    /// </summary>
    public bool AutoReverse { get; set; }

    /// <summary>
    ///     Fraction of the duration (0.0–1.0) spent accelerating at the start (ease-in).
    ///     <c>0</c> means no acceleration. Maps to the OOXML <c>accel</c> attribute.
    /// </summary>
    public double AccelerationPercent { get; set; }

    /// <summary>
    ///     Fraction of the duration (0.0–1.0) spent decelerating at the end (ease-out).
    ///     <c>0</c> means no deceleration. Maps to the OOXML <c>decel</c> attribute.
    /// </summary>
    public double DecelerationPercent { get; set; }
}
